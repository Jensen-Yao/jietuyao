namespace STranslate.Core;

/// <summary>
/// 撤销/重做命令栈。Push 时立即执行 redo 动作。
/// </summary>
public sealed class ScreenshotUndoHistory
{
    private sealed record Command(string Name, Action UndoAction, Action RedoAction);

    private readonly Stack<Command> _undoStack = new();
    private readonly Stack<Command> _redoStack = new();

    public event Action? Changed;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// 记录命令并立即执行
    /// </summary>
    public void Push(string name, Action undoAction, Action redoAction)
    {
        redoAction();
        _undoStack.Push(new Command(name, undoAction, redoAction));
        _redoStack.Clear();
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Pop();
        cmd.UndoAction();
        _redoStack.Push(cmd);
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.RedoAction();
        _undoStack.Push(cmd);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Changed?.Invoke();
    }
}
