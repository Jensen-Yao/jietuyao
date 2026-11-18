namespace STranslate.Plugin;

/// <summary>
/// 音频数据格式。
/// </summary>
public enum AudioFormat
{
    /// <summary>
    /// 根据音频内容自动识别格式。
    /// </summary>
    Auto,

    /// <summary>
    /// MPEG Layer-3 音频。
    /// </summary>
    Mp3,

    /// <summary>
    /// RIFF WAVE 容器音频。
    /// </summary>
    Wav,

    /// <summary>
    /// 不带容器头的 PCM 音频。
    /// </summary>
    Pcm
}

/// <summary>
/// PCM 采样编码。
/// </summary>
public enum PcmSampleEncoding
{
    /// <summary>
    /// 小端有符号整数采样。
    /// </summary>
    SignedIntegerLittleEndian,

    /// <summary>
    /// 小端 IEEE 浮点采样。
    /// </summary>
    IeeeFloatLittleEndian
}

/// <summary>
/// 裸 PCM 音频参数。
/// </summary>
/// <param name="SampleRate">采样率。</param>
/// <param name="Channels">声道数。</param>
/// <param name="BitsPerSample">每个采样的位数。</param>
/// <param name="Encoding">采样编码。</param>
public sealed record PcmAudioFormat(
    int SampleRate,
    int Channels,
    int BitsPerSample,
    PcmSampleEncoding Encoding = PcmSampleEncoding.SignedIntegerLittleEndian);

/// <summary>
/// 待播放的音频数据及格式信息。
/// </summary>
/// <param name="Content">音频字节。</param>
/// <param name="Format">音频格式。</param>
/// <param name="PcmFormat">裸 PCM 参数，仅在 <paramref name="Format"/> 为 <see cref="AudioFormat.Pcm"/> 时使用。</param>
public sealed record AudioData(
    byte[] Content,
    AudioFormat Format,
    PcmAudioFormat? PcmFormat = null);
