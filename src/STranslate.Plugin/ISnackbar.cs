namespace STranslate.Plugin;

/// <summary>
/// 消息弹窗接口
/// </summary>
public interface ISnackbar
{
    /// <summary>
    /// 显示
    /// </summary>
    /// <param name="message"></param>
    /// <param name="severity"></param>
    /// <param name="durationMs"></param>
    /// <param name="actionText"></param>
    /// <param name="actionCallback"></param>
    void Show(
        string message,
        Severity severity = Severity.Informational,
        int durationMs = 3000,
        string? actionText = null,
        Action? actionCallback = null);

    /// <summary>
    /// 显示成功
    /// </summary>
    /// <param name="message"></param>
    /// <param name="durationMs"></param>
    void ShowSuccess(string message, int durationMs = 3000);
    
    /// <summary>
    /// 显示错误
    /// </summary>
    /// <param name="message"></param>
    /// <param name="durationMs"></param>
    void ShowError(string message, int durationMs = 4000);
    
    /// <summary>
    /// 显示警告
    /// </summary>
    /// <param name="message"></param>
    /// <param name="durationMs"></param>
    void ShowWarning(string message, int durationMs = 3000);
    
    /// <summary>
    /// 显示信息
    /// </summary>
    /// <param name="message"></param>
    /// <param name="durationMs"></param>
    void ShowInfo(string message, int durationMs = 3000);
}

/// <summary>
/// 严重程度
/// </summary>
public enum Severity
{
    /// <summary>
    /// 信息
    /// </summary>
    Informational,
    
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    
    /// <summary>
    /// 警告
    /// </summary>
    Warning,
    
    /// <summary>
    /// 错误
    /// </summary>
    Error
}
