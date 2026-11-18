using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using STranslate.Core;

namespace STranslate.Controls;

/// <summary>
/// 截图编辑器浮动工具栏：选区信息行 + 工具行 + 动作行 + 工具样式面板。
/// 图标全部来自 jietuyao v1 的自有 SVG 资产（JietuyaoIcons）。
/// </summary>
public sealed class ScreenshotEditorToolbar : Border
{
    private static readonly Brush ForegroundBrush = new SolidColorBrush(Color.FromRgb(235, 235, 235));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
    private static readonly Color DimColor = Color.FromRgb(120, 120, 120);

    private readonly ScreenshotEditorSession _session;
    private readonly StackPanel _root;
    private readonly StackPanel _infoRow;
    private readonly StackPanel _toolRow;
    private readonly StackPanel _actionRow;
    private readonly StackPanel _settingsPanel;
    private readonly Dictionary<ScreenshotTool, ToggleButton> _toolButtons = [];
    private readonly Dictionary<string, ToggleButton> _infoToggles = new();
    private Button _borderModeButton = null!;

    /// <summary>动作按钮点击（复制/保存/贴图/OCR/翻译/退出）</summary>
    public event Action<ScreenshotEditorAction>? ActionRequested;

    public ScreenshotEditorToolbar(ScreenshotEditorSession session)
    {
        _session = session;

        Background = new SolidColorBrush(Color.FromArgb(235, 32, 32, 32));
        BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(8, 6, 8, 6);
        IsHitTestVisible = true;

        _root = new StackPanel { Orientation = Orientation.Vertical };
        _infoRow = new StackPanel { Orientation = Orientation.Horizontal };
        _toolRow = new StackPanel { Orientation = Orientation.Horizontal };
        _actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _settingsPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        _root.Children.Add(_infoRow);
        _root.Children.Add(_toolRow);
        _root.Children.Add(_settingsPanel);
        _root.Children.Add(_actionRow);
        Child = _root;

        BuildInfoRow();
        BuildToolRow();
        BuildActionRow();
        UpdateToolButtons();
        UpdateInfoToggles();
        session.ToolChanged += () => Dispatcher.Invoke(() => { UpdateToolButtons(); RebuildSettingsPanel(); });
        session.Undo.Changed += () => Dispatcher.Invoke(UpdateUndoRedo);
    }

    // ===== 选区信息行（jietuyao v1 独家功能）=====
    private void BuildInfoRow()
    {
        AddInfoToggle("rounded", "圆角截图", JietuyaoIcons.RoundedCorner,
            () => _session.SelectionStyle.RoundedEnabled,
            v => _session.SelectionStyle.RoundedEnabled = v);

        _borderModeButton = CreateIconButton("阴影/描边（点击切换）", JietuyaoIconRenderer.Create(JietuyaoIcons.ShadowBorder, ForegroundBrush), () =>
        {
            var style = _session.SelectionStyle;
            style.BorderMode = style.BorderMode switch
            {
                "none" => "shadow",
                "shadow" => "border",
                _ => "none"
            };
            UpdateInfoToggles();
            RefreshSettingsPanel();
        });
        _infoRow.Children.Add(_borderModeButton);

        AddInfoToggle("lockRatio", "锁定纵横比", JietuyaoIcons.LockRatio,
            () => _session.SelectionStyle.LockRatio,
            v => _session.SelectionStyle.LockRatio = v);

        var refresh = CreateIconButton("刷新背景（重新截取屏幕内容）", JietuyaoIconRenderer.Create(JietuyaoIcons.RefreshBackground, ForegroundBrush), () =>
        {
            _session.RefreshBackground();
        });
        _infoRow.Children.Add(refresh);
    }

    private void AddInfoToggle(string key, string tooltip, JietuyaoIcons.IconDef icon,
        Func<bool> getter, Action<bool> setter)
    {
        var button = new ToggleButton
        {
            Content = JietuyaoIconRenderer.Create(icon, ForegroundBrush),
            ToolTip = tooltip,
            Width = 32,
            Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ForegroundBrush,
        };
        button.Click += (_, _) =>
        {
            setter(button.IsChecked == true);
            UpdateInfoToggles();
        };
        _infoToggles[key] = button;
        _infoRow.Children.Add(button);
    }

    private void UpdateInfoToggles()
    {
        var style = _session.SelectionStyle;
        if (_infoToggles.TryGetValue("rounded", out var rounded))
        {
            rounded.IsChecked = style.RoundedEnabled;
            rounded.Background = style.RoundedEnabled ? AccentBrush : Brushes.Transparent;
        }
        if (_infoToggles.TryGetValue("lockRatio", out var lockRatio))
        {
            lockRatio.IsChecked = style.LockRatio;
            lockRatio.Background = style.LockRatio ? AccentBrush : Brushes.Transparent;
        }
        // 阴影/描边按钮着色：none=暗淡 shadow=蓝 border=红
        var brush = style.BorderMode switch
        {
            "shadow" => new SolidColorBrush((Color)ColorConverter.ConvertFromString(style.ShadowColorHex)),
            "border" => new SolidColorBrush((Color)ColorConverter.ConvertFromString(style.BorderColorHex)),
            _ => new SolidColorBrush(DimColor),
        };
        brush.Freeze();
        _borderModeButton.Content = JietuyaoIconRenderer.Create(JietuyaoIcons.ShadowBorder, brush);
        _borderModeButton.ToolTip = style.BorderMode switch
        {
            "shadow" => "阴影效果（点击切为描边）",
            "border" => "描边效果（点击关闭）",
            _ => "阴影/描边（点击开启阴影）",
        };
    }

    // ===== 工具行 =====
    private void BuildToolRow()
    {
        AddToolButton(ScreenshotTool.Select, "选择/移动 (1)", JietuyaoIconRenderer.Create(JietuyaoIcons.Select, ForegroundBrush));
        AddToolButton(ScreenshotTool.Pen, "画笔 (2)", JietuyaoIconRenderer.Create(JietuyaoIcons.Pen, ForegroundBrush));
        AddToolButton(ScreenshotTool.Highlighter, "荧光笔 (3)", JietuyaoIconRenderer.Create(JietuyaoIcons.Highlighter, ForegroundBrush));
        AddToolButton(ScreenshotTool.Rectangle, "矩形 (4)", JietuyaoIconRenderer.Create(JietuyaoIcons.Rectangle, ForegroundBrush));
        AddToolButton(ScreenshotTool.Ellipse, "椭圆 (5)", JietuyaoIconRenderer.Create(JietuyaoIcons.Ellipse, ForegroundBrush));
        AddToolButton(ScreenshotTool.Arrow, "箭头 (6)", JietuyaoIconRenderer.Create(JietuyaoIcons.Arrow, ForegroundBrush));
        AddToolButton(ScreenshotTool.Text, "文字 (7)", JietuyaoIconRenderer.Create(JietuyaoIcons.Text, ForegroundBrush));
        AddToolButton(ScreenshotTool.Number, "序号 (8)", CreateNumberIcon());
        AddToolButton(ScreenshotTool.Eraser, "橡皮 (9)", JietuyaoIconRenderer.Create(JietuyaoIcons.Eraser, ForegroundBrush));
        AddToolButton(ScreenshotTool.Cursor, "鼠标指针 (0)", JietuyaoIconRenderer.Create(JietuyaoIcons.Cursor, ForegroundBrush));

        _toolRow.Children.Add(CreateSeparator());

        _undoButton = CreateIconButton("撤销 (Ctrl+Z)", JietuyaoIconRenderer.Create(JietuyaoIcons.Undo, ForegroundBrush), () => _session.Undo.Undo());
        _redoButton = CreateIconButton("重做 (Ctrl+Y)", JietuyaoIconRenderer.Create(JietuyaoIcons.Undo, ForegroundBrush, mirror: true), () => _session.Undo.Redo());
        _toolRow.Children.Add(_undoButton);
        _toolRow.Children.Add(_redoButton);
        UpdateUndoRedo();

        _toolRow.Children.Add(CreateSeparator());

        _settingsToggle = new ToggleButton
        {
            Content = JietuyaoIconRenderer.Create(JietuyaoIcons.ColorSettings, ForegroundBrush, 14),
            ToolTip = "工具样式",
            Width = 32,
            Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ForegroundBrush,
        };
        _settingsToggle.Checked += (_, _) => { _settingsPanel.Visibility = Visibility.Visible; RebuildSettingsPanel(); };
        _settingsToggle.Unchecked += (_, _) => _settingsPanel.Visibility = Visibility.Collapsed;
        _toolRow.Children.Add(_settingsToggle);
    }

    private Button? _undoButton;
    private Button? _redoButton;
    private ToggleButton? _settingsToggle;

    /// <summary>序号图标：圆圈 + "1"</summary>
    private static UIElement CreateNumberIcon()
    {
        var grid = new Grid();
        grid.Children.Add(JietuyaoIconRenderer.Create(JietuyaoIcons.Number, ForegroundBrush, 16));
        var text = new TextBlock
        {
            Text = "1",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = ForegroundBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(text);
        return grid;
    }

    private void AddToolButton(ScreenshotTool tool, string tooltip, UIElement icon)
    {
        var button = new ToggleButton
        {
            Content = icon,
            ToolTip = tooltip,
            Width = 32,
            Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ForegroundBrush,
        };
        button.Click += (_, _) =>
        {
            if (_session.ActiveTool == tool)
            {
                // 再次点击 = 切换样式面板
                if (_settingsToggle != null)
                    _settingsToggle.IsChecked = _settingsPanel.Visibility != Visibility.Visible;
            }
            else
            {
                _session.SetTool(tool);
                RebuildSettingsPanel();
            }
        };
        _toolButtons[tool] = button;
        _toolRow.Children.Add(button);
    }

    private void UpdateToolButtons()
    {
        foreach (var (tool, button) in _toolButtons)
        {
            button.IsChecked = _session.ActiveTool == tool;
            button.Background = button.IsChecked == true ? AccentBrush : Brushes.Transparent;
            // 选中时图标反白（序号为复合图标，背景高亮已足够）
            if (tool != ScreenshotTool.Number)
                button.Content = JietuyaoIconRenderer.Create(IconForTool(tool), button.IsChecked == true ? Brushes.White : ForegroundBrush, 16);
        }
    }

    private static JietuyaoIcons.IconDef IconForTool(ScreenshotTool tool) => tool switch
    {
        ScreenshotTool.Select => JietuyaoIcons.Select,
        ScreenshotTool.Pen => JietuyaoIcons.Pen,
        ScreenshotTool.Highlighter => JietuyaoIcons.Highlighter,
        ScreenshotTool.Rectangle => JietuyaoIcons.Rectangle,
        ScreenshotTool.Ellipse => JietuyaoIcons.Ellipse,
        ScreenshotTool.Arrow => JietuyaoIcons.Arrow,
        ScreenshotTool.Text => JietuyaoIcons.Text,
        ScreenshotTool.Number => JietuyaoIcons.Number,
        ScreenshotTool.Eraser => JietuyaoIcons.Eraser,
        ScreenshotTool.Cursor => JietuyaoIcons.Cursor,
        _ => JietuyaoIcons.Select,
    };

    private void UpdateUndoRedo()
    {
        if (_undoButton != null) _undoButton.IsEnabled = _session.Undo.CanUndo;
        if (_redoButton != null) _redoButton.IsEnabled = _session.Undo.CanRedo;
        if (_undoButton != null) _undoButton.Opacity = _undoButton.IsEnabled ? 1 : 0.35;
        if (_redoButton != null) _redoButton.Opacity = _redoButton.IsEnabled ? 1 : 0.35;
    }

    // ===== 动作行 =====
    private void BuildActionRow()
    {
        _actionRow.Children.Add(CreateActionButton("OCR (O)", JietuyaoIconRenderer.Create(JietuyaoIcons.Ocr, ForegroundBrush, 15), () => ActionRequested?.Invoke(ScreenshotEditorAction.Ocr)));
        _actionRow.Children.Add(CreateActionButton("翻译 (F)", JietuyaoIconRenderer.Create(JietuyaoIcons.Translate, ForegroundBrush, 15), () => ActionRequested?.Invoke(ScreenshotEditorAction.Translate)));
        _actionRow.Children.Add(CreateActionButton("贴图 (T)", JietuyaoIconRenderer.Create(JietuyaoIcons.Pin, ForegroundBrush, 15), () => ActionRequested?.Invoke(ScreenshotEditorAction.Pin)));
        _actionRow.Children.Add(CreateSeparator());
        _actionRow.Children.Add(CreateActionButton("保存 (S)", JietuyaoIconRenderer.Create(JietuyaoIcons.Save, ForegroundBrush, 15), () => ActionRequested?.Invoke(ScreenshotEditorAction.Save)));
        _actionRow.Children.Add(CreateActionButton("复制 (Enter/C)", JietuyaoIconRenderer.Create(JietuyaoIcons.Copy, ForegroundBrush, 15), () => ActionRequested?.Invoke(ScreenshotEditorAction.Copy)));
        _actionRow.Children.Add(CreateActionButton("退出 (Esc)", JietuyaoIconRenderer.Create(JietuyaoIcons.Close, ForegroundBrush, 13), () => ActionRequested?.Invoke(ScreenshotEditorAction.None)));
    }

    // ===== 样式面板 =====
    private void RebuildSettingsPanel()
    {
        _settingsPanel.Children.Clear();
        var tool = _session.ActiveTool;
        var style = _session.GetStyle(tool);

        // 调色板
        var palette = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var hex in ScreenshotEditorSession.Palette)
        {
            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = hex,
            };
            swatch.MouseLeftButtonDown += (_, _) =>
            {
                style.ColorHex = hex;
                RefreshSwatches(palette, hex);
            };
            palette.Children.Add(swatch);
        }
        _settingsPanel.Children.Add(palette);
        RefreshSwatches(palette, style.ColorHex);

        switch (tool)
        {
            case ScreenshotTool.Pen or ScreenshotTool.Highlighter or ScreenshotTool.Rectangle or ScreenshotTool.Ellipse or ScreenshotTool.Arrow:
                _settingsPanel.Children.Add(CreateSlider("线宽", 1, 30, style.StrokeWidth, v => style.StrokeWidth = v, format: "F0"));
                if (tool is ScreenshotTool.Rectangle or ScreenshotTool.Ellipse)
                    _settingsPanel.Children.Add(CreateCheckBox("半透明填充", style.Fill, v => style.Fill = v));
                break;
            case ScreenshotTool.Text:
                _settingsPanel.Children.Add(CreateSlider("字号", 8, 60, style.FontSize, v => style.FontSize = v, format: "F0"));
                _settingsPanel.Children.Add(CreateCheckBox("白色背景", style.TextBackground, v => style.TextBackground = v));
                break;
            case ScreenshotTool.Number:
                _settingsPanel.Children.Add(CreateSlider("字号", 8, 40, style.FontSize, v => style.FontSize = v, format: "F0"));
                break;
            case ScreenshotTool.Cursor:
                _settingsPanel.Children.Add(CreateSlider("大小", 16, 64, style.StrokeWidth * 2.5, v => style.StrokeWidth = v / 2.5, format: "F0"));
                break;
            case ScreenshotTool.Eraser:
                _settingsPanel.Children.Add(CreateSlider("橡皮大小", 8, 80, style.EraserSize, v => style.EraserSize = v, format: "F0"));
                break;
        }

        // ===== 选区效果区（v1 圆角/阴影/描边参数）=====
        _settingsPanel.Children.Add(CreateSettingsSectionLabel("选区效果"));
        var selectionStyle = _session.SelectionStyle;
        var roundedToggle = CreateCheckBox("圆角截图", selectionStyle.RoundedEnabled, v =>
        {
            selectionStyle.RoundedEnabled = v;
            UpdateInfoToggles();
        });
        _settingsPanel.Children.Add(roundedToggle);
        _settingsPanel.Children.Add(CreateSlider("圆角半径", 0, 100, selectionStyle.RoundedRadius, v =>
        {
            selectionStyle.RoundedRadius = v;
            if (v > 0) selectionStyle.RoundedEnabled = true;
            UpdateInfoToggles();
        }, format: "F0"));
        _settingsPanel.Children.Add(CreateSlider("阴影/描边大小", 1, 50, selectionStyle.BorderSize, v => selectionStyle.BorderSize = v, format: "F0"));
    }

    private void RefreshSettingsPanel()
    {
        if (_settingsPanel.Visibility == Visibility.Visible)
            RebuildSettingsPanel();
    }

    private static UIElement CreateSettingsSectionLabel(string text) => new TextBlock
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
        FontSize = 11,
        Margin = new Thickness(2, 8, 0, 2),
    };

    private static void RefreshSwatches(StackPanel palette, string activeHex)
    {
        foreach (var child in palette.Children.OfType<Border>())
        {
            child.BorderBrush = (string)child.Tag == activeHex
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(120, 120, 120));
            child.BorderThickness = (string)child.Tag == activeHex
                ? new Thickness(2)
                : new Thickness(1);
        }
    }

    private UIElement CreateSlider(string label, double min, double max, double value, Action<double> setter, string format = "F1")
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var text = new TextBlock
        {
            Text = $"{label}: {value.ToString(format)}",
            Foreground = ForegroundBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 100,
        };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 120,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ForegroundBrush,
            Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
        };
        slider.ValueChanged += (_, _) =>
        {
            setter(slider.Value);
            text.Text = $"{label}: {slider.Value.ToString(format)}";
            _session.RequestRepaint();
        };
        panel.Children.Add(text);
        panel.Children.Add(slider);
        return panel;
    }

    private UIElement CreateCheckBox(string label, bool isChecked, Action<bool> setter)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = isChecked,
            Foreground = ForegroundBrush,
            Margin = new Thickness(0, 4, 0, 0),
        };
        box.Checked += (_, _) => setter(true);
        box.Unchecked += (_, _) => setter(false);
        return box;
    }

    // ===== 控件工厂 =====
    private static UIElement CreateSeparator() => new Border
    {
        Width = 1,
        Height = 20,
        Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
        Margin = new Thickness(5, 5, 5, 5),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private Button CreateIconButton(string tooltip, UIElement icon, Action action)
    {
        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip,
            Width = 32,
            Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ForegroundBrush,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button CreateActionButton(string tooltip, UIElement icon, Action action)
    {
        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip,
            Height = 30,
            MinWidth = 34,
            Padding = new Thickness(6, 0, 6, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ForegroundBrush,
        };
        button.Click += (_, _) => action();
        return button;
    }
}
