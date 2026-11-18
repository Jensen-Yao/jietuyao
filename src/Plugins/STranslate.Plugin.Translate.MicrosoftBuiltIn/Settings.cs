namespace STranslate.Plugin.Translate.MicrosoftBuiltIn;

/// <summary>
/// MicrosoftBuiltIn 插件设置。
/// </summary>
public class Settings
{
    /// <summary>
    /// 请求鉴权方案。默认使用 Default 方案。
    /// </summary>
    public RequestMode RequestMode { get; set; } = RequestMode.Default;
}

/// <summary>
/// 微软内置翻译请求方案。
/// </summary>
public enum RequestMode
{
    /// <summary>
    /// Default：X-MT-Signature 方案。
    /// </summary>
    Default = 1,

    /// <summary>
    /// EdgeToken：Authorization Bearer 方案。
    /// </summary>
    EdgeToken = 2
}
