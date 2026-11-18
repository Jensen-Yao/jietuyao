using STranslate.Plugin;
using STranslate.Plugin.Translate.OpenAI;

namespace STranslate.Tests;

public class OpenAIPluginTests
{
    [Fact]
    public void Uyghur_MapsToPromptLanguageName()
    {
        var plugin = new Main();

        Assert.Equal("Uyghur", plugin.GetSourceLanguage(LangEnum.Uyghur));
        Assert.Equal("Uyghur", plugin.GetTargetLanguage(LangEnum.Uyghur));
    }
}
