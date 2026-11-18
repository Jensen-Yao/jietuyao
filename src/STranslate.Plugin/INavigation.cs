namespace STranslate.Plugin;

/// <summary>
///     用于处理切换导航 PasswordBox 内容丢失的问题
/// </summary>
internal interface INavigation
{
    /// <summary>
    /// 是否导航
    /// </summary>
    bool IsNavigated { get; set; }
}
