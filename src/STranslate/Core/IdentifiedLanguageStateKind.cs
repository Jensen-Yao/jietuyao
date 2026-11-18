namespace STranslate.Core;

/// <summary>
/// 描述主界面识别语言结果的来源状态。
/// </summary>
public enum IdentifiedLanguageStateKind
{
    /// <summary>
    /// 当前没有可展示的识别语言。
    /// </summary>
    None,

    /// <summary>
    /// 识别语言来自历史缓存。
    /// </summary>
    Cache,

    /// <summary>
    /// 识别语言来自本次检测或用户手动选择。
    /// </summary>
    Detected
}
