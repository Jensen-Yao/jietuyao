using NAudio.Wave;
using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.Tests;

public class AudioReaderFactoryTests
{
    private const string Mp3AudioBase64 =
        "//NExAAAAANIAAAAAExBTUUzLjEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVTEFNRTMu//NExFMAAANIAAAAADEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVTEFNRTMu//NExKYAAANIAAAAADEwMFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//NExKwAAANIAAAAAFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//NExKwAAANIAAAAAFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV";

    [Fact]
    public void Create_AutoDetectsMp3()
    {
        var content = Convert.FromBase64String(Mp3AudioBase64);
        var audioData = new AudioData(content, AudioFormat.Auto);

        using var stream = new MemoryStream(content, writable: false);
        using var reader = AudioReaderFactory.Create(audioData, stream);

        Assert.IsType<Mp3FileReader>(reader);
        Assert.Equal(24000, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
    }

    [Theory]
    [InlineData(24000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void Create_AutoDetectsWavAndPreservesSampleRate(int sampleRate)
    {
        var content = CreateWav(sampleRate, channels: 1, bitsPerSample: 16);
        var audioData = new AudioData(content, AudioFormat.Auto);

        using var stream = new MemoryStream(content, writable: false);
        using var reader = AudioReaderFactory.Create(audioData, stream);

        Assert.IsType<WaveFileReader>(reader);
        Assert.Equal(sampleRate, reader.WaveFormat.SampleRate);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
    }

    [Theory]
    [InlineData(24000, 1, 8)]
    [InlineData(24000, 1, 16)]
    [InlineData(44100, 2, 24)]
    [InlineData(48000, 2, 32)]
    public void Create_BuildsIntegerPcmWaveFormat(int sampleRate, int channels, int bitsPerSample)
    {
        var pcmFormat = new PcmAudioFormat(sampleRate, channels, bitsPerSample);
        var content = new byte[channels * (bitsPerSample / 8) * 10];
        var audioData = new AudioData(content, AudioFormat.Pcm, pcmFormat);

        using var stream = new MemoryStream(content, writable: false);
        using var reader = AudioReaderFactory.Create(audioData, stream);

        Assert.IsType<RawSourceWaveStream>(reader);
        Assert.Equal(sampleRate, reader.WaveFormat.SampleRate);
        Assert.Equal(channels, reader.WaveFormat.Channels);
        Assert.Equal(bitsPerSample, reader.WaveFormat.BitsPerSample);
        Assert.Equal(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);
    }

    [Fact]
    public void Create_BuildsGeminiPcmWaveFormat()
    {
        var content = new byte[480];
        var audioData = new AudioData(
            content,
            AudioFormat.Pcm,
            new PcmAudioFormat(24000, 1, 16));

        using var stream = new MemoryStream(content, writable: false);
        using var reader = AudioReaderFactory.Create(audioData, stream);

        Assert.Equal(24000, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
    }

    [Fact]
    public void Create_BuildsIeeeFloatPcmWaveFormat()
    {
        var content = new byte[400];
        var audioData = new AudioData(
            content,
            AudioFormat.Pcm,
            new PcmAudioFormat(48000, 2, 32, PcmSampleEncoding.IeeeFloatLittleEndian));

        using var stream = new MemoryStream(content, writable: false);
        using var reader = AudioReaderFactory.Create(audioData, stream);

        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        Assert.Equal(32, reader.WaveFormat.BitsPerSample);
    }

    [Fact]
    public void Create_RejectsPcmWithoutFormatParameters()
    {
        var content = new byte[16];
        var audioData = new AudioData(content, AudioFormat.Pcm);

        using var stream = new MemoryStream(content, writable: false);

        Assert.Throws<ArgumentException>(() => AudioReaderFactory.Create(audioData, stream));
    }

    [Theory]
    [InlineData(0, 1, 16, PcmSampleEncoding.SignedIntegerLittleEndian)]
    [InlineData(24000, 0, 16, PcmSampleEncoding.SignedIntegerLittleEndian)]
    [InlineData(24000, 1, 12, PcmSampleEncoding.SignedIntegerLittleEndian)]
    [InlineData(24000, 1, 16, PcmSampleEncoding.IeeeFloatLittleEndian)]
    public void Create_RejectsInvalidPcmParameters(
        int sampleRate,
        int channels,
        int bitsPerSample,
        PcmSampleEncoding encoding)
    {
        var content = new byte[16];
        var audioData = new AudioData(
            content,
            AudioFormat.Pcm,
            new PcmAudioFormat(sampleRate, channels, bitsPerSample, encoding));

        using var stream = new MemoryStream(content, writable: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => AudioReaderFactory.Create(audioData, stream));
    }

    [Fact]
    public void DetectFormat_RejectsUnknownBytes()
    {
        Assert.Throws<NotSupportedException>(() => AudioReaderFactory.DetectFormat([1, 2, 3, 4]));
    }

    [Fact]
    public void Create_RejectsFormatThatDoesNotMatchContent()
    {
        var content = Convert.FromBase64String(Mp3AudioBase64);
        var audioData = new AudioData(content, AudioFormat.Wav);

        using var stream = new MemoryStream(content, writable: false);

        Assert.Throws<FormatException>(() => AudioReaderFactory.Create(audioData, stream));
    }

    [Fact]
    public void Create_RejectsDamagedMp3()
    {
        byte[] content = [0xFF, 0xFB, 0x90, 0x64];
        var audioData = new AudioData(content, AudioFormat.Mp3);

        using var stream = new MemoryStream(content, writable: false);

        Assert.Throws<EndOfStreamException>(() => AudioReaderFactory.Create(audioData, stream));
    }

    private static byte[] CreateWav(int sampleRate, int channels, int bitsPerSample)
    {
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, new WaveFormat(sampleRate, bitsPerSample, channels)))
        {
            writer.Write(new byte[channels * (bitsPerSample / 8) * 10]);
        }

        return stream.ToArray();
    }
}
