using Gma.System.MouseKeyHook;
using STranslate.Core;
using System.Windows.Forms;

namespace STranslate.Helpers;

public static class CtrlSameCHelper
{
    private static IKeyboardMouseEvents? _keyboardHook;
    private static DebounceExecutor? _debounceExecutor;
    private static bool _isListening;
    private static int _pressCount;

    /// <summary>
    /// Ctrl + C 双击事件
    /// </summary>
    public static event Action? OnCtrlSameC;

    /// <summary>
    /// 启动 Ctrl + C + C 监听
    /// </summary>
    public static void Start()
    {
        if (_isListening) return;

        // 初始化防抖执行器
        _debounceExecutor = new DebounceExecutor();
        _pressCount = 0;

        _keyboardHook = Hook.GlobalEvents();
        _keyboardHook.KeyDown += OnKeyDown;

        _isListening = true;
    }

    /// <summary>
    /// 停止 Ctrl + C + C 监听
    /// </summary>
    public static void Stop()
    {
        if (!_isListening) return;

        if (_keyboardHook != null)
        {
            _keyboardHook.KeyDown -= OnKeyDown;
            _keyboardHook.Dispose();
            _keyboardHook = null;
        }

        _debounceExecutor?.Dispose();
        _debounceExecutor = null;

        _pressCount = 0;
        _isListening = false;
    }

    /// <summary>
    /// 切换监听状态
    /// </summary>
    public static void Toggle()
    {
        if (_isListening)
            Stop();
        else
            Start();
    }

    /// <summary>
    /// 判断是否正在监听
    /// </summary>
    public static bool IsListening => _isListening;

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // 监听 Ctrl + C
        // 排除 Alt 和 Shift 键，确保只是纯粹的 Ctrl + C
        // 允许 Win 键如果不做限制，但通常 Ctrl+C 不会配合 Win 键
        if (e.KeyCode == Keys.C && e.Control && !e.Alt && !e.Shift)
        {
            // 如果 e.Handled 被设为 true，会拦截按键，这里默认 false 以保留系统的复制功能

            if (_pressCount == 0)
            {
                _pressCount++;
                // 启动 500ms 超时重置任务
                // 如果在 500ms 内没有第二次按下，计数器归零
                _debounceExecutor?.Execute(() => _pressCount = 0, TimeSpan.FromMilliseconds(500));
            }
            else
            {
                // 检测到第二次按下（且在超时前）
                
                // 重置状态
                _pressCount = 0;
                // 取消当前的重置任务
                _debounceExecutor?.Cancel();

                // 检查是否应该跳过热键执行
                if (ShouldSkipHotkey())
                    return;

                // 触发事件 (可以在此处 Task.Run 避免阻塞 Hook，取决于外部订阅者的实现)
                OnCtrlSameC?.Invoke();
            }
        }
    }

    /// <summary>
    /// 检查是否应该跳过热键执行
    /// </summary>
    private static bool ShouldSkipHotkey() => HotkeyExecutionGuard.ShouldSkipGlobalHotkey();
}
