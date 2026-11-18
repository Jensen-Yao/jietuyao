using System.Windows;
using STranslate.Helpers;
using STranslate.Plugin;

namespace STranslate.Tests;

public class BidiDirectionHelperTests
{
    [Fact]
    public void Explicit_Language_Takes_Precedence_Over_Text()
    {
        var direction = BidiDirectionHelper.GetFlowDirection(
            "English text",
            LangEnum.Persian,
            LangEnum.English);

        Assert.Equal(FlowDirection.RightToLeft, direction);
    }

    [Fact]
    public void Explicit_Uyghur_Uses_RightToLeft_Direction()
    {
        var direction = BidiDirectionHelper.GetFlowDirection(
            "English text",
            LangEnum.Uyghur,
            LangEnum.English);

        Assert.Equal(FlowDirection.RightToLeft, direction);
    }

    [Fact]
    public void Detected_Language_Is_Used_When_Explicit_Language_Is_Auto()
    {
        var direction = BidiDirectionHelper.GetFlowDirection(
            "English text",
            LangEnum.Auto,
            LangEnum.Arabic);

        Assert.Equal(FlowDirection.RightToLeft, direction);
    }

    [Fact]
    public void Explicit_LeftToRight_Language_Overrides_RightToLeft_Text()
    {
        var direction = BidiDirectionHelper.GetFlowDirection(
            "سلام",
            LangEnum.English,
            LangEnum.Persian);

        Assert.Equal(FlowDirection.LeftToRight, direction);
    }

    [Theory]
    [InlineData("سلام، کد «ABC» است.", FlowDirection.RightToLeft)]
    [InlineData("ABC سلام", FlowDirection.LeftToRight)]
    [InlineData("، ١٢٣ ABC", FlowDirection.LeftToRight)]
    [InlineData("123، سلام", FlowDirection.RightToLeft)]
    [InlineData("َABC", FlowDirection.LeftToRight)]
    [InlineData("😀سلام", FlowDirection.RightToLeft)]
    [InlineData("\U0001EE00 ABC", FlowDirection.RightToLeft)]
    [InlineData("שלום ABC", FlowDirection.RightToLeft)]
    [InlineData("", FlowDirection.LeftToRight)]
    [InlineData("  123 😀", FlowDirection.LeftToRight)]
    public void Auto_Language_Uses_First_Strong_Directional_Letter(
        string text,
        FlowDirection expected)
    {
        var direction = BidiDirectionHelper.GetFlowDirection(
            text,
            LangEnum.Auto,
            LangEnum.Auto);

        Assert.Equal(expected, direction);
    }
}
