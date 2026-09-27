# PCL2 offline compile check (works without Visual Studio).
# Uses .NET Framework 4.8 reference assemblies + Roslyn vbc pulled from NuGet.
# NOTE: WPF XAML compilation (PresentationBuildTasks / MarkupCompilePass) needs Visual Studio
#       components, so the XAML-generated .g.vb fields are absent. Reference errors against
#       those fields are EXPECTED NOISE and are classified separately below.
param(
    # 默认按「本脚本所在的仓库根目录」推断
    [string]$Repo = "",
    # 存放 net48 参考程序集与 Roslyn 的目录（由 buildenv 准备）
    [string]$Env  = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Repo)) { $Repo = $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($Repo)) { $Repo = (Get-Location).Path }
if ([string]::IsNullOrWhiteSpace($Env)) { $Env = Join-Path $Repo "..\buildenv" }
$proj = Join-Path $Repo "Plain Craft Launcher 2"
$vbc  = Join-Path $Env  "microsoft.net.compilers.3.11.0\tools\vbc.exe"
$refs = Join-Path $Env  "microsoft.netframework.referenceassemblies.net48.1.0.3\build\.NETFramework\v4.8"
$out  = Join-Path $env:TEMP "pcl_check"

if (-not (Test-Path $vbc))  { throw "vbc.exe not found: $vbc" }
if (-not (Test-Path $refs)) { throw "net48 reference assemblies not found: $refs" }

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $out | Out-Null

$projFile = Join-Path $proj "Plain Craft Launcher 2.vbproj"
[xml]$xml = Get-Content $projFile -Encoding UTF8
$sources = @()
foreach ($node in $xml.Project.ItemGroup.Compile) {
    if ($node.Include) {
        $p = Join-Path $proj $node.Include
        if (Test-Path $p) { $sources += $p }
    }
}
Write-Host ("source files: " + $sources.Count)

$refFiles = @(Get-ChildItem $refs -Filter *.dll | ForEach-Object { $_.FullName })
foreach ($d in Get-ChildItem (Join-Path $proj "Resources") -Filter *.dll -ErrorAction SilentlyContinue) {
    $refFiles += $d.FullName
}
foreach ($extra in @("PCLCS\bin\PCLCS.dll","PCLCS\bin\MeloongCore.dll","PCLCS\bin\MeloongCore.Wpf.dll")) {
    $p = Join-Path $Repo $extra
    if (Test-Path $p) { $refFiles += $p }
}

$A = New-Object System.Collections.Generic.List[string]
$A.Add("/nologo")
$A.Add("/target:library")
$A.Add("/optionexplicit+")
$A.Add("/optioninfer+")
$A.Add("/rootnamespace:PCL")
$A.Add("/define:SNAPSHOT")
$A.Add(("/out:" + (Join-Path $out "PCL_check.dll")))
$A.Add("/langversion:15.5")
$imports = "MeloongCore,MeloongCore.Extensions,MeloongCore.Wpf,MeloongCore.Wpf.Extensions,Microsoft.VisualBasic,Newtonsoft.Json,Newtonsoft.Json.Linq,PCLCS,System,System.Collections.Concurrent,System.Collections.Generic,System.IO,System.IO.Compression,System.Linq,System.Net,System.Net.Http,System.Text,System.Threading,System.Threading.Tasks,System.Xml.Linq,System.Collections,System.Diagnostics,System.Windows,System.Windows.Controls,System.Windows.Data,System.Windows.Documents,System.Windows.Input,System.Windows.Media,System.Windows.Media.Imaging,ThrottleDebounce"
$A.Add("/imports:$imports")
foreach ($r in ($refFiles | Sort-Object -Unique)) { $A.Add('/r:"' + $r + '"') }
foreach ($s in $sources) { $A.Add('"' + $s + '"') }

$rsp = Join-Path $out "vbc.rsp"
[System.IO.File]::WriteAllLines($rsp, $A, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "running Roslyn vbc ..."
$raw = & $vbc "@$rsp" 2>&1
$raw | Set-Content -Path (Join-Path $out "vbc_raw.txt") -Encoding UTF8

$lines = @($raw | ForEach-Object { $_.ToString() })
$errors = @($lines | Where-Object { $_ -match "error BC\d+" })
$syntax = @($errors | Where-Object { $_ -match "error BC3\d{3}" })
$noise  = @($errors | Where-Object { $_ -match "BC30451|BC30456|BC30002|BC30491|BC42024|BC42104|BC30455|BC30469|BC30183" })
$other  = @($errors | Where-Object { ($syntax -notcontains $_) -and ($noise -notcontains $_) })

Write-Host ""
Write-Host ("total errors : " + $errors.Count)
Write-Host ("syntax BC3xxx: " + $syntax.Count)
Write-Host ("xaml-field noise: " + $noise.Count)
Write-Host ("other errors : " + $other.Count)
Write-Host ""
if ($syntax.Count -gt 0) {
    Write-Host "===== SYNTAX ERRORS (must fix) ====="
    $syntax | Select-Object -First 60 | ForEach-Object { Write-Host $_ }
}
if ($other.Count -gt 0) {
    Write-Host "===== OTHER ERRORS (judge manually) ====="
    $other | Select-Object -First 60 | ForEach-Object { Write-Host $_ }
}
Write-Host ""
Write-Host ("full output: " + (Join-Path $out "vbc_raw.txt"))
