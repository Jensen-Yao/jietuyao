using System.Diagnostics.CodeAnalysis;
using System.IO;
using ZXing;
using ZXing.ZKWeb;

namespace STranslate.Core;

internal readonly record struct QrCodePoint(float X, float Y);

internal readonly record struct QrCodeDecodeResult(
    string? Text,
    IReadOnlyList<QrCodePoint>? Points,
    Exception? Error)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}

internal static class QrCodeDecoder
{
    public static QrCodeDecodeResult Decode(byte[] bytes)
    {
        try
        {
            var reader = new BarcodeReader();
            reader.Options.CharacterSet = "UTF-8";

            using var stream = new MemoryStream(bytes);
            using var bitmap = new System.DrawingCore.Bitmap(stream);
            var result = reader.Decode(bitmap);

            var points = result?.ResultPoints?
                .Select(point => new QrCodePoint(point.X, point.Y))
                .ToArray();

            return new QrCodeDecodeResult(result?.Text, points, null);
        }
        catch (Exception ex)
        {
            return new QrCodeDecodeResult(null, null, ex);
        }
    }

    public static bool TryGetWebUri(string? text, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var candidate) ||
            string.IsNullOrWhiteSpace(candidate.Host) ||
            !(candidate.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              candidate.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        uri = candidate;
        return true;
    }
}
