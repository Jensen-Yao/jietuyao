namespace STranslate.Plugin.Translate.GoogleBuiltIn;

/// <summary>
/// Google 内置翻译插件设置。
/// </summary>
public class Settings
{
    /// <summary>
    /// 翻译请求模式。默认沿用原有的中转 API，避免升级后改变现有行为。
    /// </summary>
    public RequestMode RequestMode { get; set; } = RequestMode.CustomApi;

    /// <summary>
    /// 自定义中转 API 的完整地址。
    /// </summary>
    public string ApiUrl { get; set; } = "https://google.stranslate.deno.net/translate";
}

/// <summary>
/// Google 内置翻译的请求模式。
/// </summary>
public enum RequestMode
{
    /// <summary>
    /// 使用兼容 Deno 示例协议的自定义中转 API。
    /// </summary>
    CustomApi = 1,

    /// <summary>
    /// 从当前电脑直接访问 Google 翻译网站接口。
    /// </summary>
    Direct = 2
}
