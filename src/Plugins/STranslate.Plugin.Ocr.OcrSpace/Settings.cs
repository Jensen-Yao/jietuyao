namespace STranslate.Plugin.Ocr.OcrSpace;

public class Settings
{
    public string ApiKey { get; set; } = string.Empty;
    public OcrSpaceEngine Engine { get; set; } = OcrSpaceEngine.Engine2;
}

public enum OcrSpaceEngine
{
    // Engine 1：最快，支持多语言含中英日韩
    Engine1,

    // Engine 2：最佳全能选择，支持语言自动检测
    Engine2,

    // Engine 3：最高精度，支持 200+ 语言与表格 Markdown
    Engine3
}
