#!/usr/bin/env python3
"""M12 patch B(灵动交互)/C(QuickAction)/D(接线) 第二段：画布/会话/服务/编辑器窗口"""
from pathlib import Path

# ================= B2. Shift 正形 =================
p = Path("src/STranslate/Core/Screenshot/AnnotationCanvas.cs")
t = p.read_text(encoding="utf-8")

helper = """    /// <summary>Shift 约束：从锚点出发强制正方形（正选区/正圆/正方）</summary>
    private static Point ShiftConstrain(Point anchor, Point current)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var dx = current.X - anchor.X;
            var dy = current.Y - anchor.Y;
            var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new Point(anchor.X + Math.Sign(dx) * side, anchor.Y + Math.Sign(dy) * side);
        }
        return current;
    }

    private enum Interaction"""
assert "private enum Interaction" in t and "ShiftConstrain" not in t
t = t.replace("    private enum Interaction", helper, 1)

old = """            case Interaction.CreatingSelection:
            {
                var rect = Normalize(new Rect(_pressPoint, physical));
                Session.Selection = ClampToVirtual(rect);"""
new = """            case Interaction.CreatingSelection:
            {
                var rect = Normalize(new Rect(_pressPoint, ShiftConstrain(_pressPoint, physical)));
                Session.Selection = ClampToVirtual(rect);"""
assert old in t
t = t.replace(old, new, 1)

old = """                else
                {
                    _previewShapeRect = new Rect(_pressPoint, physical);
                    RenderPreview();
                }
                break;"""
new = """                else
                {
                    _previewShapeRect = new Rect(_pressPoint, ShiftConstrain(_pressPoint, physical));
                    RenderPreview();
                }
                break;"""
assert old in t
t = t.replace(old, new, 1)

old = """            case Interaction.CreatingSelection:
            {
                var rect = Normalize(new Rect(_pressPoint, physical));
                // 近似点击：尝试吸附智能窗口"""
new = """            case Interaction.CreatingSelection:
            {
                var rect = Normalize(new Rect(_pressPoint, ShiftConstrain(_pressPoint, physical)));
                // 近似点击：尝试吸附智能窗口"""
assert old in t
t = t.replace(old, new, 1)

# ================= B4. 滚轮调工具粗细 =================
wheel = """    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        // 灵动细节：画笔/荧光笔/马赛克/橡皮激活时，滚轮直接调当前工具粗细
        if (Session.ActiveTool is ScreenshotTool.Pen or ScreenshotTool.Highlighter
            or ScreenshotTool.Mosaic or ScreenshotTool.Eraser)
        {
            var style = Session.GetStyle(Session.ActiveTool);
            var (min, max, step) = Session.ActiveTool switch
            {
                ScreenshotTool.Eraser => (8, 80, 2.0),
                ScreenshotTool.Mosaic => (4, 48, 2.0),
                _ => (1, 30, 1.0),
            };
            var current = Session.ActiveTool == ScreenshotTool.Eraser ? style.EraserSize : style.StrokeWidth;
            var next = Math.Clamp(current + Math.Sign(e.Delta) * step, min, max);
            if (Session.ActiveTool == ScreenshotTool.Eraser)
                style.EraserSize = next;
            else
                style.StrokeWidth = next;
            Session.NotifyToolStyleChanged();
            e.Handled = true;
            return;
        }
        base.OnMouseWheel(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)"""
assert "protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)" in t and "OnMouseWheel(MouseWheelEventArgs e)" not in t
t = t.replace("    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)", wheel, 1)
p.write_text(t, encoding="utf-8")

# Session：样式变更通知
p2 = Path("src/STranslate/Core/Screenshot/ScreenshotEditorSession.cs")
t2 = p2.read_text(encoding="utf-8")
old2 = """    public void SetTool(ScreenshotTool tool)
    {
        if (ActiveTool == tool) return;
        ActiveTool = tool;
        ToolChanged?.Invoke();
    }"""
new2 = """    public void SetTool(ScreenshotTool tool)
    {
        if (ActiveTool == tool) return;
        ActiveTool = tool;
        ToolChanged?.Invoke();
    }

    /// <summary>工具样式（粗细等）外部变更通知：刷新工具栏面板</summary>
    public void NotifyToolStyleChanged() => ToolChanged?.Invoke();"""
assert old2 in t2 and "NotifyToolStyleChanged" not in t2
t2 = t2.replace(old2, new2, 1)
p2.write_text(t2, encoding="utf-8")

# ================= B1+D. 服务：记住工具/译图目标语接线 =================
p3 = Path("src/STranslate/Core/Screenshot/ScreenshotEditorService.cs")
t3 = p3.read_text(encoding="utf-8")
old3 = """                session.BuildPixelCache();
                session.ToolStyles = settings.ScreenshotToolStyles;"""
new3 = """                session.BuildPixelCache();
                session.ToolStyles = settings.ScreenshotToolStyles;
                if (settings.RememberScreenshotTool &&
                    Enum.TryParse<ScreenshotTool>(settings.LastScreenshotTool, out var lastTool))
                {
                    session.SetTool(lastTool);
                }"""
assert old3 in t3 and "RememberScreenshotTool" not in t3
t3 = t3.replace(old3, new3, 1)

old4 = """        settings.ScreenshotBorderSize = style.BorderSize;
        settings.ScreenshotLockRatio = style.LockRatio;
    }"""
new4 = """        settings.ScreenshotBorderSize = style.BorderSize;
        settings.ScreenshotLockRatio = style.LockRatio;
        if (settings.RememberScreenshotTool)
            settings.LastScreenshotTool = session.ActiveTool.ToString();
    }"""
assert old4 in t3
t3 = t3.replace(old4, new4, 1)

old5 = "            var outcome = await TranslatedImageController.CreateAsync(clean);"
new5 = "            var outcome = await TranslatedImageController.CreateAsync(clean, TranslatedImageController.ParseTarget(settings.ImageOverlayTargetLang));"
assert old5 in t3
t3 = t3.replace(old5, new5, 1)
p3.write_text(t3, encoding="utf-8")

# ================= B3. Esc 分级 =================
p4 = Path("src/STranslate/Views/ScreenshotEditorWindow.xaml.cs")
t4 = p4.read_text(encoding="utf-8")
old6 = """            case Key.Escape:
                _session.Cancel();
                e.Handled = true;
                break;"""
new6 = """            case Key.Escape:
                // 分级退出：有选区且无标注 → 先清空选区；再次 Esc 才退出
                if (_session.Selection is { } && _session.Annotations.Count == 0)
                    _session.Selection = null;
                else
                    _session.Cancel();
                e.Handled = true;
                break;"""
assert old6 in t4 and "分级退出" not in t4
t4 = t4.replace(old6, new6, 1)

# ================= C. QuickAction =================
old7 = "        _canvas.BeginTextEditRequested += ShowTextEditor;"
new7 = """        _canvas.SelectionStabilized += OnSelectionStabilizedForQuick;
        _canvas.BeginTextEditRequested += ShowTextEditor;"""
assert old7 in t4 and "OnSelectionStabilizedForQuick" not in t4
t4 = t4.replace(old7, new7, 1)

old8 = "    public void CloseEditor()"
new8 = """    /// <summary>QuickAction：选区稳定后短暂停顿即自动执行（拖动/改手柄会取消）</summary>
    private void OnSelectionStabilizedForQuick()
    {
        if (Session.Options.QuickAction is not { } action)
            return;
        _quickTimer?.Stop();
        _quickTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _quickTimer.Tick += (_, _) =>
        {
            _quickTimer?.Stop();
            if (Session.Selection is { Width: >= 8 })
                _session.Finish(action);
        };
        _quickTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer? _quickTimer;

    public void CloseEditor()"""
t4 = t4.replace(old8, new8, 1)

old9 = "        _canvas.InteractionStarted += HideToolbar;"
new9 = "        _canvas.InteractionStarted += () => { _quickTimer?.Stop(); HideToolbar(); };"
assert old9 in t4
t4 = t4.replace(old9, new9, 1)

old10 = """    public void CloseEditor()
    {
        if (_closed) return;
        _closed = true;"""
new10 = """    public void CloseEditor()
    {
        if (_closed) return;
        _closed = true;
        _quickTimer?.Stop();"""
assert old10 in t4
t4 = t4.replace(old10, new10, 1)
p4.write_text(t4, encoding="utf-8")

print("B/C/D-service patches done")
