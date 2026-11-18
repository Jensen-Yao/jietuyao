using STranslate.Core;
using STranslate.Plugin;
using System.Reflection;
using System.Text.Json;

namespace STranslate.Tests;

public class SettingsOcrLanguageTests
{
    [Fact]
    public void SettingsExposeSeparateOcrLanguagePropertiesAndRemoveLegacyProperty()
    {
        var propertyNames = typeof(Settings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("OcrLanguage", propertyNames);
        Assert.Contains("ScreenshotOcrLanguage", propertyNames);
        Assert.Contains("OcrWindowOcrLanguage", propertyNames);
        Assert.Contains("ImageTranslateOcrLanguage", propertyNames);
        Assert.Contains("IsImageTranslateCompactOcrLanguageVisible", propertyNames);
    }

    [Fact]
    public void SeparateOcrLanguageSettingsDefaultToAutoAndDoNotMigrateLegacyOcrLanguage()
    {
        var settings = JsonSerializer.Deserialize<Settings>(
            """
            {
              "OcrLanguage": "ChineseSimplified"
            }
            """);

        Assert.NotNull(settings);
        Assert.Equal(LangEnum.Auto, GetSetting<LangEnum>(settings, "ScreenshotOcrLanguage"));
        Assert.Equal(LangEnum.Auto, GetSetting<LangEnum>(settings, "OcrWindowOcrLanguage"));
        Assert.Equal(LangEnum.Auto, GetSetting<LangEnum>(settings, "ImageTranslateOcrLanguage"));
        Assert.False(GetSetting<bool>(settings, "IsImageTranslateCompactOcrLanguageVisible"));
    }

    private static T GetSetting<T>(Settings settings, string propertyName)
    {
        var property = typeof(Settings).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);

        var value = property.GetValue(settings);
        return Assert.IsType<T>(value);
    }
}
