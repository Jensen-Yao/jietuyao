namespace STranslate.Plugin.Ocr.PpOnnx;

public class Settings
{
    public Settings()
    {
        // 默认：<exe>\models\ 下的 PaddleOCR 导出模型（开箱即用）
        var modelsDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "models");
        DetModelPath = System.IO.Path.Combine(modelsDir, "PP-OCRv6_det_small.onnx");
        RecModelPath = System.IO.Path.Combine(modelsDir, "PP-OCRv6_rec_small.onnx");
    }

    /// <summary>检测模型路径（如 PP-OCRv6_det_small.onnx）</summary>
    public string DetModelPath { get; set; }

    /// <summary>识别模型路径（如 PP-OCRv6_rec_small.onnx，字典内嵌 metadata）</summary>
    public string RecModelPath { get; set; }

    /// <summary>推理线程数</summary>
    public int Threads { get; set; } = 2;
}
