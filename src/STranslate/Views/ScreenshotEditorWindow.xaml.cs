using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using STranslate.Controls;
using STranslate.Core;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace STranslate.Views;

/// <summary>
/// 截图编辑器窗口：每显示器一个，共享 ScreenshotEditorSession
/// </summary>
public sealed partial class ScreenshotEditorWindow : Window
{
    private readonly ScreenshotEditorSession _session;
    private readonly AnnotationCanvas _canvas;
    private readonly ScreenshotEditorToolbar _toolbar;
    private TextBox? _textEditor;
    private bool _closed;

    public ScreenshotMonitor Monitor { get; }

    public ScreenshotEditorWindow(ScreenshotEditorSession session, ScreenshotMonitor monitor)
    {
        _session = session;
        Monitor = monitor;

        InitializeComponent();

        _canvas = new AnnotationCanvas(session);
        CanvasHost.Children.Add(_canvas);
        _canvas.ApplyViewport(monitor.Bounds.TopLeft, monitor.DpiScale);

        _toolbar = new ScreenshotEditorToolbar(session);
        _toolbar.Visibility = Visibility.Collapsed;
        _toolbar.IsHitTestVisible = true;
        OverlayPanel.Children.Add(_toolbar);
        _toolbar.ActionRequested += action =>
        {
            if (action == ScreenshotEditorAction.None)
                _session.Cancel();
            else
                _session.Finish(action);
        };

        _canvas.BeginTextEditRequested += ShowTextEditor;
        _canvas.ContextMenuRequested += ShowContextMenu;
        _canvas.SelectionStabilized += UpdateToolbarPosition;
        _canvas.InteractionStarted += HideToolbar;
        _canvas.InteractionEnded += UpdateToolbarPosition;
        _canvas.DefaultActionRequested += () => _session.Finish(ScreenshotEditorAction.Copy);
        session.SessionClosing += CloseEditor;

        // 窗口 dip 尺寸（按目标显示器 DPI）
        var scale = monitor.DpiScale;
        Width = monitor.Bounds.Width / scale;
        Height = monitor.Bounds.Height / scale;
        Left = monitor.Bounds.Left / scale;
        Top = monitor.Bounds.Top / scale;

        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void ShowNoActivate()
    {
        Show();
        // 显示后用物理坐标精确定位（规避 WPF 多 DPI 换算误差）
        PositionPhysical();
    }

    public void ActivateCanvas()
    {
        Activate();
        _canvas.Focus();
        Keyboard.Focus(_canvas);
    }

    private unsafe void PositionPhysical()
    {
        var hwnd = new HWND(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        PInvoke.SetWindowPos(hwnd, HWND.Null,
            (int)Monitor.Bounds.X, (int)Monitor.Bounds.Y,
            (int)Math.Ceiling(Monitor.Bounds.Width), (int)Math.Ceiling(Monitor.Bounds.Height),
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PositionPhysical();
    }

    public void CloseEditor()
    {
        if (_closed) return;
        _closed = true;
        _session.SessionClosing -= CloseEditor;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        CommitTextEditor();
    }

    // ===== 键盘 =====
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 文本编辑中让文本框自行处理（Esc/Ctrl+Enter 在文本框内处理）
        if (_textEditor != null)
            return;

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10.0 : 1.0;

        switch (e.Key)
        {
            case Key.Escape:
                _session.Cancel();
                e.Handled = true;
                break;

            case Key.Enter:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.Copy);
                    e.Handled = true;
                }
                break;

            case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    _session.Undo.Redo();
                else
                    _session.Undo.Undo();
                e.Handled = true;
                break;

            case Key.Y when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                _session.Undo.Redo();
                e.Handled = true;
                break;

            case Key.S:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.Save);
                    e.Handled = true;
                }
                break;

            case Key.C when !Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.Copy);
                    e.Handled = true;
                }
                break;

            case Key.T:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.Pin);
                    e.Handled = true;
                }
                break;

            case Key.I:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.ImageOverlay);
                    e.Handled = true;
                }
                break;

            case Key.O:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.Ocr);
                    e.Handled = true;
                }
                break;

            case Key.F:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.Translate);
                    e.Handled = true;
                }
                break;

            case Key.L:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.LongScreenshot);
                    e.Handled = true;
                }
                break;

            case Key.G:
                if (_session.Selection != null)
                {
                    _session.Finish(ScreenshotEditorAction.GifRecord);
                    e.Handled = true;
                }
                break;

            case Key.Delete:
                _canvas.DeleteHoveredOrClear();
                e.Handled = true;
                break;

            case Key.Left:
                _canvas.NudgeSelection(-step, 0);
                e.Handled = true;
                break;
            case Key.Right:
                _canvas.NudgeSelection(step, 0);
                e.Handled = true;
                break;
            case Key.Up:
                _canvas.NudgeSelection(0, -step);
                e.Handled = true;
                break;
            case Key.Down:
                _canvas.NudgeSelection(0, step);
                e.Handled = true;
                break;

            case Key.D1: case Key.NumPad1: _session.SetTool(ScreenshotTool.Select); e.Handled = true; break;
            case Key.D2: case Key.NumPad2: _session.SetTool(ScreenshotTool.Pen); e.Handled = true; break;
            case Key.D3: case Key.NumPad3: _session.SetTool(ScreenshotTool.Highlighter); e.Handled = true; break;
            case Key.D4: case Key.NumPad4: _session.SetTool(ScreenshotTool.Rectangle); e.Handled = true; break;
            case Key.D5: case Key.NumPad5: _session.SetTool(ScreenshotTool.Ellipse); e.Handled = true; break;
            case Key.D6: case Key.NumPad6: _session.SetTool(ScreenshotTool.Arrow); e.Handled = true; break;
            case Key.D7: case Key.NumPad7: _session.SetTool(ScreenshotTool.Text); e.Handled = true; break;
            case Key.D8: case Key.NumPad8: _session.SetTool(ScreenshotTool.Number); e.Handled = true; break;
            case Key.D9: case Key.NumPad9: _session.SetTool(ScreenshotTool.Eraser); e.Handled = true; break;
            case Key.D0: case Key.NumPad0: _session.SetTool(ScreenshotTool.Cursor); e.Handled = true; break;
        }
    }

    // ===== 文本编辑覆盖层 =====
    private Point _textEditorPhysicalPoint;

    private void ShowTextEditor(Point physicalPoint)
    {
        if (_textEditor != null)
            CommitTextEditor();

        var style = _session.GetStyle(ScreenshotTool.Text);
        var color = (Color)ColorConverter.ConvertFromString(style.ColorHex);
        _textEditorPhysicalPoint = physicalPoint;

        var dip = _canvas.PhysicalToDip(physicalPoint);
        _textEditor = new TextBox
        {
            FontSize = style.FontSize / _canvas.Scale,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(color),
            Background = style.TextBackground
                ? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            BorderThickness = new Thickness(1),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 60,
            MinHeight = style.FontSize / _canvas.Scale + 8,
            IsHitTestVisible = true,
            Padding = new Thickness(4, 2, 4, 2),
        };
        _textEditor.PreviewKeyDown += OnTextEditorKeyDown;
        _textEditor.LostKeyboardFocus += (_, _) => CommitTextEditor();

        OverlayPanel.Children.Add(_textEditor);
        Canvas.SetLeft(_textEditor, dip.X);
        Canvas.SetTop(_textEditor, dip.Y);
        _textEditor.Focus();
        Keyboard.Focus(_textEditor);
    }

    private void OnTextEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (_textEditor == null) return;
        switch (e.Key)
        {
            case Key.Escape:
                _textEditor.Text = string.Empty;
                RemoveTextEditor();
                e.Handled = true;
                _canvas.Focus();
                break;
            case Key.Enter when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                CommitTextEditor();
                e.Handled = true;
                _canvas.Focus();
                break;
        }
    }

    private void CommitTextEditor()
    {
        if (_textEditor == null) return;
        var text = _textEditor.Text;
        var style = _session.GetStyle(ScreenshotTool.Text);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var color = (Color)ColorConverter.ConvertFromString(style.ColorHex);
            _session.AddAnnotation(new TextAnnotation(_textEditorPhysicalPoint, text, color,
                style.FontSize, style.TextBackground));
        }
        RemoveTextEditor();
    }

    private void RemoveTextEditor()
    {
        if (_textEditor == null) return;
        OverlayPanel.Children.Remove(_textEditor);
        _textEditor = null;
    }

    // ===== 右键菜单 =====
    private void ShowContextMenu(Point physicalPoint)
    {
        var menu = new ContextMenu();

        void Add(string header, Action action, bool separator = false)
        {
            if (separator)
                menu.Items.Add(new Separator());
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Add("复制 (Enter)", () => _session.Finish(ScreenshotEditorAction.Copy));
        Add("保存 (S)", () => _session.Finish(ScreenshotEditorAction.Save));
        Add("贴图 (T)", () => _session.Finish(ScreenshotEditorAction.Pin));
        Add("OCR (O)", () => _session.Finish(ScreenshotEditorAction.Ocr));
        Add("翻译 (F)", () => _session.Finish(ScreenshotEditorAction.Translate));
        Add($"撤销 (Ctrl+Z)", () => _session.Undo.Undo(), separator: true);
        Add($"重做 (Ctrl+Y)", () => _session.Undo.Redo());
        Add("清空标注 (Del)", _canvas.DeleteHoveredOrClear);
        Add("退出截图 (Esc)", _session.Cancel, separator: true);

        menu.PlacementTarget = _canvas;
        menu.Placement = PlacementMode.RelativePoint;
        var dip = _canvas.PhysicalToDip(physicalPoint);
        menu.HorizontalOffset = dip.X;
        menu.VerticalOffset = dip.Y;
        menu.IsOpen = true;
    }

    // ===== 工具栏定位 =====
    private void OnSelectionChanged()
    {
        // 选区拖动期间隐藏，稳定后由 SelectionStabilized 处理
    }

    private void HideToolbar()
    {
        _toolbar.Visibility = Visibility.Collapsed;
    }

    private void UpdateToolbarPosition()
    {
        if (_session.Selection is not { } selection || selection.Width < 8 || selection.Height < 8)
        {
            _toolbar.Visibility = Visibility.Collapsed;
            return;
        }

        // 只在选区所在显示器显示
        var center = new Point(selection.X + selection.Width / 2, selection.Y + selection.Height / 2);
        if (!Monitor.Bounds.Contains(center))
        {
            _toolbar.Visibility = Visibility.Collapsed;
            return;
        }

        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _toolbar.DesiredSize;
        if (size.Width < 10 || size.Height < 10)
            return;

        var selDip = new Rect(_canvas.PhysicalToDip(selection.Location),
            new Size(selection.Width / _canvas.Scale, selection.Height / _canvas.Scale));

        var x = selDip.Left + selDip.Width / 2 - size.Width / 2;
        var y = selDip.Bottom + 8;
        if (y + size.Height > ActualHeight - 4)
            y = selDip.Top - size.Height - 8;
        if (y < 4) y = 4;
        x = Math.Clamp(x, 4, Math.Max(4, ActualWidth - size.Width - 4));

        Canvas.SetLeft(_toolbar, x);
        Canvas.SetTop(_toolbar, y);
        _toolbar.Visibility = Visibility.Visible;
    }
}
