using System.Text.Json;
using System.Text.Json.Serialization;

namespace STranslate.Core;

internal sealed class LayoutAnalysisModeJsonConverter : JsonConverter<LayoutAnalysisMode>
{
    public override LayoutAnalysisMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ReadFromString(reader.GetString()),
            JsonTokenType.Number => ReadFromNumber(ref reader),
            _ => LayoutAnalysisMode.Auto
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        LayoutAnalysisMode value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            LayoutAnalysisMode.Provider => "provider",
            LayoutAnalysisMode.Smart => "smart",
            LayoutAnalysisMode.NoMerge => "noMerge",
            _ => "auto"
        });
    }

    private static LayoutAnalysisMode ReadFromString(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "provider" => LayoutAnalysisMode.Provider,
            "smart" => LayoutAnalysisMode.Smart,
            "nomerge" => LayoutAnalysisMode.NoMerge,
            "no_merge" => LayoutAnalysisMode.NoMerge,
            "no-merge" => LayoutAnalysisMode.NoMerge,
            _ => LayoutAnalysisMode.Auto
        };

    private static LayoutAnalysisMode ReadFromNumber(ref Utf8JsonReader reader)
    {
        if (!reader.TryGetInt32(out var value))
            return LayoutAnalysisMode.Auto;

        return value switch
        {
            1 => LayoutAnalysisMode.Provider,
            2 => LayoutAnalysisMode.Smart,
            3 or 4 => LayoutAnalysisMode.NoMerge,
            _ => LayoutAnalysisMode.Auto
        };
    }
}
