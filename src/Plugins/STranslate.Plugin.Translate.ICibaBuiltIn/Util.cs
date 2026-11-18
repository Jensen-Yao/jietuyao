using System.Text.RegularExpressions;

namespace STranslate.Plugin.Translate.ICibaBuiltIn;

public partial class Util
{
    public static bool IsChinese(string text) => ChineseRegex().IsMatch(text);

    [GeneratedRegex(@"^[\u4E00-\u9FA5]+$")]
    private static partial Regex ChineseRegex();
}
