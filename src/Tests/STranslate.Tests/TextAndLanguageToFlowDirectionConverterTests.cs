using System.Globalization;
using System.Windows.Data;
using STranslate.Converters;

namespace STranslate.Tests;

public class TextAndLanguageToFlowDirectionConverterTests
{
    [Fact]
    public void ConvertBack_Returns_DoNothing_For_Each_Source_Binding()
    {
        var converter = new TextAndLanguageToFlowDirectionConverter();
        var targetTypes = new[] { typeof(string), typeof(object), typeof(object) };

        var values = converter.ConvertBack(
            value: null!,
            targetTypes,
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal(targetTypes.Length, values.Length);
        Assert.All(values, value => Assert.Same(Binding.DoNothing, value));
    }
}
