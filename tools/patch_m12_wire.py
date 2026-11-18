#!/usr/bin/env python3
"""M12 第二段：Options.QuickAction / ParseTarget / 快捷键 Ctrl+Shift+2 / 设置页新卡 / i18n"""
from pathlib import Path

# 1) QuickAction 属性
p = Path("src/STranslate/Core/Screenshot/ScreenshotTypes.cs")
t = p.read_text(encoding="utf-8")
old = """    /// <summary>是否隐藏主窗口与贴图翻译窗口后再截图</summary>
    public bool HideAppWindows { get; init; } = true;
}"""
new = """    /// <summary>是否隐藏主窗口与贴图翻译窗口后再截图</summary>
    public bool HideAppWindows { get; init; } = true;

    /// <summary>选区确定后自动执行的动作（如截图即译图直达键）；空 = 停留编辑器</summary>
    public ScreenshotEditorAction? QuickAction { get; init; }
}"""
assert old in t and "QuickAction" not in t
t = t.replace(old, new, 1)
p.write_text(t, encoding="utf-8")

# 2) ParseTarget
p2 = Path("src/STranslate/Core/Screenshot/TranslatedImageController.cs")
t2 = p2.read_text(encoding="utf-8")
anchor = "    private static LangEnum InferTarget(string sample)"
add = """    /// <summary>设置字符串 → 目标语（空/未知 = null = 自动推断）</summary>
    public static LangEnum? ParseTarget(string? tag) => (tag ?? "").Trim().ToLowerInvariant() switch
    {
        "" or "auto" => null,
        "zh" or "zh-hans" or "zh-cn" => LangEnum.ChineseSimplified,
        "zh-hant" or "zh-tw" => LangEnum.ChineseTraditional,
        "en" => LangEnum.English,
        "ja" or "jp" => LangEnum.Japanese,
        "ko" or "kr" => LangEnum.Korean,
        "fr" => LangEnum.French,
        "de" => LangEnum.German,
        "es" => LangEnum.Spanish,
        "ru" => LangEnum.Russian,
        _ => null,
    };

""" + anchor
assert anchor in t2 and "ParseTarget" not in t2
t2 = t2.replace(anchor, add, 1)
# CreateAsync 默认改为设置驱动
old2 = "    public static async Task<Result> CreateAsync(Bitmap cleanImage, LangEnum? targetOverride = null)"
new2 = """    public static Task<Result> CreateAsync(Bitmap cleanImage) =>
        CreateAsync(cleanImage, ParseTarget(Ioc.Default.GetService<Settings>()?.ImageOverlayTargetLang));

    public static async Task<Result> CreateAsync(Bitmap cleanImage, LangEnum? targetOverride)"""
assert old2 in t2
t2 = t2.replace(old2, new2, 1)
p2.write_text(t2, encoding="utf-8")

# 3) VM 命令
p3 = Path("src/STranslate/ViewModels/MainWindowViewModel.cs")
t3 = p3.read_text(encoding="utf-8")
anchor3 = """    /// <summary>
    /// 剪贴板图片贴屏（Ctrl+Shift+3，吸收自 SnipTrans）：读剪贴板图片贴到光标附近
    /// </summary>"""
add3 = """    /// <summary>
    /// 截图即译图（Ctrl+Shift+2）：框选后自动执行 译图，直达 SnipTrans 式体验
    /// </summary>
    [RelayCommand]
    private async Task QuickImageOverlayAsync()
    {
        var editor = Ioc.Default.GetRequiredService<ScreenshotEditorService>();
        using var result = await editor.EditAsync(new ScreenshotEditOptions
        {
            QuickAction = ScreenshotEditorAction.ImageOverlay,
        });
        result?.Dispose();
    }

""" + anchor3
assert anchor3 in t3 and "QuickImageOverlayAsync" not in t3
t3 = t3.replace(anchor3, add3, 1)
p3.write_text(t3, encoding="utf-8")

# 4) 热键六处
p4 = Path("src/STranslate/Core/HotkeySettings.cs")
t4 = p4.read_text(encoding="utf-8")
t4 = t4.replace('    public GlobalHotkey ClipboardPinHotkey { get; set; } = new("Ctrl + Shift + 3");',
                '    public GlobalHotkey ClipboardPinHotkey { get; set; } = new("Ctrl + Shift + 3");\n    public GlobalHotkey QuickOverlayHotkey { get; set; } = new("Ctrl + Shift + 2");')
t4 = t4.replace('        CreateGlobalHotkeyData(ClipboardPinHotkey.Key, "Hotkey_ClipboardPin", () => ClipboardPinHotkey.Key = Constant.EmptyHotkey),',
                '        CreateGlobalHotkeyData(ClipboardPinHotkey.Key, "Hotkey_ClipboardPin", () => ClipboardPinHotkey.Key = Constant.EmptyHotkey),\n        CreateGlobalHotkeyData(QuickOverlayHotkey.Key, "Hotkey_QuickOverlay", () => QuickOverlayHotkey.Key = Constant.EmptyHotkey),')
t4 = t4.replace('            [nameof(ClipboardPinHotkey)] = "Ctrl + Shift + 3",',
                '            [nameof(ClipboardPinHotkey)] = "Ctrl + Shift + 3",\n            [nameof(QuickOverlayHotkey)] = "Ctrl + Shift + 2",')
t4 = t4.replace('        HandleGlobalLogic(nameof(ClipboardPinHotkey), retryOnConflict: true);',
                '        HandleGlobalLogic(nameof(ClipboardPinHotkey), retryOnConflict: true);\n        HandleGlobalLogic(nameof(QuickOverlayHotkey), retryOnConflict: true);')
t4 = t4.replace('        HotkeyMapper.RemoveHotkey(ClipboardPinHotkey.Key);',
                '        HotkeyMapper.RemoveHotkey(ClipboardPinHotkey.Key);\n        HotkeyMapper.RemoveHotkey(QuickOverlayHotkey.Key);')
t4 = t4.replace("""            case nameof(ClipboardPinHotkey):
                RegisterHotkey(ClipboardPinHotkey, WithFullscreenCheck(() => MainWindowViewModel.PinClipboardImageCommand.Execute(null)), retryOnConflict);
                break;""",
"""            case nameof(ClipboardPinHotkey):
                RegisterHotkey(ClipboardPinHotkey, WithFullscreenCheck(() => MainWindowViewModel.PinClipboardImageCommand.Execute(null)), retryOnConflict);
                break;
            case nameof(QuickOverlayHotkey):
                RegisterHotkey(QuickOverlayHotkey, WithFullscreenCheck(() => MainWindowViewModel.QuickImageOverlayCommand.Execute(null)), retryOnConflict);
                break;""")
t4 = t4.replace('            case nameof(ClipboardPinHotkey): ClipboardPinHotkey.IsConflict = isConflict; break;',
                '            case nameof(ClipboardPinHotkey): ClipboardPinHotkey.IsConflict = isConflict; break;\n            case nameof(QuickOverlayHotkey): QuickOverlayHotkey.IsConflict = isConflict; break;')
assert "QuickOverlayHotkey" in t4
p4.write_text(t4, encoding="utf-8")

# 5) 托盘菜单
p5 = Path("src/STranslate/Views/MainWindow.xaml")
t5 = p5.read_text(encoding="utf-8")
anchor5 = '''                    <MenuItem
                        Command="{Binding Source={StaticResource ViewModel}, Path=Data.PinClipboardImageCommand}"'''
add5 = '''                    <MenuItem
                        Command="{Binding Source={StaticResource ViewModel}, Path=Data.QuickImageOverlayCommand}"
                        Header="{DynamicResource Hotkey_QuickOverlay}">
                        <MenuItem.Icon>
                            <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Glasses_20_Regular}" />
                        </MenuItem.Icon>
                    </MenuItem>
'''
assert anchor5 in t5 and "QuickImageOverlayCommand" not in t5
t5 = t5.replace(anchor5, add5 + anchor5, 1)
p5.write_text(t5, encoding="utf-8")

# 6) 热键页卡片
p6 = Path("src/STranslate/Views/Pages/HotkeyPage.xaml")
t6 = p6.read_text(encoding="utf-8")
anchor6 = '''                    <ui:SettingsCard Description="{DynamicResource Hotkey_ClipboardPinDescription}" Header="{DynamicResource Hotkey_ClipboardPin}">'''
add6 = '''                    <ui:SettingsCard Description="{DynamicResource Hotkey_QuickOverlayDescription}" Header="{DynamicResource Hotkey_QuickOverlay}">
                        <ui:SettingsCard.HeaderIcon>
                            <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Glasses_24_Regular}" />
                        </ui:SettingsCard.HeaderIcon>
                        <control:HotkeyControl
                            DefaultHotkey="{Binding HotkeySettings.QuickOverlayHotkey.Default}"
                            Hotkey="{Binding HotkeySettings.QuickOverlayHotkey.Key}"
                            IsRegistered="{Binding HotkeySettings.QuickOverlayHotkey.IsConflict}"
                            WindowTitle="{DynamicResource Hotkey_QuickOverlay}" />
                    </ui:SettingsCard>

'''
assert anchor6 in t6 and "QuickOverlayHotkey" not in t6
t6 = t6.replace(anchor6, add6 + anchor6, 1)
p6.write_text(t6, encoding="utf-8")

# 7) 截图耀设置页新卡（编辑器分组前追加 GIF/译图/记忆工具）
p7 = Path("src/STranslate/Views/Pages/ScreenshotPage.xaml")
t7 = p7.read_text(encoding="utf-8")
anchor7 = '<TextBlock Margin="2,14,0,2" Style="{DynamicResource {x:Static ui:ThemeKeys.SubtitleTextBlockStyleKey}}" Text="{DynamicResource Jy_EditorGroup}" />'
add7 = '''<TextBlock Margin="2,14,0,2" Style="{DynamicResource {x:Static ui:ThemeKeys.SubtitleTextBlockStyleKey}}" Text="{DynamicResource Jy_SmartGroup}" />
                <control:CardGroup xmlns:control="clr-namespace:STranslate.Controls">
                    <ui:SettingsCard Description="{DynamicResource Jy_RememberToolDesc}" Header="{DynamicResource Jy_RememberTool}">
                        <ui:SettingsCard.HeaderIcon>
                            <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.History_24_Regular}" />
                        </ui:SettingsCard.HeaderIcon>
                        <ui:ToggleSwitch IsOn="{Binding Settings.RememberScreenshotTool, Mode=TwoWay}" />
                    </ui:SettingsCard>
                    <ui:SettingsCard Description="{DynamicResource Jy_GifFpsDesc}" Header="{DynamicResource Jy_GifFps}">
                        <ui:SettingsCard.HeaderIcon>
                            <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.SpeedHigh_24_Regular}" />
                        </ui:SettingsCard.HeaderIcon>
                        <ui:NumberBox
                            MinWidth="140"
                            LargeChange="5"
                            Maximum="24"
                            Minimum="5"
                            SmallChange="1"
                            SpinButtonPlacementMode="Compact"
                            Value="{Binding Settings.GifDefaultFps, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    </ui:SettingsCard>
                    <ui:SettingsCard Description="{DynamicResource Jy_OverlayTargetDesc}" Header="{DynamicResource Jy_OverlayTarget}">
                        <ui:SettingsCard.HeaderIcon>
                            <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Globe_24_Regular}" />
                        </ui:SettingsCard.HeaderIcon>
                        <ComboBox
                            MinWidth="150"
                            DisplayMemberPath="Key"
                            SelectedValuePath="Value"
                            SelectedValue="{Binding Settings.ImageOverlayTargetLang, Mode=TwoWay}">
                            <ComboBoxItem DataContext="{x:Null}" />
                        </ComboBox>
                    </ui:SettingsCard>
                </control:CardGroup>

''' + anchor7
assert anchor7 in t7 and "Jy_SmartGroup" not in t7
t7 = t7.replace(anchor7, add7, 1)
p7.write_text(t7, encoding="utf-8")

print("patch2 done（ComboBox 项在下一步 code-behind 填充）")
