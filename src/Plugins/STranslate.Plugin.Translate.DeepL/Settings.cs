using System.Text.Json.Serialization;

namespace STranslate.Plugin.Translate.DeepL;

/// <summary>
/// 保存 DeepL 插件的鉴权、API 地址和用量信息。
/// </summary>
public class Settings
{
    public string ApiKey { get; set; } = string.Empty;

    public DeepLApiType ApiType { get; set; } = DeepLApiType.ApiFree;

    public string CustomApiUrl { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseProApi { get; set; }

    public string UsageStr { get; set; } = string.Empty;
    public double Usage { get; set; }

    internal bool MigrateLegacyApiType()
    {
        if (UseProApi is not bool useProApi)
            return false;

        ApiType = useProApi ? DeepLApiType.Api : DeepLApiType.ApiFree;
        UseProApi = null;
        return true;
    }
}

/// <summary>
/// 指定 DeepL 插件使用的 API 基地址类型。
/// </summary>
public enum DeepLApiType
{
    /// <summary>
    /// DeepL API Pro 标准地址。
    /// </summary>
    Api,

    /// <summary>
    /// DeepL API Free 标准地址。
    /// </summary>
    ApiFree,

    /// <summary>
    /// 用户提供的 DeepL 兼容 API 基地址。
    /// </summary>
    Custom
}
