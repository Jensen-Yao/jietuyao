namespace STranslate.Plugin.Tts.MicrosoftEdge;

public class Settings
{
    public string Url { get; set; } = "https://tts.wangwangit.com/v1/audio/speech";

    public string Voice { get; set; } = "zh-CN-XiaoxiaoNeural";

    /// <summary>
    ///     语速 (0.5-2.0)
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    ///     音调 (-50 到 50)
    /// </summary>
    public int Pitch { get; set; } = 0;

    public string Style { get; set; } = "general";
}