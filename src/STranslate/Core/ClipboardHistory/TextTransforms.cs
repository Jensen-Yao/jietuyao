namespace STranslate.Core.ClipboardHistory;

/// <summary>
/// 特殊粘贴文本变换（jietuyao v1 特色功能移植）
/// </summary>
public static class TextTransforms
{
    public sealed record TransformInfo(string Id, string Name, Func<string, string> Apply);

    public static readonly List<TransformInfo> All =
    [
        new("upper", "大写", s => s.ToUpperInvariant()),
        new("lower", "小写", s => s.ToLowerInvariant()),
        new("capitalize", "首字母大写", Capitalize),
        new("trim", "去首尾空格", s => s.Trim()),
        new("no_space", "去所有空格", s => s.Replace(" ", "").Replace("\u3000", "")),
        new("single_line", "合并为单行", s => s.Replace("\r", "").Replace("\n", " ").Trim()),
        new("no_blank_lines", "去空行", s => string.Join("\n", s.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0))),
        new("sql_in", "SQL IN 子句", SqlIn),
        new("json_escape", "JSON 转义", JsonEscape),
        new("reverse", "反转文本", s => new string(s.Reverse().ToArray())),
        new("append_time", "追加时间", s => $"{s}{DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
        new("append_line_time", "换行追加时间", s => $"{s}\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
        new("add_line_numbers", "加行号", AddLineNumbers),
    ];

    public static string? ApplyById(string id, string text)
    {
        var info = All.FirstOrDefault(t => t.Id == id);
        return info?.Apply(text);
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var chars = s.ToCharArray();
        var prevLetter = false;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                chars[i] = prevLetter ? char.ToLowerInvariant(chars[i]) : char.ToUpperInvariant(chars[i]);
                prevLetter = true;
            }
            else
            {
                prevLetter = false;
            }
        }
        return new string(chars);
    }

    private static string SqlIn(string s)
    {
        var items = s.Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(l => long.TryParse(l, out _) ? l : $"'{l.Replace("'", "''")}'");
        return $"({string.Join(", ", items)})";
    }

    private static string JsonEscape(string s) =>
        System.Text.Json.JsonEncodedText.Encode(s).ToString();

    private static string AddLineNumbers(string s)
    {
        var lines = s.Replace("\r", "").Split('\n');
        var width = lines.Length.ToString().Length;
        return string.Join("\n", lines.Select((l, i) => $"{(i + 1).ToString().PadLeft(width)}. {l}"));
    }
}
