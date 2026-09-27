'由于包含加解密等安全信息，本文件中的部分代码已被删除

'取色与提取图片主色需要 System.Drawing，但不能直接 Imports —— 那会让
'LinearGradientBrush 解析到 GDI+ 的版本，把 ThemeRefreshMain 里的 WPF 画布搞坏。
'所以用命名空间别名，只在需要的地方显式限定。
Imports Draw = System.Drawing

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "")
    'CurseForge API Key
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"), "")
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "网络鉴权"

    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    ''' <summary>
    ''' 设置界面正在把预置主题的配色回填到自定义滑条。
    ''' 回填会触发 MySlider 的 Change 事件，若不屏蔽，就会把主题又切回「自定义」。
    ''' </summary>
    Public SyncingThemeSliders As Boolean = False

    ''' <summary>
    ''' 主题切换后，由设置界面挂上来的回调：把新主题的配色回填到四个配色滑条。
    ''' 用委托而不是直接调用，是为了避免主题模块反向依赖设置页面。
    ''' </summary>
    Public ThemeChangedHook As Action(Of Integer) = Nothing

    ''' <summary>
    ''' 内置主题数量（ID 0 ~ 19，其中 14 为自定义主题）。
    ''' </summary>
    Public Const ThemeCount As Integer = 20

    ''' <summary>
    ''' 内置主题的配色预置。
    ''' </summary>
    Private Structure ThemePreset
        Public ThemeName As String
        Public Hue As Integer
        Public Sat As Integer
        Public LightAdjust As Integer
        Public TopbarDelta As Integer
    End Structure

    ''' <summary>
    ''' 主题预置表，索引即主题 ID（0 ~ 19）。
    ''' 主题 14 为自定义，配色由设置中的滑条决定，此处的取值不会被使用。
    ''' </summary>
    Private ReadOnly ThemePresets As ThemePreset() = {
        New ThemePreset With {.ThemeName = "龙猫蓝", .Hue = 210, .Sat = 85, .LightAdjust = 0, .TopbarDelta = 0},      '0
        New ThemePreset With {.ThemeName = "甜柠青", .Hue = 185, .Sat = 75, .LightAdjust = 0, .TopbarDelta = -30},    '1
        New ThemePreset With {.ThemeName = "小草绿", .Hue = 100, .Sat = 60, .LightAdjust = 0, .TopbarDelta = 12},     '2
        New ThemePreset With {.ThemeName = "菠萝黄", .Hue = 40, .Sat = 95, .LightAdjust = 0, .TopbarDelta = -35},     '3
        New ThemePreset With {.ThemeName = "橡木棕", .Hue = 25, .Sat = 55, .LightAdjust = 5, .TopbarDelta = -10},     '4
        New ThemePreset With {.ThemeName = "玄素黑", .Hue = 210, .Sat = 8, .LightAdjust = -6, .TopbarDelta = 0},      '5
        New ThemePreset With {.ThemeName = "铁杆粉", .Hue = 330, .Sat = 75, .LightAdjust = 0, .TopbarDelta = 25},     '6
        New ThemePreset With {.ThemeName = "神秘紫", .Hue = 275, .Sat = 70, .LightAdjust = 0, .TopbarDelta = -25},    '7
        New ThemePreset With {.ThemeName = "秋仪金", .Hue = 38, .Sat = 55, .LightAdjust = -3, .TopbarDelta = 15},     '8
        New ThemePreset With {.ThemeName = "活跃橙", .Hue = 22, .Sat = 80, .LightAdjust = 0, .TopbarDelta = -20},     '9
        New ThemePreset With {.ThemeName = "跳票红", .Hue = 355, .Sat = 70, .LightAdjust = 0, .TopbarDelta = 18},     '10
        New ThemePreset With {.ThemeName = "极客蓝", .Hue = 220, .Sat = 90, .LightAdjust = -8, .TopbarDelta = 0},     '11
        New ThemePreset With {.ThemeName = "滑稽彩", .Hue = 45, .Sat = 90, .LightAdjust = 0, .TopbarDelta = 0},       '12
        New ThemePreset With {.ThemeName = "欧皇彩", .Hue = 300, .Sat = 85, .LightAdjust = 0, .TopbarDelta = 0},      '13
        New ThemePreset With {.ThemeName = "自定义", .Hue = 210, .Sat = 85, .LightAdjust = 0, .TopbarDelta = 0},      '14，取值不会被使用
        New ThemePreset With {.ThemeName = "深海蓝", .Hue = 200, .Sat = 55, .LightAdjust = -6, .TopbarDelta = 0},     '15
        New ThemePreset With {.ThemeName = "樱花粉", .Hue = 335, .Sat = 65, .LightAdjust = 0, .TopbarDelta = 20},     '16
        New ThemePreset With {.ThemeName = "薰衣紫", .Hue = 275, .Sat = 60, .LightAdjust = 0, .TopbarDelta = -24},    '17
        New ThemePreset With {.ThemeName = "曜石青", .Hue = 195, .Sat = 30, .LightAdjust = -12, .TopbarDelta = 8},    '18
        New ThemePreset With {.ThemeName = "抹茶绿", .Hue = 135, .Sat = 45, .LightAdjust = -4, .TopbarDelta = 16}}    '19

    ''' <summary>
    ''' 生成一个已冻结的纯色画刷，减少主题刷新后的渲染开销。
    ''' </summary>
    Private Function FrozenBrush(Col As MyColor) As SolidColorBrush
        Dim Bru As New SolidColorBrush(Col)
        Bru.Freeze()
        Return Bru
    End Function

    ''' <summary>
    ''' 将指定主题的配色参数载入当前主题变量。
    ''' 主题 14 为自定义，改为从设置中的滑条读取；越界时按主题 0 处理。
    ''' </summary>
    Friend Sub ThemeLoadPreset(Theme As Integer)
        If Theme < 0 OrElse Theme >= ThemeCount Then Theme = 0
        If Theme = 14 Then
            '自定义主题：从滑条设置读取，默认值依次为 180 / 80 / 20 / 90
            ColorHue = Settings.Get(Of Integer)("UiLauncherHue")
            ColorSat = Settings.Get(Of Integer)("UiLauncherSat")
            ColorLightAdjust = Settings.Get(Of Integer)("UiLauncherLight") - 20
            ColorHueTopbarDelta = CInt((Settings.Get(Of Integer)("UiLauncherDelta") - 90) * 2)
        Else
            Dim Preset As ThemePreset = ThemePresets(Theme)
            ColorHue = Preset.Hue
            ColorSat = Preset.Sat
            ColorLightAdjust = Preset.LightAdjust
            ColorHueTopbarDelta = Preset.TopbarDelta
        End If
    End Sub

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then ThemeNow = NewTheme

            '载入当前主题的配色参数
            If ThemeNow < 0 Then
                '从未指定过主题（仅滑条变化）：按自定义主题从设置中读取
                ThemeLoadPreset(14)
            Else
                ThemeLoadPreset(ThemeNow)
            End If

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = FrozenBrush(Color1)
            Application.Current.Resources("ColorBrush2") = FrozenBrush(Color2)
            Application.Current.Resources("ColorBrush3") = FrozenBrush(Color3)
            Application.Current.Resources("ColorBrush4") = FrozenBrush(Color4)
            Application.Current.Resources("ColorBrush5") = FrozenBrush(Color5)
            Application.Current.Resources("ColorBrush6") = FrozenBrush(Color6)
            Application.Current.Resources("ColorBrush7") = FrozenBrush(Color7)
            Application.Current.Resources("ColorBrush8") = FrozenBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = FrozenBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = FrozenBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
            '把新主题的配色回填到设置界面的四个配色滑条（由设置界面挂上来的回调）
            Try
                ThemeChangedHook?.Invoke(ThemeNow)
            Catch ex As Exception
                Logger.Warn(ex, "同步主题配色到滑条失败")
            End Try
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, 48 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, 48 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(245, 245, 245)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    ''' <summary>
    ''' 检查所有主题的解锁状态。开源版没有隐藏主题，所有主题均可直接选择，无需处理。
    ''' </summary>
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
        '开源版无隐藏主题门槛
    End Sub
    ''' <summary>
    ''' 检查指定主题是否可用。开源版所有主题均可直接选择。
    ''' </summary>
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return Id >= 0 AndAlso Id < ThemeCount
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        If Id < 0 OrElse Id >= ThemeCount Then Return False
        '开源版没有隐藏主题，所有主题一开始就能用；但调用方（如 FormMain、ModLaunch）
        '会把 True 当成"刚刚解锁了"并弹窗，所以这里必须按"是否首次"来返回，
        '否则每次启动 / 每次微软登录都会重复弹一次解锁提示。
        '解锁状态沿用原版的设置项持久化：0|1|2|3|4 是初始值，其余主题在自己首次解锁时补进去。
        SyncThemeUnlockState()
        If UnlockedThemes.Contains(Id) Then Return False
        MarkThemeUnlocked(Id)
        Return True
    End Function

    ''' <summary>
    ''' 记录已经弹过解锁提示的主题 ID，避免同一主题被反复"解锁"。
    ''' </summary>
    Private ReadOnly UnlockedThemes As New List(Of Integer)

    ''' <summary>
    ''' 把设置里记录过的解锁主题读进内存缓存。只执行一次，重复调用直接返回。
    ''' </summary>
    Private Sub SyncThemeUnlockState()
        If UnlockedThemes.Count > 0 Then Return
        Try
            For Each Part As String In Settings.Get(Of String)("UiLauncherThemeHide2").Split("|"c)
                Dim Tmp As Integer
                If Integer.TryParse(Part, Tmp) AndAlso Not UnlockedThemes.Contains(Tmp) Then UnlockedThemes.Add(Tmp)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取主题解锁状态失败")
        End Try
    End Sub
    ''' <summary>
    ''' 把主题标记为已解锁并写入设置。
    ''' </summary>
    Private Sub MarkThemeUnlocked(Id As Integer)
        If UnlockedThemes.Contains(Id) Then Return
        UnlockedThemes.Add(Id)
        Try
            Settings.Set("UiLauncherThemeHide2", UnlockedThemes.Join("|"c))
        Catch ex As Exception
            Logger.Warn(ex, "保存主题解锁状态失败")
        End Try
    End Sub
    ''' <summary>
    ''' 获取指定主题的配色参数。
    ''' 供设置界面在选中预置主题时，把四个配色滑条同步成该主题的值 ——
    ''' 否则滑条会停留在上一次自定义的取值上，用户一动它就跳回「自定义」。
    ''' 主题 14（自定义）与越界值返回 Nothing。
    ''' </summary>
    Friend Function ThemeGetPreset(Theme As Integer, ByRef Hue As Integer, ByRef Sat As Integer, ByRef LightAdjust As Integer, ByRef TopbarDelta As Integer) As Boolean
        If Theme < 0 OrElse Theme >= ThemeCount OrElse Theme = 14 Then Return False
        Dim Preset As ThemePreset = ThemePresets(Theme)
        Hue = Preset.Hue
        Sat = Preset.Sat
        LightAdjust = Preset.LightAdjust
        TopbarDelta = Preset.TopbarDelta
        Return True
    End Function

    ''' <summary>
    ''' 获取主题的中文名称，越界时返回"自定义"。
    ''' </summary>
    Friend Function ThemeGetName(Theme As Integer) As String
        If Theme < 0 OrElse Theme >= ThemeCount Then Return "自定义"
        Return ThemePresets(Theme).ThemeName
    End Function

#End Region

#Region "自定义配色方案"

    ''' <summary>
    ''' 一套用户保存下来的自定义配色。
    ''' 存进设置时编码为 名称~色调~饱和度~亮度~色调渐变，
    ''' 多套之间用 | 分隔；名称里可能带 #，所以整体用 URL 编码。
    ''' </summary>
    Public Class ColorProfile
        Public Name As String = ""
        Public Hue As Integer = 210
        Public Sat As Integer = 85
        Public LightAdjust As Integer = 0
        Public TopbarDelta As Integer = 0
    End Class

    ''' <summary>
    ''' 解析设置里的配色方案列表。任何一格损坏都跳过，不让整份配置失效。
    ''' </summary>
    Private Function LoadColorProfiles() As List(Of ColorProfile)
        Dim Result As New List(Of ColorProfile)
        Try
            Dim Raw As String = Settings.Get(Of String)("UiLauncherColorProfiles")
            If String.IsNullOrWhiteSpace(Raw) Then Return Result
            For Each Chunk As String In Raw.Split("|"c)
                If Chunk.Length = 0 Then Continue For
                Dim Parts As String() = Chunk.Split("~"c)
                If Parts.Length < 5 Then Continue For
                Dim Item As New ColorProfile With {
                    .Name = Uri.UnescapeDataString(Parts(0)),
                    .Hue = CInt(Val(Parts(1))),
                    .Sat = CInt(Val(Parts(2))),
                    .LightAdjust = CInt(Val(Parts(3))),
                    .TopbarDelta = CInt(Val(Parts(4)))
                }
                Result.Add(Item)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "解析自定义配色方案失败")
        End Try
        Return Result
    End Function

    ''' <summary>
    ''' 把配色方案列表写回设置。
    ''' </summary>
    Private Sub SaveColorProfiles(Profiles As List(Of ColorProfile))
        Try
            Dim Parts As New List(Of String)
            For Each Item As ColorProfile In Profiles
                Parts.Add(String.Join("~"c, {
                    Uri.EscapeDataString(If(Item.Name, "")),
                    Item.Hue.ToString(),
                    Item.Sat.ToString(),
                    Item.LightAdjust.ToString(),
                    Item.TopbarDelta.ToString()}))
            Next
            Settings.Set("UiLauncherColorProfiles", String.Join("|"c, Parts.ToArray()))
        Catch ex As Exception
            Logger.Error(ex, "保存自定义配色方案失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>
    ''' 已保存的配色方案数量。
    ''' </summary>
    Friend Function ColorProfileCount() As Integer
        Return LoadColorProfiles().Count
    End Function

    ''' <summary>
    ''' 取指定下标方案的显示名称与配色参数，越界返回 Nothing。
    ''' </summary>
    Friend Function ColorProfileGet(Index As Integer, ByRef Name As String, ByRef Hue As Integer,
                                    ByRef Sat As Integer, ByRef LightAdjust As Integer, ByRef TopbarDelta As Integer) As Boolean
        Dim Profiles As List(Of ColorProfile) = LoadColorProfiles()
        If Index < 0 OrElse Index >= Profiles.Count Then Return False
        Dim Item As ColorProfile = Profiles(Index)
        Name = Item.Name
        Hue = Item.Hue
        Sat = Item.Sat
        LightAdjust = Item.LightAdjust
        TopbarDelta = Item.TopbarDelta
        Return True
    End Function

    ''' <summary>
    ''' 追加一套配色方案，返回它的下标。名称留空时自动编号。
    ''' </summary>
    Friend Function ColorProfileAdd(Name As String, Hue As Integer, Sat As Integer,
                                    LightAdjust As Integer, TopbarDelta As Integer) As Integer
        Dim Profiles As List(Of ColorProfile) = LoadColorProfiles()
        If String.IsNullOrWhiteSpace(Name) Then Name = "配色 " & (Profiles.Count + 1)
        Profiles.Add(New ColorProfile With {
            .Name = Name,
            .Hue = Hue,
            .Sat = Sat,
            .LightAdjust = LightAdjust,
            .TopbarDelta = TopbarDelta})
        SaveColorProfiles(Profiles)
        Return Profiles.Count - 1
    End Function

    ''' <summary>
    ''' 覆盖保存指定下标的配色方案。
    ''' </summary>
    Friend Sub ColorProfileUpdate(Index As Integer, Hue As Integer, Sat As Integer,
                                  LightAdjust As Integer, TopbarDelta As Integer)
        Dim Profiles As List(Of ColorProfile) = LoadColorProfiles()
        If Index < 0 OrElse Index >= Profiles.Count Then Return
        Profiles(Index).Hue = Hue
        Profiles(Index).Sat = Sat
        Profiles(Index).LightAdjust = LightAdjust
        Profiles(Index).TopbarDelta = TopbarDelta
        SaveColorProfiles(Profiles)
    End Sub

    ''' <summary>
    ''' 删除指定下标的配色方案。删除的是最后一套时返回 True。
    ''' </summary>
    Friend Function ColorProfileRemove(Index As Integer) As Boolean
        Dim Profiles As List(Of ColorProfile) = LoadColorProfiles()
        If Index < 0 OrElse Index >= Profiles.Count Then Return False
        Profiles.RemoveAt(Index)
        SaveColorProfiles(Profiles)
        Return Profiles.Count = 0
    End Function

#End Region

#Region "取色与自动优化"

    ''' <summary>
    ''' 把 0~255 的 RGB 换成 HSL。
    ''' Hue 0~360；Sat / Light 0~1。灰度色的 Hue 记作 0。
    ''' </summary>
    Public Sub RgbToHsl(R As Integer, G As Integer, B As Integer,
                        ByRef Hue As Double, ByRef Sat As Double, ByRef Light As Double)
        Dim Rd As Double = R / 255.0
        Dim Gd As Double = G / 255.0
        Dim Bd As Double = B / 255.0
        Dim MaxV As Double = Math.Max(Rd, Math.Max(Gd, Bd))
        Dim MinV As Double = Math.Min(Rd, Math.Min(Gd, Bd))
        Light = (MaxV + MinV) / 2
        Dim Delta As Double = MaxV - MinV
        If Delta < 0.00001 Then
            Hue = 0
            Sat = 0
            Return
        End If
        If Light > 0.5 Then
            Sat = Delta / (2 - MaxV - MinV)
        Else
            Sat = Delta / (MaxV + MinV)
        End If
        If MaxV = Rd Then
            Hue = (Gd - Bd) / Delta
            If Gd < Bd Then Hue += 6
        ElseIf MaxV = Gd Then
            Hue = (Bd - Rd) / Delta + 2
        Else
            Hue = (Rd - Gd) / Delta + 4
        End If
        Hue *= 60
    End Sub

    ''' <summary>
    ''' 把任意颜色换算成能直接喂给 PCL 主题生成器的色相 / 饱和度。
    '''
    ''' PCL 的主题配色天然是「一个色相 + 一个饱和度 + 一串固定亮度」，
    ''' 所以取到一个颜色后只需要定出这两个值再加上亮度微调，就能得到一整套可用配色。
    ''' 饱和度会按 PCL 曲线的比例回推（主题色用的是 0.85 倍饱和度），并对灰阶做保底提升，
    ''' 避免吸到接近灰白的颜色时整套主题变成无色。
    ''' </summary>
    Public Sub ColorToThemeParams(R As Integer, G As Integer, B As Integer,
                                  ByRef Hue As Integer, ByRef Sat As Integer)
        Dim H As Double, S As Double, L As Double
        RgbToHsl(R, G, B, H, S, L)
        Hue = CInt(Math.Round(H)) Mod 360
        If Hue < 0 Then Hue += 360
        '按 PCL 曲线回推：Color3 = FromHSL2(H, Sat, 55)，实际呈现饱和度约为 Sat 的 0.85 倍
        Dim RawSat As Double = S * 100 / 0.85
        '纯灰 / 接近灰的颜色没有可用色相，给一点默认饱和度，至少别是纯灰
        If S < 0.06 Then RawSat = 18
        Sat = CInt(Math.Round(RawSat.Clamp(10, 100)))
    End Sub

    ''' <summary>
    ''' 一套配色的对比度体检结果。
    ''' </summary>
    Public Class ContrastReport
        Public MainText As Double = 0      '正文在页面背景上的对比度
        Public SecondaryText As Double = 0 '次要文字在页面背景上的对比度
        Public AccentOnBg As Double = 0    '强调色在页面背景上的对比度
        Public IsMainTextOk As Boolean = False
        Public IsSecondaryTextOk As Boolean = False
        Public IsAccentOk As Boolean = False
    End Class

    ''' <summary>
    ''' 计算 WCAG 相对亮度。
    ''' </summary>
    Private Function RelativeLuminance(Col As MyColor) As Double
        Dim Rs As Double = Col.R / 255.0
        Dim Gs As Double = Col.G / 255.0
        Dim Bs As Double = Col.B / 255.0
        If Rs <= 0.03928 Then Rs = Rs / 12.92 Else Rs = Math.Pow((Rs + 0.055) / 1.055, 2.4)
        If Gs <= 0.03928 Then Gs = Gs / 12.92 Else Gs = Math.Pow((Gs + 0.055) / 1.055, 2.4)
        If Bs <= 0.03928 Then Bs = Bs / 12.92 Else Bs = Math.Pow((Bs + 0.055) / 1.055, 2.4)
        Return 0.2126 * Rs + 0.7152 * Gs + 0.0722 * Bs
    End Function

    ''' <summary>
    ''' WCAG 对比度，范围 1 ~ 21。正文建议 >= 4.5，大字与次要文字建议 >= 3。
    ''' </summary>
    Public Function ContrastRatio(A As MyColor, B As MyColor) As Double
        Dim La As Double = RelativeLuminance(A)
        Dim Lb As Double = RelativeLuminance(B)
        Dim Hi As Double = Math.Max(La, Lb)
        Dim Lo As Double = Math.Min(La, Lb)
        Return (Hi + 0.05) / (Lo + 0.05)
    End Function

    ''' <summary>
    ''' 对指定配色做一次对比度体检（只做计算，不改变当前主题）。
    ''' </summary>
    Public Function CheckContrast(Hue As Integer, Sat As Integer, LightAdjust As Integer) As ContrastReport
        Dim Report As New ContrastReport
        Try
            Dim Bg As MyColor = New MyColor().FromHSL2(Hue, Sat, 97)
            Dim MainText As New MyColor(64, 64, 64)
            Dim SecondaryText As New MyColor(115, 115, 115)
            Dim Accent As MyColor = New MyColor().FromHSL2(Hue, Sat, 55 + LightAdjust)
            Report.MainText = ContrastRatio(MainText, Bg)
            Report.SecondaryText = ContrastRatio(SecondaryText, Bg)
            Report.AccentOnBg = ContrastRatio(Accent, Bg)
            Report.IsMainTextOk = Report.MainText >= 4.5
            Report.IsSecondaryTextOk = Report.SecondaryText >= 3.0
            Report.IsAccentOk = Report.AccentOnBg >= 3.0
        Catch ex As Exception
            Logger.Warn(ex, "对比度体检失败")
        End Try
        Return Report
    End Function

    ''' <summary>
    ''' 自动优化：在亮度微调的可用范围内挑一个让明暗对比更舒服、
    ''' 同时尽量贴近你原本倾向的值。返回值即建议的亮度微调。
    ''' </summary>
    Public Function AutoOptimizeLightAdjust(Hue As Integer, Sat As Integer, CurrentLightAdjust As Integer,
                                            ByRef Improved As Boolean) As Integer
        Improved = False
        Try
            Dim BestAdjust As Integer = CurrentLightAdjust
            Dim BestScore As Double = -1
            For Adjust As Integer = -20 To 20
                Dim Bg As MyColor = New MyColor().FromHSL2(Hue, Sat, 97)
                Dim Accent As MyColor = New MyColor().FromHSL2(Hue, Sat, 55 + Adjust)
                Dim AccentRatio As Double = ContrastRatio(Accent, Bg)
                '越接近原值越优先，避免为了对比度把颜色推得面目全非
                Dim Score As Double = Math.Min(AccentRatio, 7.0) - Math.Abs(Adjust - CurrentLightAdjust) * 0.12
                If Score > BestScore Then
                    BestScore = Score
                    BestAdjust = Adjust
                End If
            Next
            Improved = BestAdjust <> CurrentLightAdjust
            Return BestAdjust
        Catch ex As Exception
            Logger.Warn(ex, "自动优化亮度失败")
            Return CurrentLightAdjust
        End Try
    End Function

    ''' <summary>最近一次取色 / 提取主色的结果，0~255。</summary>
    Public DominantR As Integer = 0
    Public DominantG As Integer = 0
    Public DominantB As Integer = 0

    ''' <summary>
    ''' 从图片里提取最能代表它的主色，结果放进 DominantR / G / B。
    ''' 做法：把图片缩到小尺寸做颜色分桶，再取占比最高的桶的平均色；
    ''' 跳过过暗、过亮、以及接近灰的像素，避免主色被大片背景或阴影带偏。
    ''' </summary>
    Public Function ExtractDominantColor(ImagePath As String) As Boolean
        Try
            Using Img As Draw.Bitmap = New Draw.Bitmap(ImagePath)
                Using SmallImg As New Draw.Bitmap(48, 48)
                    Using g As Draw.Graphics = Draw.Graphics.FromImage(SmallImg)
                        g.InterpolationMode = Draw.Drawing2D.InterpolationMode.HighQualityBilinear
                        g.DrawImage(Img, 0, 0, 48, 48)
                    End Using
                    Dim Buckets(63) As Integer
                    Dim SumR(63) As Integer
                    Dim SumG(63) As Integer
                    Dim SumB(63) As Integer
                    For x As Integer = 0 To 47
                        For y As Integer = 0 To 47
                            Dim Px As Draw.Color = SmallImg.GetPixel(x, y)
                            Dim H As Double, S As Double, L As Double
                            RgbToHsl(Px.R, Px.G, Px.B, H, S, L)
                            If L < 0.12 OrElse L > 0.92 Then Continue For
                            If S < 0.15 Then Continue For
                            Dim Idx As Integer = (Px.R \ 64) * 16 + (Px.G \ 64) * 4 + (Px.B \ 64)
                            Buckets(Idx) += 1
                            SumR(Idx) += Px.R
                            SumG(Idx) += Px.G
                            SumB(Idx) += Px.B
                        Next
                    Next
                    Dim BestIdx As Integer = -1
                    Dim BestCount As Integer = 0
                    For i As Integer = 0 To 63
                        If Buckets(i) > BestCount Then
                            BestCount = Buckets(i)
                            BestIdx = i
                        End If
                    Next
                    If BestIdx < 0 OrElse BestCount = 0 Then
                        '整张图都是灰阶或极端明暗，退化为取平均色
                        Dim RSum As Long = 0, GSum As Long = 0, BSum As Long = 0
                        For x As Integer = 0 To 47
                            For y As Integer = 0 To 47
                                Dim Px As Draw.Color = SmallImg.GetPixel(x, y)
                                RSum += Px.R
                                GSum += Px.G
                                BSum += Px.B
                            Next
                        Next
                        DominantR = CInt(RSum / 2304)
                        DominantG = CInt(GSum / 2304)
                        DominantB = CInt(BSum / 2304)
                        Return True
                    End If
                    DominantR = CInt(SumR(BestIdx) / BestCount)
                    DominantG = CInt(SumG(BestIdx) / BestCount)
                    DominantB = CInt(SumB(BestIdx) / BestCount)
                    Return True
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "提取图片主色失败")
            Return False
        End Try
    End Function

#End Region

#Region "更新"

    Friend Sub UpdateCheckByButton()
        Hint("该版本中不包含更新功能……")
    End Sub

    Friend IsUpdateWaitingRestart As Boolean = False
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新", Sub() Logger.Info("该版本中不包含更新通知功能……"), Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "赞助等级"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    Public Sub InputPotatoCode(IsUpdating As Boolean)
    End Sub
    Friend Sub GeneratePotatoCode()
    End Sub

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
