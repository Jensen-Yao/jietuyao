using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using STranslate.Core;

namespace STranslate.Controls;

/// <summary>
/// 微信式截图工具栏：单行紧凑。工具图标用自有矢量（JietuyaoIcons SVG→Path，任意 DPI 锐利）；
/// 动作用文字符号。齿轮弹出样式飞出面板（调色板/参数/选区效果），「更多」收纳 OCR/翻译/译图/长截图/GIF。
/// </summary>
public sealed class ScreenshotEditorToolbar : Border
{
    private static readonly Brush Fg = Frozen(Color.FromRgb(235, 235, 235));
    private static readonly Brush DimFg = Frozen(Color.FromRgb(150, 150, 150));
    private static readonly Brush Accent = Frozen(Color.FromRgb(0, 120, 215));
    private static readonly Brush Success = Frozen(Color.FromRgb(46, 160, 107));

    private readonly ScreenshotEditorSession _session;
    private readonly StackPanel _row;
    private readonly Dictionary<ScreenshotTool, ToggleButton> _toolButtons = [];
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly ToggleButton _gearButton;
    private readonly Popup _flyout;

    public event Action<ScreenshotEditorAction>? ActionRequested;

    public ScreenshotEditorToolbar(ScreenshotEditorSession session)
    {
        _session = session;

        Background = new SolidColorBrush(Color.FromArgb(240, 28, 28, 30));
        BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(9);
        Padding = new Thickness(5, 4, 5, 4);
        IsHitTestVisible = true;

        _row = new StackPanel { Orientation = Orientation.Horizontal };
        Child = _row;

        // ===== 工具组（矢量图标 + 快捷键提示） =====
        AddTool(ScreenshotTool.Select, "选择/移动 (1)", JietuyaoIcons.Select);
        AddTool(ScreenshotTool.Rectangle, "矩形 (4) · Shift 正方", JietuyaoIcons.Rectangle);
        AddTool(ScreenshotTool.Ellipse, "椭圆 (5) · Shift 正圆", JietuyaoIcons.Ellipse);
        AddTool(ScreenshotTool.Arrow, "箭头 (6)", JietuyaoIcons.Arrow);
        AddTool(ScreenshotTool.Pen, "画笔 (2) · 滚轮调粗细", JietuyaoIcons.Pen);
        AddTool(ScreenshotTool.Highlighter, "荧光笔 (3)", JietuyaoIcons.Highlighter);
        AddTool(ScreenshotTool.Mosaic, "马赛克 · 滚轮调块大小", JietuyaoIcons.Mosaic);
        AddTool(ScreenshotTool.Text, "文字 (7)", JietuyaoIcons.Text);
        AddTool(ScreenshotTool.Number, "序号 (8)", JietuyaoIcons.Number);
        AddTool(ScreenshotTool.Eraser, "橡皮 (9) · 滚轮调大小", JietuyaoIcons.Eraser);
        AddTool(ScreenshotTool.Cursor, "鼠标指针 (0)", JietuyaoIcons.Cursor);

        _row.Children.Add(Sep());

        _undoButton = Act("撤销 (Ctrl+Z)", "↶", () => _session.Undo.Undo());
        _redoButton = Act("重做 (Ctrl+Y)", "↷", () => _session.Undo.Redo());
        _row.Children.Add(_undoButton);
        _row.Children.Add(_redoButton);

        _row.Children.Add(Sep());

        // ===== 齿轮：样式飞出面板 =====
        _flyout = new Popup
        {
            Child = new StackPanel(),
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
        };
        _gearButton = new ToggleButton
        {
            Content = new TextBlock { Text = "⚙", FontSize = 15, Foreground = Fg },
            ToolTip = "样式与选区效果",
            Width = 34,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Fg,
            Cursor = Cursors.Hand,
        };
        _gearButton.Click += (_, _) =>
        {
            if (_gearButton.IsChecked == true)
            {
                _flyout.Child = BuildFlyout();
                _flyout.IsOpen = true;
            }
            else
            {
                _flyout.IsOpen = false;
            }
        };
        _flyout.Closed += (_, _) => _gearButton.IsChecked = false;
        _row.Children.Add(_gearButton);

        _row.Children.Add(Sep());

        // ===== 动作组（微信式：绿色完成 + 紧凑次要动作） =====
        _row.Children.Add(Act("完成 · 复制 (Enter / 双击)", "✓",
            () => ActionRequested?.Invoke(ScreenshotEditorAction.Copy), success: true));
        _row.Children.Add(Act("保存 (S)", "存", () => ActionRequested?.Invoke(ScreenshotEditorAction.Save)));
        _row.Children.Add(Act("贴图 (T)", "钉", () => ActionRequested?.Invoke(ScreenshotEditorAction.Pin)));

        var more = Act("更多：OCR / 翻译 / 译图 / 长截图 / GIF", "⋯", () => { });
        more.Click += (_, _) => OpenMoreMenu(more);
        _row.Children.Add(more);

        UpdateToolButtons();
        UpdateUndoRedo();

        session.ToolChanged += () => Dispatcher.Invoke(() =>
        {
            UpdateToolButtons();
            if (_flyout.IsOpen)
                _flyout.Child = BuildFlyout();
        });
        session.Undo.Changed += () => Dispatcher.Invoke(UpdateUndoRedo);
        Unloaded += (_, _) => _flyout.IsOpen = false;
    }

    private void OpenMoreMenu(Button anchor)
    {
        var menu = new ContextMenu { Placement = PlacementMode.Bottom, PlacementTarget = anchor };
        void M(string header, ScreenshotEditorAction action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => ActionRequested?.Invoke(action);
            menu.Items.Add(item);
        }
        M("OCR 识别 (O)", ScreenshotEditorAction.Ocr);
        M("翻译 (F)", ScreenshotEditorAction.Translate);
        M("译图 · 工作台 (I)", ScreenshotEditorAction.ImageOverlay);
        M("滚动长截图 (L)", ScreenshotEditorAction.LongScreenshot);
        M("GIF 录屏 (G)", ScreenshotEditorAction.GifRecord);
        menu.Items.Add(new Separator());
        M("复制", ScreenshotEditorAction.Copy);
        M("退出 (Esc)", ScreenshotEditorAction.None);
        menu.IsOpen = true;
    }

    // ===== 样式飞出面板 =====
    private Border BuildFlyout()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        var tool = _session.ActiveTool;
        var style = _session.GetStyle(tool);

        panel.Children.Add(new TextBlock
        {
            Text = $"「{ToolName(tool)}」样式",
            Foreground = DimFg,
            FontSize = 11,
            Margin = new Thickness(2, 0, 0, 6),
        });

        var palette = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var hex in ScreenshotEditorSession.Palette)
        {
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
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
        panel.Children.Add(palette);
        RefreshSwatches(palette, style.ColorHex);

        switch (tool)
        {
            case ScreenshotTool.Pen or ScreenshotTool.Highlighter or ScreenshotTool.Rectangle or ScreenshotTool.Ellipse or ScreenshotTool.Arrow:
                panel.Children.Add(SliderRow("线宽", 1, 30, style.StrokeWidth, v => style.StrokeWidth = v));
                if (tool is ScreenshotTool.Rectangle or ScreenshotTool.Ellipse)
                    panel.Children.Add(CheckRow("半透明填充", style.Fill, v => style.Fill = v));
                break;
            case ScreenshotTool.Text:
                panel.Children.Add(SliderRow("字号", 8, 60, style.FontSize, v => style.FontSize = v));
                panel.Children.Add(CheckRow("白色背景", style.TextBackground, v => style.TextBackground = v));
                break;
            case ScreenshotTool.Number:
                panel.Children.Add(SliderRow("字号", 8, 40, style.FontSize, v => style.FontSize = v));
                break;
            case ScreenshotTool.Cursor:
                panel.Children.Add(SliderRow("大小", 16, 64, style.StrokeWidth * 2.5, v => style.StrokeWidth = v / 2.5));
                break;
            case ScreenshotTool.Eraser:
                panel.Children.Add(SliderRow("橡皮大小", 8, 80, style.EraserSize, v => style.EraserSize = v));
                break;
            case ScreenshotTool.Mosaic:
                panel.Children.Add(SliderRow("马赛克块", 4, 48, style.StrokeWidth, v => style.StrokeWidth = v));
                break;
        }

        // ===== 选区效果 =====
        panel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 6) });
        panel.Children.Add(new TextBlock { Text = "选区效果", Foreground = DimFg, FontSize = 11, Margin = new Thickness(2, 0, 0, 6) });

        var ss = _session.SelectionStyle;
        var effectRow = new StackPanel { Orientation = Orientation.Horizontal };
        effectRow.Children.Add(EffectBtn("圆角", () => ss.RoundedEnabled, v => ss.RoundedEnabled = v));
        effectRow.Children.Add(EffectBtn("描边/阴影", () => ss.BorderMode != "none",
            _ => ss.BorderMode = ss.BorderMode switch { "none" => "shadow", "shadow" => "border", _ => "none" }));
        effectRow.Children.Add(EffectBtn("锁比例", () => ss.LockRatio, v => ss.LockRatio = v));
        var refresh = new Button
        {
            Content = new TextBlock { Text = "⟳", FontSize = 15, Foreground = Fg },
            ToolTip = "刷新背景（保持选区与标注重截屏幕）",
            Width = 40,
            Height = 28,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Fg,
            Cursor = Cursors.Hand,
            Margin = new Thickness(4, 0, 0, 0),
        };
        refresh.Click += (_, _) => _session.RefreshBackground();
        effectRow.Children.Add(refresh);
        panel.Children.Add(effectRow);

        panel.Children.Add(SliderRow("圆角半径", 0, 100, ss.RoundedRadius, v => { ss.RoundedRadius = v; if (v > 0) ss.RoundedEnabled = true; }));
        panel.Children.Add(SliderRow("阴影/描边大小", 1, 50, ss.BorderSize, v => ss.BorderSize = v));

        return new Border
        {
            Child = panel,
            Background = new SolidColorBrush(Color.FromArgb(246, 30, 30, 32)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 6, 0, 0),
        };
    }

    private static string ToolName(ScreenshotTool tool) => tool switch
    {
        ScreenshotTool.Select => "选择",
        ScreenshotTool.Pen => "画笔",
        ScreenshotTool.Highlighter => "荧光笔",
        ScreenshotTool.Rectangle => "矩形",
        ScreenshotTool.Ellipse => "椭圆",
        ScreenshotTool.Arrow => "箭头",
        ScreenshotTool.Text => "文字",
        ScreenshotTool.Number => "序号",
        ScreenshotTool.Eraser => "橡皮",
        ScreenshotTool.Cursor => "鼠标指针",
        ScreenshotTool.Mosaic => "马赛克",
        _ => "工具",
    };

    private ToggleButton EffectBtn(string label, Func<bool> get, Action<bool> set)
    {
        var btn = new ToggleButton
        {
            Content = new TextBlock { Text = label, FontSize = 12, Foreground = Fg },
            ToolTip = label,
            Padding = new Thickness(8, 4, 8, 4),
            Height = 28,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 4, 0),
        };
        btn.IsChecked = get();
        if (get()) btn.Background = Accent;
        btn.Click += (_, _) =>
        {
            set(btn.IsChecked == true);
            btn.Background = btn.IsChecked == true ? Accent : Brushes.Transparent;
        };
        return btn;
    }

    private UIElement SliderRow(string label, double min, double max, double value, Action<double> set)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
        var text = new TextBlock
        {
            Text = $"{label}: {value:0}",
            Foreground = Fg,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 96,
            FontSize = 12,
        };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 130,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Fg,
            Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
        };
        slider.ValueChanged += (_, _) =>
        {
            set(slider.Value);
            text.Text = $"{label}: {slider.Value:0}";
            _session.RequestRepaint();
        };
        panel.Children.Add(text);
        panel.Children.Add(slider);
        return panel;
    }

    private UIElement CheckRow(string label, bool value, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = value,
            Foreground = Fg,
            Margin = new Thickness(0, 5, 0, 0),
        };
        box.Checked += (_, _) => set(true);
        box.Unchecked += (_, _) => set(false);
        return box;
    }

    private static void RefreshSwatches(StackPanel palette, string activeHex)
    {
        foreach (var child in palette.Children.OfType<Border>())
        {
            var active = (string)child.Tag == activeHex;
            child.BorderBrush = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(110, 110, 110));
            child.BorderThickness = active ? new Thickness(2) : new Thickness(1);
        }
    }

    // ===== 工具按钮 =====
    private void AddTool(ScreenshotTool tool, string tip, JietuyaoIcons.IconDef icon)
    {
        var button = new ToggleButton
        {
            Content = JietuyaoIconRenderer.Create(icon, Fg, 17),
            ToolTip = tip,
            Width = 34,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Fg,
            Cursor = Cursors.Hand,
        };
        button.Click += (_, _) =>
        {
            if (_session.ActiveTool != tool)
                _session.SetTool(tool);
            else
                _gearButton.IsChecked = true; // 再次点击同工具 → 打开样式面板
        };
        _toolButtons[tool] = button;
        _row.Children.Add(button);
    }

    private void UpdateToolButtons()
    {
        foreach (var (tool, button) in _toolButtons)
        {
            var active = _session.ActiveTool == tool;
            button.IsChecked = active;
            button.Background = active ? Accent : Brushes.Transparent;
            if (tool is not (ScreenshotTool.Number or ScreenshotTool.Mosaic) && button.Content is Viewbox)
            {
                // 选中态反白：重建矢量图标
                var def = ToolIcon(tool);
                button.Content = JietuyaoIconRenderer.Create(def, active ? Brushes.White : Fg, 17);
            }
        }
    }

    private static JietuyaoIcons.IconDef ToolIcon(ScreenshotTool tool) => tool switch
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
        ScreenshotTool.Mosaic => JietuyaoIcons.Mosaic,
        _ => JietuyaoIcons.Select,
    };

    private void UpdateUndoRedo()
    {
        _undoButton.IsEnabled = _session.Undo.CanUndo;
        _redoButton.IsEnabled = _session.Undo.CanRedo;
        _undoButton.Opacity = _undoButton.IsEnabled ? 1 : 0.35;
        _redoButton.Opacity = _redoButton.IsEnabled ? 1 : 0.35;
    }

    // ===== 基元 =====
    private Button Act(string tip, string symbol, Action action, bool success = false)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = symbol,
                FontSize = success ? 16 : 13,
                FontWeight = success ? FontWeights.Bold : FontWeights.Normal,
                Foreground = success ? Brushes.White : Fg,
            },
            ToolTip = tip,
            Width = 34,
            Height = 32,
            Background = success ? Success : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Fg,
            Cursor = Cursors.Hand,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Border Sep() => new()
    {
        Width = 1,
        Height = 20,
        Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
        Margin = new Thickness(5, 6, 5, 6),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
