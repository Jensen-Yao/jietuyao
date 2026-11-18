using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Baidu;

public class Settings
{
    public BaiduOCRAction Action { get; set; } = BaiduOCRAction.Accurate;
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public enum BaiduOCRAction
{
    // 高精度版
    [Description("accurate")]
    Accurate,

    [Description("accurate_basic")]
    AccurateBasic,

    // 标准版
    [Description("general")]
    General,

    [Description("general_basic")]
    GeneralBasic,
}