using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using STranslate.Core;


namespace STranslate.Controls;

/// <summary>
/// 截图编辑器浮动工具栏：工具行 + 动作行 + 工具样式面板
/// </summary>
public sealed class ScreenshotEditorToolbar : Border
{
    private static readonly Brush ForegroundBrush = new SolidColorBrush(Color.FromRgb(235, 235, 235));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
    private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromRgb(43, 43, 43));

    private readonly ScreenshotEditorSession _session;
    private readonly StackPanel _root;
    private readonly StackPanel _toolRow;
    private readonly StackPanel _actionRow;
    private readonly StackPanel _settingsPanel;
    private readonly Dictionary<ScreenshotTool, ToggleButton> _toolButtons = [];

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
        _toolRow = new StackPanel { Orientation = Orientation.Horizontal };
        _actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _settingsPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        _root.Children.Add(_toolRow);
        _root.Children.Add(_settingsPanel);
        _root.Children.Add(_actionRow);
        Child = _root;

        BuildToolRow();
        BuildActionRow();
        UpdateToolButtons();
        session.ToolChanged += () => Dispatcher.Invoke(() => { UpdateToolButtons(); RebuildSettingsPanel(); });
        session.Undo.Changed += () => Dispatcher.Invoke(UpdateUndoRedo);
    }

    // ===== 工具行 =====
    private void BuildToolRow()
    {
        AddToolButton(ScreenshotTool.Select, "选择/移动 (1)", CreateSelectIcon(false));
        AddToolButton(ScreenshotTool.Pen, "画笔 (2)", CreatePenIcon());
        AddToolButton(ScreenshotTool.Highlighter, "荧光笔 (3)", CreateHighlighterIcon());
        AddToolButton(ScreenshotTool.Rectangle, "矩形 (4)", CreateShapeIcon(rectangle: true));
        AddToolButton(ScreenshotTool.Ellipse, "椭圆 (5)", CreateShapeIcon(rectangle: false));
        AddToolButton(ScreenshotTool.Arrow, "箭头 (6)", CreateArrowIcon());
        AddToolButton(ScreenshotTool.Text, "文字 (7)", CreateGlyph("T", bold: true));
        AddToolButton(ScreenshotTool.Number, "序号 (8)", CreateNumberIcon());
        AddToolButton(ScreenshotTool.Eraser, "橡皮 (9)", CreateEraserIcon());
        AddToolButton(ScreenshotTool.Cursor, "鼠标指针 (0)", CreateSelectIcon(true));

        _toolRow.Children.Add(CreateSeparator());

        _undoButton = CreateActionButton("撤销 (Ctrl+Z)", CreateGlyph("↶", 15), () => _session.Undo.Undo());
        _redoButton = CreateActionButton("重做 (Ctrl+Y)", CreateGlyph("↷", 15), () => _session.Undo.Redo());
        _toolRow.Children.Add(_undoButton);
        _toolRow.Children.Add(_redoButton);
        UpdateUndoRedo();

        _toolRow.Children.Add(CreateSeparator());

        _settingsToggle = new ToggleButton
        {
            Content = CreateGlyph("⚙", 14),
            ToolTip = "工具样式",
            Width = 32,
            Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ForegroundBrush,
            FontSize = 14,
        };
        _settingsToggle.Checked += (_, _) => _settingsPanel.Visibility = Visibility.Visible;
        _settingsToggle.Unchecked += (_, _) => _settingsPanel.Visibility = Visibility.Collapsed;
        _toolRow.Children.Add(_settingsToggle);
    }

    private Button? _undoButton;
    private Button? _redoButton;
    private ToggleButton? _settingsToggle;

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
        }
    }

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
        _actionRow.Children.Add(CreateActionAccent("OCR (O)", CreateGlyph("A", 13), () => ActionRequested?.Invoke(ScreenshotEditorAction.Ocr)));
        _actionRow.Children.Add(CreateActionAccent("翻译 (F)", CreateGlyph("文A", 12), () => ActionRequested?.Invoke(ScreenshotEditorAction.Translate)));
        _actionRow.Children.Add(CreateActionAccent("贴图 (T)", CreatePinIcon(), () => ActionRequested?.Invoke(ScreenshotEditorAction.Pin)));
        _actionRow.Children.Add(CreateSeparator());
        _actionRow.Children.Add(CreateActionButton("保存 (S)", CreateSaveIcon(), () => ActionRequested?.Invoke(ScreenshotEditorAction.Save)));
        _actionRow.Children.Add(CreateActionButton("复制 (Enter/C)", CreateCopyIcon(), () => ActionRequested?.Invoke(ScreenshotEditorAction.Copy)));
        _actionRow.Children.Add(CreateActionButton("退出 (Esc)", CreateGlyph("✕", 12), () => ActionRequested?.Invoke(ScreenshotEditorAction.None), isDanger: true));
    }

    // ===== 样式面板 =====
    private void RebuildSettingsPanel()
    {
        _settingsPanel.Children.Clear();
        var tool = _session.ActiveTool;
        var style = _session.GetStyle(tool);

        // 调色板（文字/序号以外的所有工具 + 文字/序号同样可用）
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
    }

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
            Width = 84,
        };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 130,
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

    private static TextBlock CreateGlyph(string glyph, double size = 14, bool bold = false) => new()
    {
        Text = glyph,
        FontSize = size,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Foreground = ForegroundBrush,
        FontFamily = new FontFamily("Segoe UI"),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static UIElement CreateSelectIcon(bool filled) => new Path
    {
        Data = Geometry.Parse("M6 3 L6 19 L10.2 15.2 L12.8 21 L15 20 L12.4 14.4 L17.5 14 Z"),
        Fill = filled ? Brushes.White : Brushes.Transparent,
        Stroke = Brushes.White,
        StrokeThickness = 1.2,
        Width = 16,
        Height = 16,
        Stretch = Stretch.Uniform,
    };

    private static UIElement CreatePenIcon() => new Path
    {
        Data = Geometry.Parse("M4 20 L4.8 15.5 L15.5 4.8 L19.2 8.5 L8.5 19.2 Z M4.8 15.5 L8.5 19.2"),
        Stroke = Brushes.White,
        StrokeThickness = 1.2,
        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        Width = 16,
        Height = 16,
        Stretch = Stretch.Uniform,
    };

    private static UIElement CreateHighlighterIcon() => new Path
    {
        Data = Geometry.Parse("M9 15 L4 20 M9 15 L12 3 L17 5 L13 15 Z M4 20 L15 20"),
        Stroke = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(60, 255, 215, 0)),
        Width = 16,
        Height = 16,
        Stretch = Stretch.Uniform,
    };

    private static UIElement CreateShapeIcon(bool rectangle)
    {
        if (rectangle)
            return new Rectangle
            {
                Width = 14,
                Height = 10,
                Stroke = Brushes.White,
                StrokeThickness = 1.4,
                Fill = Brushes.Transparent,
            };
        return new Ellipse
        {
            Width = 13,
            Height = 13,
            Stroke = Brushes.White,
            StrokeThickness = 1.4,
            Fill = Brushes.Transparent,
        };
    }

    private static UIElement CreateArrowIcon() => new Path
    {
        Data = Geometry.Parse("M5 19 L17 7 M17 7 L11 7 M17 7 L17 13"),
        Stroke = Brushes.White,
        StrokeThickness = 1.6,
        Width = 16,
        Height = 16,
        Stretch = Stretch.Uniform,
    };

    private static UIElement CreateNumberIcon()
    {
        var grid = new Grid();
        grid.Children.Add(new Ellipse
        {
            Width = 15,
            Height = 15,
            Stroke = Brushes.White,
            StrokeThickness = 1.3,
            Fill = Brushes.Transparent,
        });
        var text = CreateGlyph("1", 10, bold: true);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(text);
        return grid;
    }

    private static UIElement CreateEraserIcon() => new Path
    {
        Data = Geometry.Parse("M8 18 L4 14 L12 6 L18 12 L12 18 Z M8 18 L16 18"),
        Stroke = Brushes.White,
        StrokeThickness = 1.2,
        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        Width = 16,
        Height = 16,
        Stretch = Stretch.Uniform,
    };

    private static UIElement CreateCopyIcon()
    {
        var grid = new Grid();
        var back = new Rectangle
        {
            Width = 12,
            Height = 12,
            Stroke = Brushes.White,
            StrokeThickness = 1.2,
            Margin = new Thickness(4, 0, 0, 0),
            RadiusX = 2,
            RadiusY = 2,
        };
        var front = new Rectangle
        {
            Width = 12,
            Height = 12,
            Stroke = Brushes.White,
            StrokeThickness = 1.2,
            Fill = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            Margin = new Thickness(0, 0, 4, 0),
            RadiusX = 2,
            RadiusY = 2,
        };
        grid.Children.Add(back);
        grid.Children.Add(front);
        return grid;
    }

    private static UIElement CreateSaveIcon() => new Path
    {
        Data = Geometry.Parse("M4 4 H17 L20 7 V20 H4 Z M8 4 V9 H15 V4 M8 13 H16 V20"),
        Stroke = Brushes.White,
        StrokeThickness = 1.3,
        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        Width = 15,
        Height = 15,
        Stretch = Stretch.Uniform,
    };

    private static UIElement CreatePinIcon() => new Path
    {
        Data = Geometry.Parse("M9 3 H15 V10 L17 13 H7 L9 10 Z M11 13 V18 M11 18 H13 V21 H11 Z"),
        Stroke = Brushes.White,
        StrokeThickness = 1.2,
        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        Width = 14,
        Height = 15,
        Stretch = Stretch.Uniform,
    };

    private Button CreateActionButton(string tooltip, UIElement icon, Action action, bool isDanger = false)
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
        if (isDanger)
            button.ToolTip = tooltip;
        button.Click += (_, _) => action();
        return button;
    }

    private Button CreateActionAccent(string tooltip, UIElement icon, Action action)
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
