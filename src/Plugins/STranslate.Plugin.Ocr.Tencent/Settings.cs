namespace STranslate.Plugin.Ocr.Tencent;

public class Settings
{
    public TencentOCRAction Action { get; set; } = TencentOCRAction.GeneralAccurateOCR;
    public string SecretId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public enum TencentOCRAction
{
    // 通用印刷体识别
    GeneralBasicOCR,

    // 通用印刷体识别(高精度版)
    GeneralAccurateOCR
}
