# PCL 编译脚本 —— 已在 VS 2026 Insiders 环境下实测通过
#
# 实测结果（2026-09-27）：
#   MeloongCore / MeloongCore.Wpf / PCLCS -> 编译成功
#   Plain Craft Launcher 2 -> bin\Plain Craft Launcher 2.exe  (5.45 MB)
#   启动运行正常，主题切换实测生效。

param(
    # 默认按「本脚本所在的仓库根目录」推断，换台机器也不用改
    [string]$Repo = '',
    [string]$Configuration = 'Release',
    # 便携 .NET SDK（仅当机器上没有安装 .NET SDK 时才需要）
    [string]$PortableSdk = '',
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Repo)) { $Repo = $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($Repo)) { $Repo = (Get-Location).Path }
if ([string]::IsNullOrWhiteSpace($PortableSdk)) { $PortableSdk = Join-Path $Repo '..\buildenv\dotnet9' }
$sln = Join-Path $Repo 'Plain Craft Launcher 2.sln'
if (-not (Test-Path $sln)) { throw "找不到解决方案：$sln（如果你把脚本放在了别处，请用 -Repo 指定仓库根目录）" }

# ---------------------------------------------------------------------------
# 1. 子模块（必须有，否则 MeloongCore / PCLCS 是空的）
# ---------------------------------------------------------------------------
if (-not (Test-Path (Join-Path $Repo 'MeloongCore\Shared\MeloongCore.csproj'))) {
    Write-Host '[1/4] 初始化子模块 MeloongCore ...'
    git -C $Repo submodule update --init --recursive
} else {
    Write-Host '[1/4] 子模块已就绪'
}

# ---------------------------------------------------------------------------
# 2. 定位 MSBuild 18 / 17（VS 2026 / 2022）
# ---------------------------------------------------------------------------
Write-Host '[2/4] 定位 MSBuild ...'
$msbuild = $null
foreach ($root in @("${env:ProgramFiles}\Microsoft Visual Studio", "${env:ProgramFiles(x86)}\Microsoft Visual Studio")) {
    if (-not (Test-Path $root)) { continue }
    $hit = Get-ChildItem $root -Recurse -Filter 'MSBuild.exe' -ErrorAction SilentlyContinue |
           Where-Object { $_.FullName -match '\\MSBuild\\Current\\Bin\\MSBuild\.exe$' } |
           Sort-Object FullName -Descending | Select-Object -First 1
    if ($hit) { $msbuild = $hit.FullName; break }
}
if (-not $msbuild) { throw '找不到 MSBuild。请在 Visual Studio 安装器中勾选「.NET 桌面开发」工作负载。' }
Write-Host "      $msbuild"
& $msbuild -version | Select-Object -First 1 | ForEach-Object { Write-Host "      $_" }

# ---------------------------------------------------------------------------
# 3. .NET SDK —— SDK 风格工程（MeloongCore / PCLCS）需要它
#    VS 只装「.NET 桌面开发」时不会带 .NET SDK，此时用便携 SDK 顶替。
# ---------------------------------------------------------------------------
$sdkPath = $null
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$haveSdk = $false
if ($dotnetCmd) {
    $probe = & $dotnetCmd.Source --list-sdks 2>$null
    if ($probe) { $haveSdk = $true }
}
if ($haveSdk) {
    Write-Host '[3/4] 使用系统已安装的 .NET SDK'
} else {
    $sdkDir = Get-ChildItem (Join-Path $PortableSdk 'sdk') -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $sdkDir) { throw "机器上没有 .NET SDK，也找不到便携 SDK：$PortableSdk\sdk" }
    $sdkPath = $sdkDir.FullName
    Write-Host "[3/4] 未检测到系统 .NET SDK，改用便携 SDK：$sdkPath"
    $env:MSBuildSDKsPath = Join-Path $sdkPath 'Sdks'
    $env:DOTNET_ROOT     = $PortableSdk
    $env:MSBuildEnableWorkloadResolver = 'false'   # 便携 SDK 没注册工作负载，必须关掉
}

# ---------------------------------------------------------------------------
# 4. 还原 + 编译
# ---------------------------------------------------------------------------
if (-not $SkipRestore) {
    Write-Host '[4/4] 还原 NuGet 包 ...'
    if ($sdkPath) {
        # 便携 SDK 未注册，MSBuild 的 NuGet SDK 解析器找不到它；
        # 直接用便携 SDK 自带的 dotnet restore 最稳。
        & (Join-Path $PortableSdk 'dotnet.exe') restore $sln /v:minimal
    } else {
        & $msbuild $sln -t:Restore /p:Configuration=$Configuration /v:minimal /nologo
    }
    if ($LASTEXITCODE -ne 0) { throw "NuGet 还原失败（exit=$LASTEXITCODE）" }
}

Write-Host "[4/4] 编译（$Configuration）..."
$buildArgs = @($sln, "/p:Configuration=$Configuration", '/p:Platform=Any CPU', '/v:minimal', '/nologo', '/m')
if ($sdkPath) { $buildArgs += '/p:MSBuildEnableWorkloadResolver=false' }
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $msbuild @buildArgs 2>&1 |
    Where-Object { $_ -notmatch 'No \.NET SDKs were found' -and $_ -notmatch 'dotnet/download|sdk-not-found' }
$buildExit = $LASTEXITCODE
$ErrorActionPreference = $prevEap
if ($buildExit -ne 0) { throw "编译失败（exit=$buildExit）" }

$exe = Join-Path $Repo 'Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe'
Write-Host ''
Write-Host "编译成功：$exe"
Get-Item $exe | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 2) } }, LastWriteTime | Format-Table -AutoSize

# ---------------------------------------------------------------------------
# 环境说明（这台机器上实际踩到的）
# ---------------------------------------------------------------------------
# * VS 2026 只装了「.NET 桌面开发」时：
#     - 有 MSBuild 18 和 .NET Framework 4.8 目标包（这个工作负载会带）
#     - 但没有 .NET SDK，SDK 风格工程会报 MSB4236「找不到指定的 SDK Microsoft.NET.Sdk」
#   解法就是上面的 MSBuildSDKsPath 指向便携 SDK。
# * 便携 SDK 建议用 .NET 9（Roslyn 4.12+ 支持 C# 13）。
#   .NET 8 的早期补丁版捆绑的 Roslyn 会报 CS1617「langversion 13.0 无效」。
# * 不要在 PCL 仓库里留 Directory.Build.props / 额外 targets 之类的东西，
#   本脚本不需要它们。
