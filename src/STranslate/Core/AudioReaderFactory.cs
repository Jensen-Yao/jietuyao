using NAudio.Wave;
using STranslate.Plugin;
using System.IO;

namespace STranslate.Core;

internal static class AudioReaderFactory
{
    private static readonly int[] SupportedIntegerPcmBitsPerSample = [8, 16, 24, 32];

    public static WaveStream Create(AudioData audioData, Stream audioStream)
    {
        ArgumentNullException.ThrowIfNull(audioData);
        ArgumentNullException.ThrowIfNull(audioData.Content);
        ArgumentNullException.ThrowIfNull(audioStream);

        var format = audioData.Format == AudioFormat.Auto
            ? DetectFormat(audioData.Content)
            : audioData.Format;

        return format switch
        {
            AudioFormat.Mp3 => new Mp3FileReader(audioStream),
            AudioFormat.Wav => new WaveFileReader(audioStream),
            AudioFormat.Pcm => CreatePcmReader(audioData.PcmFormat, audioStream),
            _ => throw new NotSupportedException($"不支持的音频格式: {format}")
        };
    }

    internal static AudioFormat DetectFormat(ReadOnlySpan<byte> content)
    {
        if (IsWave(content))
            return AudioFormat.Wav;

        if (IsMp3(content))
            return AudioFormat.Mp3;

        throw new NotSupportedException("无法识别音频格式，请显式指定音频格式");
    }

    private static RawSourceWaveStream CreatePcmReader(PcmAudioFormat? pcmFormat, Stream audioStream)
    {
        if (pcmFormat == null)
            throw new ArgumentException("播放裸 PCM 音频时必须提供 PCM 格式参数", nameof(pcmFormat));

        if (pcmFormat.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(pcmFormat), "PCM 采样率必须大于零");

        if (pcmFormat.Channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(pcmFormat), "PCM 声道数必须大于零");

        WaveFormat waveFormat;
        switch (pcmFormat.Encoding)
        {
            case PcmSampleEncoding.SignedIntegerLittleEndian:
                if (!SupportedIntegerPcmBitsPerSample.Contains(pcmFormat.BitsPerSample))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(pcmFormat),
                        "整数 PCM 位深必须为 8、16、24 或 32 bit");
                }

                waveFormat = new WaveFormat(
                    pcmFormat.SampleRate,
                    pcmFormat.BitsPerSample,
                    pcmFormat.Channels);
                break;

            case PcmSampleEncoding.IeeeFloatLittleEndian:
                if (pcmFormat.BitsPerSample != 32)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(pcmFormat),
                        "IEEE Float PCM 仅支持 32 bit 位深");
                }

                waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                    pcmFormat.SampleRate,
                    pcmFormat.Channels);
                break;

            default:
                throw new NotSupportedException($"不支持的 PCM 采样编码: {pcmFormat.Encoding}");
        }

        return new RawSourceWaveStream(audioStream, waveFormat);
    }

    private static bool IsWave(ReadOnlySpan<byte> content)
        => content.Length >= 12 &&
           content[..4].SequenceEqual("RIFF"u8) &&
           content.Slice(8, 4).SequenceEqual("WAVE"u8);

    private static bool IsMp3(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[..3].SequenceEqual("ID3"u8))
            return true;

        if (content.Length < 3 || content[0] != 0xFF || (content[1] & 0xE0) != 0xE0)
            return false;

        var version = content[1] & 0x18;
        var layer = content[1] & 0x06;
        var bitrateIndex = content[2] & 0xF0;
        var sampleRateIndex = content[2] & 0x0C;

        return version != 0x08 &&
               layer != 0 &&
               bitrateIndex is not 0 and not 0xF0 &&
               sampleRateIndex != 0x0C;
    }
}
