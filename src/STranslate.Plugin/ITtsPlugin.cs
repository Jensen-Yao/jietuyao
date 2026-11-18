namespace STranslate.Plugin;

/// <summary>
/// 表示文本转语音（TTS）插件的接口，定义了支持的语言、合成语音和播放音频的功能。
/// </summary>
public interface ITtsPlugin : IPlugin
{
    /// <summary>
    /// 异步播放音频数据。
    /// </summary>
    /// <param name="text">待合成文本。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns></returns>
    Task PlayAudioAsync(string text, CancellationToken cancellationToken = default);
}
