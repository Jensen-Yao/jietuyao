using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;

namespace STranslate.Tests;

public class LangDetectOptionsTests
{
    [Fact]
    public void Record_Carries_All_Configured_Values()
    {
        var options = new LangDetectOptions(
            LanguageDetectorType.Bing,
            0.65,
            LangEnum.Japanese,
            LangEnum.English,
            LangEnum.ChineseSimplified);

        Assert.Equal(LanguageDetectorType.Bing, options.Detector);
        Assert.Equal(0.65, options.LocalDetectorRate);
        Assert.Equal(LangEnum.Japanese, options.SourceLangIfAuto);
        Assert.Equal(LangEnum.English, options.FirstLanguage);
        Assert.Equal(LangEnum.ChineseSimplified, options.SecondLanguage);
    }

    [Fact]
    public void Records_With_Same_Values_Are_Equal()
    {
        var a = new LangDetectOptions(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.ChineseSimplified, LangEnum.English);
        var b = new LangDetectOptions(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.ChineseSimplified, LangEnum.English);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }
}
