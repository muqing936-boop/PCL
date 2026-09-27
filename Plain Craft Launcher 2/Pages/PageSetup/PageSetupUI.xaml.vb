Public Class PageSetupUI

    Private Sub PageSetupUI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
        ThemeCheckAll(True)
        If ThemeDontClick <> 0 Then
            Dim NewText As String = Nothing
            Select Case ThemeDontClick
                Case 1
                    NewText = "眼瞎白"
                Case 2
                    NewText = "真·滑稽彩"
            End Select
            For Each Control In PanLauncherTheme.Children
                If (TypeOf Control Is MyRadioBox) AndAlso CType(Control, MyRadioBox).IsEnabled Then CType(Control, MyRadioBox).Text = NewText
            Next
        End If

        AniControlEnabled += 1
        Refresh() '#4826
        AniControlEnabled -= 1

        '主题切换后由 ModSecret.ThemeRefresh 回调过来，把新主题的配色回填到四个配色滑条。
        '不这样做的话，滑条会停留在上一次自定义的取值上，用户一动它就跳回「自定义」。
        ThemeChangedHook = Sub(Theme As Integer) SyncPresetSlidersToTheme(Theme)
        SyncThemeSliders()

        '自定义配色方案：恢复上次选中的那一套
        ProfileRefresh()
        '如果当前就是「自定义」主题，把选中的配色重新套一遍，
        '否则滑条会被 RefreshSettings 重置成默认值，和界面显示的颜色对不上
        If Settings.Get(Of Integer)("UiLauncherTheme") = 14 AndAlso _ProfileIndex >= 0 Then ProfileApply(_ProfileIndex)

        '非重复加载部分
        Static Reloaded As Boolean = False
        If Reloaded Then Return
        Reloaded = True

        SliderLoad()

        '开源版没有隐藏主题与解锁门槛，所有主题都可以直接选择，SliderLoad 之后无需再处理主题解锁
        '正式版构建仍然显示赞助提示，但它已改为独立卡片（CardLauncherHide），不会再遮挡主题网格
        If BuildType = BuildTypes.Release Then CardLauncherHide.Visibility = Visibility.Visible

    End Sub
    Public Sub Refresh()
        Try
            SettingService.RefreshSettings(Me)
            BackgroundRefresh(False, False)
            BackgroundSizeRefresh()
            BackgroundPreviewRefresh()

            '标题栏
            CheckLogoLeft.Visibility = If(RadioLogoType0.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoText.Visibility = If(RadioLogoType2.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoChange.Visibility = If(RadioLogoType3.Checked, Visibility.Visible, Visibility.Collapsed)

            '背景音乐
            MusicRefreshUI()

            '主页
            OnMainPageTypeChanged()
        Catch ex As NullReferenceException
            Logger.Error(ex, "个性化设置项存在异常，已被自动重置", LogBehavior.Alert)
            Reset()
        Catch ex As Exception
            Logger.Error(ex, "重载个性化设置时出错")
        End Try
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化个性化设置！")
            Hint("已初始化个性化设置", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化个性化设置失败", LogBehavior.Alert)
        End Try
        Refresh()
    End Sub

    '背景图片
    Private Sub BtnUIBgOpen_Click(sender As Object, e As EventArgs) Handles BtnBackgroundOpen.Click
        OpenExplorer(Paths.Base & "PCL\Pictures\")
    End Sub
    Private Sub BtnBackgroundRefresh_Click(sender As Object, e As EventArgs) Handles BtnBackgroundRefresh.Click
        BackgroundRefresh(True, True)
    End Sub
    Public Sub BackgroundRefreshUI(Show As Boolean, Count As Integer)
        If IsNothing(PanBackgroundOpacity) Then Return
        If Show Then
            PanBackgroundOpacity.Visibility = Visibility.Visible
            PanBackgroundBlur.Visibility = Visibility.Visible
            PanBackgroundSuit.Visibility = Visibility.Visible
            PanBackgroundSize.Visibility = Visibility.Visible
            BtnBackgroundClear.Visibility = Visibility.Visible
            CardBackground.Title = "背景图片（" & Count & " 张）"
        Else
            PanBackgroundOpacity.Visibility = Visibility.Collapsed
            PanBackgroundBlur.Visibility = Visibility.Collapsed
            PanBackgroundSuit.Visibility = Visibility.Collapsed
            PanBackgroundSize.Visibility = Visibility.Collapsed
            BtnBackgroundClear.Visibility = Visibility.Collapsed
            CardBackground.Title = "背景图片"
        End If
        CardBackground.TriggerForceResize()
    End Sub
    Private Sub BtnBackgroundClear_Click(sender As Object, e As EventArgs) Handles BtnBackgroundClear.Click
        If MyMsgBox("即将删除背景图片文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            DirectoryUtils.Delete(Paths.Base & "PCL\Pictures")
            BackgroundRefresh(False, True)
            Hint("背景图片已清空！", HintType.Green)
        End If
    End Sub
    ''' <summary>
    ''' 按设置刷新预览模式：收起全部设置卡片（含「背景图片」自己）以及顶栏、左侧导航，
    ''' 只留 FrmMain 上那条浮动调整条，于是能完整看到整张背景，又能边拖滑条边看效果。
    ''' 写成 Shared 是为了能直接挂到 Settings 的 OnChanged 上。
    ''' </summary>
    Public Shared Sub BackgroundPreviewRefresh()
        Try
            Dim Page As PageSetupUI = FrmSetupUI
            If Page Is Nothing OrElse Page.CardBackground Is Nothing Then Return
            Dim IsPreview As Boolean = Settings.Get(Of Boolean)("UiBackgroundPreview")
            '预览时全部卡片都收起 —— 包括「背景图片」卡片本身，否则它会挡住背景
            Dim AllCards As New List(Of FrameworkElement) From {
                Page.CardBackground, Page.CardLauncher, Page.CardColorProfile,
                Page.CardMusic, Page.CardLogo, Page.CardCustom, Page.CardSwitch}
            For Each Card As FrameworkElement In AllCards
                If Card IsNot Nothing Then
                    Card.Visibility = If(IsPreview, Visibility.Collapsed, Visibility.Visible)
                End If
            Next
            'CardLauncherHide 平时由 BuildType 控制，这里只在预览时强制收起
            If Page.CardLauncherHide IsNot Nothing AndAlso IsPreview Then Page.CardLauncherHide.Visibility = Visibility.Collapsed
            '顶栏与左侧导航也收起来，才是干净的整窗口背景
            If FrmMain IsNot Nothing Then
                FrmMain.PanTitle.Visibility = If(IsPreview, Visibility.Collapsed, Visibility.Visible)
                FrmMain.PanLeft.Visibility = If(IsPreview, Visibility.Collapsed, Visibility.Visible)
                FrmMain.PanHint.Visibility = If(IsPreview, Visibility.Collapsed, Visibility.Visible)
                '浮动调整条只在预览且停在「个性化」页时显示
                FrmMain.RefreshBackgroundPreviewPanel()
            End If
            '退出预览后，设置页卡片重新出现，需要重量一次高度
            If Not IsPreview Then Page.CardBackground.TriggerForceResize()
        Catch ex As Exception
            Logger.Error(ex, "刷新背景预览模式失败")
        End Try
    End Sub

    Private Sub CheckBackgroundPreview_Change() Handles CheckBackgroundPreview.Change
        BackgroundPreviewRefresh()
    End Sub

    Private Sub BtnBackgroundSizeReset_Click(sender As Object, e As EventArgs) Handles BtnBackgroundSizeReset.Click
        Try
            AniControlEnabled += 1
            Try
                SliderBackgroundScale.Value = 100
                SliderBackgroundScaleW.Value = 100
                SliderBackgroundScaleH.Value = 100
                SliderBackgroundOffsetX.Value = 500 '位移滑条映射范围 0~1000，500 代表位移 0
                SliderBackgroundOffsetY.Value = 500
            Finally
                AniControlEnabled -= 1
            End Try
            Settings.Set("UiBackgroundScale", 100)
            Settings.Set("UiBackgroundScaleW", 100)
            Settings.Set("UiBackgroundScaleH", 100)
            Settings.Set("UiBackgroundOffsetX", 0)
            Settings.Set("UiBackgroundOffsetY", 0)
            Hint("已重置背景图片尺寸", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "重置背景图片尺寸失败")
        End Try
    End Sub
    Private Sub CheckBackgroundAspectLock_Change() Handles CheckBackgroundAspectLock.Change
        BackgroundSizeRefresh()
    End Sub
    Private Sub SliderBackgroundOffset_Change() Handles SliderBackgroundOffsetX.Change, SliderBackgroundOffsetY.Change
        If AniControlEnabled <> 0 Then Return
        Try
            '位移滑条没有绑定 SettingService.Key（需要把 0~1000 映射为 -500~500 像素），因此在这里手动保存
            Settings.Set("UiBackgroundOffsetX", SliderBackgroundOffsetX.Value - 500)
            Settings.Set("UiBackgroundOffsetY", SliderBackgroundOffsetY.Value - 500)
        Catch ex As Exception
            Logger.Error(ex, "保存背景图片位移设置失败")
        End Try
    End Sub
    ''' <summary>
    ''' 刷新背景图片尺寸设置的界面状态：位移滑条的映射值与宽高比例的显示。
    ''' </summary>
    Private Sub BackgroundSizeRefresh()
        Try
            '位移滑条的可见范围是 0~1000，实际位移 = 滑条值 - 500（MySlider 没有 MinValue，无法直接使用负数范围）
            AniControlEnabled += 1
            Try
                SliderBackgroundOffsetX.Value = (Settings.Get(Of Integer)("UiBackgroundOffsetX") + 500).Clamp(0, 1000)
                SliderBackgroundOffsetY.Value = (Settings.Get(Of Integer)("UiBackgroundOffsetY") + 500).Clamp(0, 1000)
            Finally
                AniControlEnabled -= 1
            End Try
            '锁定宽高比例时隐藏宽高独立设置
            Dim IsLock As Boolean = CheckBackgroundAspectLock.Checked
            PanBackgroundScaleW.Visibility = If(IsLock, Visibility.Collapsed, Visibility.Visible)
            PanBackgroundScaleH.Visibility = If(IsLock, Visibility.Collapsed, Visibility.Visible)
            CardBackground.TriggerForceResize()
        Catch ex As Exception
            Logger.Error(ex, "刷新背景图片尺寸设置失败")
        End Try
    End Sub
    ''' <summary>
    ''' 刷新背景图片及设置页 UI。
    ''' </summary>
    ''' <param name="IsHint">是否显示刷新提示。</param>
    ''' <param name="Refresh">是否刷新图片显示。</param>
    Public Shared Sub BackgroundRefresh(IsHint As Boolean, Refresh As Boolean)
        Try

            '获取可用的图片文件
            DirectoryUtils.Create(Paths.Base & "PCL\Pictures\")
            Dim Pic As New List(Of String)
            For Each File In DirectoryUtils.EnumerateFiles(Paths.Base & "PCL\Pictures\", True)
                Dim Extension As String = PathUtils.GetExtension(File)
                If Extension <> "ini" AndAlso Extension <> "db" Then Pic.Add(File) '文件夹可能会被加入 .ini 和 thumbs.db
            Next
            '加载
            If Not Pic.Any() Then
                If Refresh Then
                    If FrmMain.ImgBack.Visibility = Visibility.Collapsed Then
                        If IsHint Then Hint("未检测到可用背景图片！", HintType.Red)
                    Else
                        FrmMain.ImgBack.Visibility = Visibility.Collapsed
                        If IsHint Then Hint("背景图片已清除！", HintType.Green)
                    End If
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(False, 0)
            Else
                If Refresh Then
                    Dim Address As String = RandomOne(Pic)
                    Try
                        Logger.Info($"加载背景图片：{Address}")
                        FrmMain.ImgBack.Background = New MyBitmap(Address)
                        FrmMain.ImgBack.Visibility = Visibility.Visible
                        FrmMain.UpdateBackgroundAndTitleBar()
                        If IsHint Then Hint("背景图片已刷新：" & PathUtils.GetLastPart(Address), HintType.Green, False)
                    Catch ex As Exception
                        If ex.Message.Contains("参数无效") Then
                            Logger.Error($"刷新背景图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{Address}", LogBehavior.Alert)
                        Else
                            Logger.Error(ex, $"刷新背景图片失败（{Address}）", LogBehavior.Alert)
                        End If
                    End Try
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(True, Pic.Count)
            End If

        Catch ex As Exception
            Logger.Error(ex, "刷新背景图片时出现未知错误")
        End Try
    End Sub

    '顶部栏
    Private Sub BtnLogoChange_Click(sender As Object, e As EventArgs) Handles BtnLogoChange.Click
        Dim FileName As String = Dialogs.SelectFile("选择图片", False, filter:={({"png", "jpg", "jpeg", "gif", "webp"}, "常用图片文件")}).FirstOrDefault()
        If String.IsNullOrEmpty(FileName) Then Return
        Dim TargetPath As String = Paths.Base & "PCL\Logo.png"
        Try
            '复制文件
            FileUtils.Copy(FileName, TargetPath)
            '设置当前显示
            FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
            FrmMain.ImageTitleLogo.Source = TargetPath
        Catch ex As Exception
            If ex.Message.Contains("参数无效") Then
                Logger.Error($"改变标题栏图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{TargetPath}", LogBehavior.Alert)
            Else
                Logger.Error(ex, "设置标题栏图片失败", LogBehavior.Alert)
            End If
            FrmMain.ImageTitleLogo.Source = Nothing
        End Try
    End Sub
    Private Sub RadioLogoType3_Check(sender As Object, e As RouteEventArgs) Handles RadioLogoType3.PreviewCheck
        If Not (AniControlEnabled = 0 AndAlso e.RaiseByMouse) Then Return
Refresh:
        '已有图片则不再选择
        Dim TargetPath As String = Paths.Base & "PCL\Logo.png"
        If FileUtils.Exists(TargetPath) Then
            Try
                FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
                FrmMain.ImageTitleLogo.Source = TargetPath
            Catch ex As Exception
                If ex.Message.Contains("参数无效") Then
                    Logger.Error($"改变标题栏图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{TargetPath}", LogBehavior.Alert)
                Else
                    Logger.Error(ex, "调整标题栏图片失败", LogBehavior.Alert)
                End If
                FrmMain.ImageTitleLogo.Source = Nothing
                e.Handled = True
                Try
                    FileUtils.Delete(TargetPath)
                Catch exx As Exception
                    Logger.Error(exx, "清理错误的标题栏图片失败", LogBehavior.Alert)
                End Try
            End Try
            Return
        End If
        '没有图片则要求选择
        Dim FileName As String = Dialogs.SelectFile("选择图片", False, filter:={({"png", "jpeg", "jpg", "gif", "webp"}, "常用图片文件")}).FirstOrDefault()
        If String.IsNullOrEmpty(FileName) Then
            FrmMain.ImageTitleLogo.Source = Nothing
            e.Handled = True
        Else
            Try
                FileUtils.Copy(FileName, TargetPath)
                GoTo Refresh
            Catch ex As Exception
                Logger.Error(ex, "复制标题栏图片失败", LogBehavior.Alert)
            End Try
        End If
    End Sub
    Private Sub BtnLogoDelete_Click(sender As Object, e As EventArgs) Handles BtnLogoDelete.Click
        Try
            FileUtils.Delete(Paths.Base & "PCL\Logo.png")
            RadioLogoType1.SetChecked(True, True)
            Hint("标题栏图片已清空！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "清空标题栏图片失败", LogBehavior.Alert)
        End Try
    End Sub

    '背景音乐
    Private Sub BtnMusicOpen_Click(sender As Object, e As EventArgs) Handles BtnMusicOpen.Click
        OpenExplorer(Paths.Base & "PCL\Musics\")
    End Sub
    Private Sub BtnMusicRefresh_Click(sender As Object, e As EventArgs) Handles BtnMusicRefresh.Click
        MusicRefreshPlay(True)
    End Sub
    Public Sub MusicRefreshUI()
        If PanBackgroundOpacity Is Nothing Then Return
        If MusicAllList.Any Then
            PanMusicVolume.Visibility = Visibility.Visible
            PanMusicDetail.Visibility = Visibility.Visible
            BtnMusicClear.Visibility = Visibility.Visible
            CardMusic.Title = "背景音乐（" &
                DirectoryUtils.EnumerateFiles(Paths.Base & "PCL\Musics\", True).Count(Function(f) Not {"ini", "jpg", "txt", "cfg", "lrc", "db", "png"}.Contains(PathUtils.GetExtension(f))) &
                " 首）"
        Else
            PanMusicVolume.Visibility = Visibility.Collapsed
            PanMusicDetail.Visibility = Visibility.Collapsed
            BtnMusicClear.Visibility = Visibility.Collapsed
            CardMusic.Title = "背景音乐"
        End If
        CardMusic.TriggerForceResize()
    End Sub
    Private Sub BtnMusicClear_Click(sender As Object, e As EventArgs) Handles BtnMusicClear.Click
        If MyMsgBox("即将删除背景音乐文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            RunInThread(
            Sub()
                Hint("正在删除背景音乐……")
                '停止播放音乐
                MusicNAudio = Nothing
                MusicWaitingList = New List(Of String)
                MusicAllList = New List(Of String)
                Thread.Sleep(200)
                '删除文件
                Try
                    DirectoryUtils.Delete(Paths.Base & "PCL\Musics")
                    Hint("背景音乐已删除！", HintType.Green)
                Catch ex As Exception
                    Logger.Error(ex, "删除背景音乐失败", LogBehavior.Alert)
                End Try
                Try
                    DirectoryUtils.Create(Paths.Base & "PCL\Musics\")
                    RunInUi(Sub() MusicRefreshPlay(False))
                Catch ex As Exception
                    Logger.Error(ex, "重建背景音乐文件夹失败", LogBehavior.Alert)
                End Try
            End Sub)
        End If
    End Sub
    Private Sub CheckMusicStart_Change() Handles CheckMusicStart.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStart.Checked Then CheckMusicStop.Checked = False
    End Sub
    Private Sub CheckMusicStop_Change() Handles CheckMusicStop.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStop.Checked Then CheckMusicStart.Checked = False
    End Sub

    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If FileUtils.Exists(Paths.Base & "PCL\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            ExtractResources(Paths.Base & "PCL\Custom.xaml", "Custom")
            Hint("教学文件已生成！", HintType.Green)
            OpenExplorer(Paths.Base & "PCL\Custom.xaml")
        Catch ex As Exception
            Logger.Error(ex, "生成教学文件失败")
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint("已刷新主页！", HintType.Green)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" & vbCrLf &
                 "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" & vbCrLf &
                 "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" & vbCrLf &
                 vbCrLf &
                 "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" & vbCrLf &
                 "直接将主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程")
    End Sub
    Private Sub BtnCustomOpen_Click(sender As Object, e As EventArgs) Handles BtnCustomOpen.Click
        Try
            Dim CustomPath As String = Paths.Base & "PCL\Custom.xaml"
            If FileUtils.Exists(CustomPath) Then
                OpenExplorer(CustomPath)
            Else
                OpenExplorer(Paths.Base & "PCL\")
                Hint("未找到 Custom.xaml，已打开 PCL 文件夹。", HintType.Blue)
            End If
        Catch ex As Exception
            Logger.Error(ex, "打开主页文件失败")
        End Try
    End Sub
    Public Shared Sub OnMainPageTypeChanged()
        If FrmSetupUI Is Nothing Then Return
        Select Case CInt(Settings.Get(Of Integer)("UiCustomType"))
            Case 0 '无
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Settings.Get(Of Boolean)("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                CustomEventService.SetEventType(FrmSetupUI.HintCustom, CustomEvent.EventType.None)
            Case 2 '联网
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Settings.Get(Of Boolean)("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！"
                CustomEventService.SetEventType(FrmSetupUI.HintCustom, CustomEvent.EventType.打开网页)
                CustomEventService.SetEventData(FrmSetupUI.HintCustom, "https://github.com/Meloong-Git/PCL/discussions/2528")
            Case 3 '预设
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
        End Select
        FrmSetupUI.CardCustom.TriggerForceResize()
    End Sub

    '主题

    '主题自定义
    Private Sub RadioLauncherTheme14_Change(sender As Object, e As RouteEventArgs) Handles RadioLauncherTheme14.Changed
        '四个配色滑条已改为常显，这里只需要刷新卡片高度
        CardLauncher.TriggerForceResize()
    End Sub

    ''' <summary>
    ''' 把当前选中主题的配色同步到四个配色滑条上（仅在该主题是预置主题时生效）。
    ''' </summary>
    Private Sub SyncThemeSliders()
        SyncPresetSlidersToTheme(Settings.Get(Of Integer)("UiLauncherTheme"))
    End Sub

    ''' <summary>
    ''' 按指定的预置主题刷新滑条显示。主题为「自定义」或越界时不做任何事，
    ''' 以免把用户正在调的颜色覆盖掉。
    ''' 滑条值到主题参数的换算必须与 ModSecret.ThemeLoadPreset 严格互逆：
    '''   Hue = 滑条值
    '''   Sat = 滑条值
    '''   LightAdjust = 滑条值 - 20
    '''   TopbarDelta = (滑条值 - 90) * 2
    ''' </summary>
    Public Sub SyncPresetSlidersToTheme(Theme As Integer)
        Try
            If SliderLauncherHue Is Nothing OrElse Not SliderLauncherHue.IsLoaded Then Return
            Dim Hue As Integer, Sat As Integer, LightAdjust As Integer, TopbarDelta As Integer
            If Not ThemeGetPreset(Theme, Hue, Sat, LightAdjust, TopbarDelta) Then Return
            '屏蔽本次回填产生的 Change 事件，避免被误判成用户拖动而切回「自定义」
            SyncingThemeSliders = True
            AniControlEnabled += 1
            Try
                SliderLauncherHue.Value = Hue.Clamp(0, 360)
                SliderLauncherSat.Value = Sat.Clamp(0, 100)
                SliderLauncherLight.Value = (LightAdjust + 20).Clamp(0, 40)
                Dim SliderDelta As Integer = CInt(TopbarDelta / 2) + 90
                SliderLauncherDelta.Value = SliderDelta.Clamp(0, 180)
            Finally
                AniControlEnabled -= 1
                SyncingThemeSliders = False
            End Try
            '切到预置主题了，说明当前界面颜色已不是那套配色方案，取消选中标记
            If Not _ProfileApplying Then
                _ProfileIndex = -1
                Settings.Set("UiLauncherColorProfileIndex", -1)
                ProfileRefreshUI()
            End If
        Catch ex As Exception
            Logger.Error(ex, "同步主题配色到滑条失败")
        End Try
    End Sub

    Private Sub HSL_Change() Handles SliderLauncherHue.Change, SliderLauncherLight.Change, SliderLauncherSat.Change, SliderLauncherDelta.Change
        If AniControlEnabled <> 0 OrElse SliderLauncherSat Is Nothing OrElse Not SliderLauncherSat.IsLoaded Then Return
        '正在把预置主题回填到滑条，这不是用户的调整，不要切主题
        If SyncingThemeSliders Then Return
        Try
            '先手动写入设置：Handles 的触发顺序不保证，避免主题刷新时读到滑条的旧值
            SettingService.SaveSetting(SliderLauncherHue)
            SettingService.SaveSetting(SliderLauncherSat)
            SettingService.SaveSetting(SliderLauncherDelta)
            SettingService.SaveSetting(SliderLauncherLight)
            '用户拖动滑条才视为微调，切换到「自定义」；
            '已经是自定义主题时不要重复赋值，否则会打断正在进行的拖动
            If Settings.Get(Of Integer)("UiLauncherTheme") <> 14 Then RadioLauncherTheme14.Checked = True
            ThemeRefresh()
        Catch ex As Exception
            Logger.Error(ex, "应用自定义主题颜色失败")
        End Try
    End Sub

#Region "自定义配色方案"

    ''' <summary>
    ''' 当前选中的配色方案下标，-1 表示没有（还没保存过任何方案）。
    ''' </summary>
    Private _ProfileIndex As Integer = -1
    ''' <summary>正在套用配色方案，此时滑条赋值不算用户微调。</summary>
    Private _ProfileApplying As Boolean = False

    ''' <summary>
    ''' 从设置读取当前选中的配色方案下标，并同步界面。页面每次加载都会走一遍。
    ''' </summary>
    Private Sub ProfileRefresh()
        Try
            Dim Count As Integer = ColorProfileCount()
            _ProfileIndex = Settings.Get(Of Integer)("UiLauncherColorProfileIndex")
            '方案被删光、或下标越界时归零
            If Count = 0 Then
                _ProfileIndex = -1
            ElseIf _ProfileIndex < 0 OrElse _ProfileIndex >= Count Then
                _ProfileIndex = 0
            End If
            Settings.Set("UiLauncherColorProfileIndex", _ProfileIndex)
            ProfileRefreshUI()
        Catch ex As Exception
            Logger.Error(ex, "刷新配色方案失败")
        End Try
    End Sub

    ''' <summary>
    ''' 只更新配色方案的界面文字与按钮可用状态。
    ''' </summary>
    Private Sub ProfileRefreshUI()
        Try
            If LabColorProfile Is Nothing Then Return
            Dim Count As Integer = ColorProfileCount()
            Dim HasProfile As Boolean = Count > 0 AndAlso _ProfileIndex >= 0 AndAlso _ProfileIndex < Count
            If HasProfile Then
                Dim Name As String = Nothing
                Dim Hue As Integer, Sat As Integer, LightAdjust As Integer, TopbarDelta As Integer
                ColorProfileGet(_ProfileIndex, Name, Hue, Sat, LightAdjust, TopbarDelta)
                LabColorProfile.Text = $"{Name}（第 {_ProfileIndex + 1} / {Count} 套）"
            Else
                LabColorProfile.Text = "还没有保存过配色方案"
            End If
            BtnProfilePrev.IsEnabled = HasProfile
            BtnProfileNext.IsEnabled = HasProfile
            BtnProfileApply.IsEnabled = HasProfile
            BtnProfileOverwrite.IsEnabled = HasProfile
            BtnProfileDelete.IsEnabled = HasProfile
        Catch ex As Exception
            Logger.Error(ex, "刷新配色方案界面失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把指定下标的配色方案套用到四个滑条上并立即生效。
    ''' </summary>
    Private Sub ProfileApply(Index As Integer)
        Dim Name As String = Nothing
        Dim Hue As Integer, Sat As Integer, LightAdjust As Integer, TopbarDelta As Integer
        If Not ColorProfileGet(Index, Name, Hue, Sat, LightAdjust, TopbarDelta) Then Return
        _ProfileApplying = True
        Try
            '先选中「自定义」主题，保证下面写入的颜色会被采用
            If Settings.Get(Of Integer)("UiLauncherTheme") <> 14 Then RadioLauncherTheme14.Checked = True
            '套用期间屏蔽 Change 事件，避免被当成用户拖动
            SyncingThemeSliders = True
            AniControlEnabled += 1
            Try
                SliderLauncherHue.Value = Hue.Clamp(0, 360)
                SliderLauncherSat.Value = Sat.Clamp(0, 100)
                SliderLauncherLight.Value = (LightAdjust + 20).Clamp(0, 40)
                SliderLauncherDelta.Value = (CInt(TopbarDelta / 2) + 90).Clamp(0, 180)
            Finally
                AniControlEnabled -= 1
                SyncingThemeSliders = False
            End Try
            '手动落盘：滑条被屏蔽期间不会自己写设置
            SettingService.SaveSetting(SliderLauncherHue)
            SettingService.SaveSetting(SliderLauncherSat)
            SettingService.SaveSetting(SliderLauncherDelta)
            SettingService.SaveSetting(SliderLauncherLight)
            ThemeRefresh()
        Finally
            _ProfileApplying = False
        End Try
    End Sub

    ''' <summary>
    ''' 切换配色方案：写设置 + 套用。
    ''' </summary>
    Private Sub ProfileSwitch(Delta As Integer)
        Try
            Dim Count As Integer = ColorProfileCount()
            If Count = 0 Then Return
            Dim NewIndex As Integer = _ProfileIndex + Delta
            If NewIndex < 0 Then NewIndex = Count - 1
            If NewIndex >= Count Then NewIndex = 0
            _ProfileIndex = NewIndex
            Settings.Set("UiLauncherColorProfileIndex", _ProfileIndex)
            ProfileRefreshUI()
            ProfileApply(_ProfileIndex)
        Catch ex As Exception
            Logger.Error(ex, "切换配色方案失败")
        End Try
    End Sub

    Private Sub BtnProfilePrev_Click(sender As Object, e As EventArgs) Handles BtnProfilePrev.Click
        ProfileSwitch(-1)
    End Sub
    Private Sub BtnProfileNext_Click(sender As Object, e As EventArgs) Handles BtnProfileNext.Click
        ProfileSwitch(1)
    End Sub
    Private Sub BtnProfileApply_Click(sender As Object, e As EventArgs) Handles BtnProfileApply.Click
        Try
            If _ProfileIndex < 0 Then Return
            ProfileApply(_ProfileIndex)
            Hint("已应用配色方案", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "应用配色方案失败")
        End Try
    End Sub
    Private Sub BtnProfileSaveAs_Click(sender As Object, e As EventArgs) Handles BtnProfileSaveAs.Click
        Try
            '用当前滑条的值另存为新方案；颜色换算与 ModSecret.ThemeLoadPreset 严格互逆
            Dim Hue As Integer = SliderLauncherHue.Value
            Dim Sat As Integer = SliderLauncherSat.Value
            Dim LightAdjust As Integer = SliderLauncherLight.Value - 20
            Dim TopbarDelta As Integer = (SliderLauncherDelta.Value - 90) * 2
            _ProfileIndex = ColorProfileAdd("", Hue, Sat, LightAdjust, TopbarDelta)
            Settings.Set("UiLauncherColorProfileIndex", _ProfileIndex)
            ProfileRefreshUI()
            Hint($"已保存为新的配色方案（第 {_ProfileIndex + 1} 套）", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "保存配色方案失败")
        End Try
    End Sub
    Private Sub BtnProfileOverwrite_Click(sender As Object, e As EventArgs) Handles BtnProfileOverwrite.Click
        Try
            If _ProfileIndex < 0 Then Return
            Dim Hue As Integer = SliderLauncherHue.Value
            Dim Sat As Integer = SliderLauncherSat.Value
            Dim LightAdjust As Integer = SliderLauncherLight.Value - 20
            Dim TopbarDelta As Integer = (SliderLauncherDelta.Value - 90) * 2
            ColorProfileUpdate(_ProfileIndex, Hue, Sat, LightAdjust, TopbarDelta)
            ProfileRefreshUI()
            Hint("已覆盖保存当前配色", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "覆盖保存配色方案失败")
        End Try
    End Sub
    Private Sub BtnProfileDelete_Click(sender As Object, e As EventArgs) Handles BtnProfileDelete.Click
        Try
            If _ProfileIndex < 0 Then Return
            Dim Count As Integer = ColorProfileCount()
            Dim Name As String = Nothing
            Dim H As Integer, S As Integer, L As Integer, D As Integer
            If ColorProfileGet(_ProfileIndex, Name, H, S, L, D) Then Name = Name Else Name = "该配色"
            If MyMsgBox($"确定要删除配色方案「{Name}」吗？" & vbCrLf & "删除后无法恢复，但当前的界面颜色不会改变。",
                        "删除配色方案", "删除", "取消", IsWarn:=True) <> 1 Then Return
            ColorProfileRemove(_ProfileIndex)
            '重新落位到相邻的一套
            Dim NewCount As Integer = Count - 1
            If NewCount <= 0 Then
                _ProfileIndex = -1
            ElseIf _ProfileIndex >= NewCount Then
                _ProfileIndex = NewCount - 1
            End If
            Settings.Set("UiLauncherColorProfileIndex", _ProfileIndex)
            ProfileRefreshUI()
            Hint("已删除配色方案", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "删除配色方案失败")
        End Try
    End Sub

#End Region

#Region "取色与自动优化"

    ''' <summary>
    ''' 取色结束后由 FormMain 回调过来：把取到的颜色换算成主题参数并套用。
    ''' 换算逻辑在 ModSecret.ColorToThemeParams —— PCL 的主题天然是「一个色相 + 一个饱和度」，
    ''' 所以吸到一个颜色就足以生成一整套可用配色。
    ''' </summary>
    Public Shared Sub OnColorPicked()
        Try
            Dim Page As PageSetupUI = FrmSetupUI
            If Page Is Nothing Then Return
            If FrmMain Is Nothing OrElse Not FrmMain.PickedValid Then Return
            Dim R As Integer = FrmMain.PickedR
            Dim G As Integer = FrmMain.PickedG
            Dim B As Integer = FrmMain.PickedB
            Dim Hue As Integer, Sat As Integer
            ColorToThemeParams(R, G, B, Hue, Sat)
            Page.ApplyPickedColor(Hue, Sat, $"#{R:X2}{G:X2}{B:X2}")
        Catch ex As Exception
            Logger.Error(ex, "应用取色结果失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把取到的色相/饱和度写进四个滑条并立即生效，同时报一下明暗对比体检结果。
    ''' </summary>
    Private Sub ApplyPickedColor(Hue As Integer, Sat As Integer, HexText As String)
        Try
            _ProfileApplying = True
            SyncingThemeSliders = True
            AniControlEnabled += 1
            Try
                SliderLauncherHue.Value = Hue.Clamp(0, 360)
                SliderLauncherSat.Value = Sat.Clamp(0, 100)
                '吸色只定色调与饱和度，亮度保留你原来的倾向
                If SliderLauncherLight.Value <= 0 Then SliderLauncherLight.Value = 20
            Finally
                AniControlEnabled -= 1
                SyncingThemeSliders = False
                _ProfileApplying = False
            End Try
            If Settings.Get(Of Integer)("UiLauncherTheme") <> 14 Then RadioLauncherTheme14.Checked = True
            SettingService.SaveSetting(SliderLauncherHue)
            SettingService.SaveSetting(SliderLauncherSat)
            SettingService.SaveSetting(SliderLauncherLight)
            SettingService.SaveSetting(SliderLauncherDelta)
            ThemeRefresh()
            MyMsgBox($"已取到颜色 {HexText}" & vbCrLf & vbCrLf &
                     $"换算后的主题配色：" & vbCrLf &
                     $"色调 {Hue}°　饱和度 {Sat}" & vbCrLf & vbCrLf &
                     ContrastSummary(Hue, Sat, Settings.Get(Of Integer)("UiLauncherLight") - 20) & vbCrLf & vbCrLf &
                     "如果明暗对比不理想，可以点「自动优化配色」。",
                     "取色结果")
        Catch ex As Exception
            Logger.Error(ex, "套用取色失败")
        End Try
    End Sub

    ''' <summary>
    ''' 生成对比度体检的可读文字。
    ''' </summary>
    Private Shared Function ContrastSummary(Hue As Integer, Sat As Integer, LightAdjust As Integer) As String
        Dim Report As ContrastReport = CheckContrast(Hue, Sat, LightAdjust)
        Dim Lines As New List(Of String)
        Lines.Add("明暗对比体检（括号内为建议下限）：")
        Lines.Add($"　正文　　　{Report.MainText:0.0}：1　{(If(Report.IsMainTextOk, "合格", "偏弱"))}　(≥4.5)")
        Lines.Add($"　次要文字　{Report.SecondaryText:0.0}：1　{(If(Report.IsSecondaryTextOk, "合格", "偏弱"))}　(≥3.0)")
        Lines.Add($"　强调色　　 {Report.AccentOnBg:0.0}：1　{(If(Report.IsAccentOk, "合格", "偏弱"))}　(≥3.0)")
        Return String.Join(vbCrLf, Lines)
    End Function

    Private Sub BtnPickColorUi_Click(sender As Object, e As EventArgs) Handles BtnPickColorUi.Click
        Try
            If FrmMain Is Nothing Then Return
            FrmMain.StartColorPick(True)
        Catch ex As Exception
            Logger.Error(ex, "启动界面取色失败")
        End Try
    End Sub
    Private Sub BtnPickColorScreen_Click(sender As Object, e As EventArgs) Handles BtnPickColorScreen.Click
        Try
            If FrmMain Is Nothing Then Return
            FrmMain.StartColorPick(False)
        Catch ex As Exception
            Logger.Error(ex, "启动全屏取色失败")
        End Try
    End Sub
    Private Sub BtnColorFromImage_Click(sender As Object, e As EventArgs) Handles BtnColorFromImage.Click
        Try
            Dim FileName As String = Dialogs.SelectFile("选择一张图片以提取主色", False,
                filter:={({"png", "jpg", "jpeg", "bmp", "gif", "webp"}, "常用图片文件")}).FirstOrDefault()
            If String.IsNullOrEmpty(FileName) Then Return
            If Not ExtractDominantColor(FileName) Then
                Hint("没能从这张图片里提取出主色", HintType.Red)
                Return
            End If
            Dim Hue As Integer, Sat As Integer
            ColorToThemeParams(DominantR, DominantG, DominantB, Hue, Sat)
            ApplyPickedColor(Hue, Sat, $"#{DominantR:X2}{DominantG:X2}{DominantB:X2}")
        Catch ex As Exception
            Logger.Error(ex, "从图片提取主色失败")
        End Try
    End Sub
    Private Sub BtnAutoOptimize_Click(sender As Object, e As EventArgs) Handles BtnAutoOptimize.Click
        Try
            Dim Hue As Integer = SliderLauncherHue.Value
            Dim Sat As Integer = SliderLauncherSat.Value
            Dim Current As Integer = SliderLauncherLight.Value - 20
            Dim Improved As Boolean = False
            Dim Best As Integer = AutoOptimizeLightAdjust(Hue, Sat, Current, Improved)
            If Not Improved Then
                MyMsgBox("当前配色的明暗对比已经很不错了，不需要调整。" & vbCrLf & vbCrLf &
                         ContrastSummary(Hue, Sat, Current), "自动优化配色")
                Return
            End If
            Dim Before As String = ContrastSummary(Hue, Sat, Current)
            _ProfileApplying = True
            SyncingThemeSliders = True
            AniControlEnabled += 1
            Try
                SliderLauncherLight.Value = (Best + 20).Clamp(0, 40)
            Finally
                AniControlEnabled -= 1
                SyncingThemeSliders = False
                _ProfileApplying = False
            End Try
            If Settings.Get(Of Integer)("UiLauncherTheme") <> 14 Then RadioLauncherTheme14.Checked = True
            SettingService.SaveSetting(SliderLauncherLight)
            ThemeRefresh()
            MyMsgBox($"已把亮度微调从 {Current} 调整为 {Best}。" & vbCrLf & vbCrLf &
                     "调整前：" & vbCrLf & Before & vbCrLf & vbCrLf &
                     "调整后：" & vbCrLf & ContrastSummary(Hue, Sat, Best),
                     "自动优化配色")
        Catch ex As Exception
            Logger.Error(ex, "自动优化配色失败")
        End Try
    End Sub

#End Region

#Region "功能隐藏"

    Private Shared _HiddenForceShow As Boolean = False
    ''' <summary>
    ''' 是否强制显示被禁用的功能。
    ''' </summary>
    Public Shared Property HiddenForceShow As Boolean
        Get
            Return _HiddenForceShow
        End Get
        Set(value As Boolean)
            _HiddenForceShow = value
            HiddenRefresh()
        End Set
    End Property

    ''' <summary>
    ''' 更新功能隐藏带来的显示变化。
    ''' </summary>
    Public Shared Sub HiddenRefresh() Handles Me.Loaded
        If FrmMain.PanTitleSelect Is Nothing OrElse Not FrmMain.PanTitleSelect.IsLoaded Then Return
        Try
            '顶部栏
            If Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Settings.Get(Of Boolean)("UiHiddenPageLink") AndAlso Settings.Get(Of Boolean)("UiHiddenPageSetup") AndAlso Settings.Get(Of Boolean)("UiHiddenPageOther") Then
                '顶部栏已被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed
            Else
                '顶部栏未被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Visible
                FrmMain.BtnTitleSelect1.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageDownload"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect2.Visibility = Visibility.Collapsed 'If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageLink"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect3.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageSetup"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect4.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageOther"), Visibility.Collapsed, Visibility.Visible)
            End If
            '功能
            FrmLaunchLeft.RefreshButtonsUI()
            If FrmSetupUI IsNot Nothing Then
                FrmSetupUI.CardSwitch.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenFunctionHidden"), Visibility.Collapsed, Visibility.Visible)
            End If
            '设置子页面
            If FrmSetupLeft IsNot Nothing Then
                FrmSetupLeft.ItemLaunch.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLaunch"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemUI.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupUi"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemLink.Visibility = Visibility.Collapsed 'If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLink"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemSystem.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupSystem"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                Dim AvaliableCount As Integer = 0
                If Not Settings.Get(Of Boolean)("UiHiddenSetupLaunch") Then AvaliableCount += 1
                If Not Settings.Get(Of Boolean)("UiHiddenSetupUi") Then AvaliableCount += 1
                'If Not Settings.Get(Of Boolean)("UiHiddenSetupLink") Then AvaliableCount += 1
                If Not Settings.Get(Of Boolean)("UiHiddenSetupSystem") Then AvaliableCount += 1
                FrmSetupLeft.PanItem.Visibility = If(AvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            '更多子页面
            Dim OtherAvaliableCount As Integer = 0
            If Not Settings.Get(Of Boolean)("UiHiddenOtherHelp") Then OtherAvaliableCount += 1
            If Not Settings.Get(Of Boolean)("UiHiddenOtherAbout") Then OtherAvaliableCount += 1
            If Not Settings.Get(Of Boolean)("UiHiddenOtherTest") Then OtherAvaliableCount += 1
            If Not Settings.Get(Of Boolean)("UiHiddenOtherFeedback") Then OtherAvaliableCount += 1
            If Not Settings.Get(Of Boolean)("UiHiddenOtherVote") Then OtherAvaliableCount += 1
            If FrmOtherLeft IsNot Nothing Then
                FrmOtherLeft.ItemHelp.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherHelp"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemFeedback.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherFeedback"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemVote.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherVote"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemAbout.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherAbout"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemTest.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                FrmOtherLeft.PanItem.Visibility = If(OtherAvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            If OtherAvaliableCount = 1 AndAlso Not HiddenForceShow Then
                If Not Settings.Get(Of Boolean)("UiHiddenOtherHelp") Then
                    FrmMain.BtnTitleSelect4.Text = "帮助"
                ElseIf Not Settings.Get(Of Boolean)("UiHiddenOtherAbout") Then
                    FrmMain.BtnTitleSelect4.Text = "关于"
                Else
                    FrmMain.BtnTitleSelect4.Text = "百宝箱"
                End If
            Else
                FrmMain.BtnTitleSelect4.Text = "更多"
            End If
            '各个页面的入口
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSelect Then FrmSelectRight.BtnEmptyDownload_Loaded()
            If FrmMain.PageCurrent = FormMain.PageType.Launch Then FrmLaunchLeft.RefreshButtonsUI()
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmInstanceModDisabled IsNot Nothing Then FrmInstanceModDisabled.BtnDownload_Loaded()
            '备注
            If FrmSetupUI IsNot Nothing Then FrmSetupUI.CardSwitch.Title = If(HiddenForceShow, "功能隐藏（已暂时关闭，按 F12 以重新启用）", "功能隐藏")
        Catch ex As Exception
            Logger.Error(ex, "刷新功能隐藏项目失败")
        End Try
    End Sub

    'UI 协同改变
    Private Sub HiddenSetupMain() Handles CheckHiddenPageSetup.Change
        '设置主页面
        If CheckHiddenPageSetup.Checked Then
            '开启
            CheckHiddenSetupLaunch.Checked = True
            CheckHiddenSetupSystem.Checked = True
            CheckHiddenSetupLink.Checked = True
            CheckHiddenSetupUI.Checked = True
        Else
            '关闭
            If Settings.Get(Of Boolean)("UiHiddenSetupLaunch") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupUi") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupSystem") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLink") Then
                CheckHiddenSetupLaunch.Checked = False
                CheckHiddenSetupSystem.Checked = False
                CheckHiddenSetupLink.Checked = False
                CheckHiddenSetupUI.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenSetupSub() Handles CheckHiddenSetupLaunch.Change, CheckHiddenSetupSystem.Change, CheckHiddenSetupLink.Change, CheckHiddenSetupUI.Change
        '设置子页面
        If Settings.Get(Of Boolean)("UiHiddenSetupLaunch") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupUi") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupSystem") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLink") Then
            '已被全部隐藏
            CheckHiddenPageSetup.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageSetup.Checked = False
        End If
    End Sub
    Private Sub HiddenOtherMain() Handles CheckHiddenPageOther.Change
        '更多主页面
        If CheckHiddenPageOther.Checked Then
            '开启
            CheckHiddenOtherAbout.Checked = True
            CheckHiddenOtherTest.Checked = True
            CheckHiddenOtherFeedback.Checked = True
            CheckHiddenOtherVote.Checked = True
            CheckHiddenOtherHelp.Checked = True
        Else
            '关闭
            If Settings.Get(Of Boolean)("UiHiddenOtherHelp") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherAbout") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest") AndAlso
                Settings.Get(Of Boolean)("UiHiddenOtherVote") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherFeedback") Then
                CheckHiddenOtherAbout.Checked = False
                CheckHiddenOtherTest.Checked = False
                CheckHiddenOtherFeedback.Checked = False
                CheckHiddenOtherVote.Checked = False
                CheckHiddenOtherHelp.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenOtherSub(sender As Object, user As Boolean) Handles CheckHiddenOtherHelp.Change, CheckHiddenOtherAbout.Change, CheckHiddenOtherTest.Change
        '更多子页面（有具体内容的）
        If Settings.Get(Of Boolean)("UiHiddenOtherHelp") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherAbout") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest") Then
            '已被全部隐藏
            CheckHiddenPageOther.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageOther.Checked = False
        End If
        '修改无具体内容的项
        If Not user Then Return
        If Settings.Get(Of Boolean)("UiHiddenOtherHelp") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherAbout") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest") Then
            CheckHiddenOtherFeedback.Checked = True
            CheckHiddenOtherVote.Checked = True
        End If
    End Sub
    Private Sub HiddenOtherNet(sender As Object, user As Boolean) Handles CheckHiddenOtherFeedback.Change, CheckHiddenOtherVote.Change
        '更多子页面（无具体内容的）
        If Not user Then Return
        If Settings.Get(Of Boolean)("UiHiddenOtherHelp") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherAbout") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest") AndAlso
            (Not Settings.Get(Of Boolean)("UiHiddenOtherFeedback") OrElse Not Settings.Get(Of Boolean)("UiHiddenOtherVote")) Then
            CheckHiddenOtherAbout.Checked = False
            CheckHiddenOtherTest.Checked = False
            CheckHiddenOtherHelp.Checked = False
        End If
    End Sub

    '警告提示
    Private Sub HiddenHint(sender As Object, user As Boolean) Handles CheckHiddenFunctionHidden.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupUI.Change
        If AniControlEnabled = 0 AndAlso sender.Checked Then Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……")
    End Sub

#End Region

    '滑动条
    Private Sub SliderLoad()
        SliderMusicVolume.GetHintText = Function(v) Math.Ceiling(v * 0.1) & "%"
        SliderLauncherTransparent.GetHintText = Function(v) Math.Round(40 + v * 0.1) & "%"
        SliderLauncherHue.GetHintText = Function(v) v & "°"
        SliderLauncherSat.GetHintText = Function(v) v & "%"
        SliderLauncherDelta.GetHintText =
        Function(Value As Integer) As String
            If Value > 90 Then
                Return "+" & (Value - 90)
            ElseIf Value = 90 Then
                Return 0
            Else
                Return Value - 90
            End If
        End Function
        SliderLauncherLight.GetHintText =
        Function(Value As Integer) As String
            If Value > 20 Then
                Return "+" & (Value - 20)
            ElseIf Value = 20 Then
                Return 0
            Else
                Return Value - 20
            End If
        End Function
        SliderBackgroundOpacity.GetHintText = Function(v) Math.Round(v * 0.1) & "%"
        SliderBackgroundBlur.GetHintText = Function(v) v & " 像素"
        SliderBackgroundScale.GetHintText = Function(v) v & "%"
        SliderBackgroundScaleW.GetHintText = Function(v) v & "%"
        SliderBackgroundScaleH.GetHintText = Function(v) v & "%"
        SliderBackgroundOffsetX.GetHintText = Function(v) (v - 500) & " 像素"
        SliderBackgroundOffsetY.GetHintText = Function(v) (v - 500) & " 像素"
    End Sub

End Class
