namespace STranslate.Plugin.Translate.Azure;

public class Settings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    /// <summary>自定义端点（如 VNET/区域网关），留空使用公共端点</summary>
    public string Endpoint { get; set; } = string.Empty;
}
