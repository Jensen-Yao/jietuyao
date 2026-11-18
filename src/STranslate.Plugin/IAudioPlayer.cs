namespace STranslate.Plugin;

/// <summary>
/// 音频播放器接口
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>
    /// 播放带格式信息的音频数据
    /// </summary>
    /// <param name="audioData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PlayAsync(AudioData audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// 播放音频
    /// </summary>
    /// <param name="audioData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PlayAsync(byte[] audioData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 播放音频
    /// </summary>
    /// <param name="audioUrl"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PlayAsync(string audioUrl, CancellationToken cancellationToken = default);
}
