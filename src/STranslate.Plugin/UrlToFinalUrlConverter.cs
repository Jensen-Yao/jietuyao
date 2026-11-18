using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Plugin;

/// <summary>
/// 将输入的 URL 转换为最终请求的完整 URL
/// </summary>
/// <remarks>
/// 转换规则:
/// <list type="bullet">
/// <item>如果 URL 以 "#" 结尾,移除 "#" 并强制使用该地址(不添加默认路径)</item>
/// <item>根据 <see cref="Rule"/> 匹配路径并添加默认路径</item>
/// <item>其他情况保持原样</item>
/// </list>
/// </remarks>
/// <example>
/// XAML 使用示例:
/// <code>
/// &lt;!-- 使用默认规则 (OpenAI) --&gt;
/// &lt;TextBlock Text="{Binding Url, Converter={plugin:UrlToFinalUrlConverter}}" /&gt;
/// 
/// &lt;!-- ChatGLM 规则 --&gt;
/// &lt;TextBlock Text="{Binding Url, Converter={plugin:UrlToFinalUrlConverter Rule=ChatGLM, Path=/api/paas/v4/chat/completions}}" /&gt;
/// 
/// &lt;!-- 使用 ConverterParameter 覆盖默认路径 --&gt;
/// &lt;TextBlock Text="{Binding Url, Converter={plugin:UrlToFinalUrlConverter}, ConverterParameter=/custom/path}" /&gt;
/// </code>
/// </example>
[ValueConversion(typeof(string), typeof(string))]
public sealed class UrlToFinalUrlConverter : MarkupExtension, IValueConverter
{
    private static UrlToFinalUrlConverter? _defaultInstance;
    private static readonly Dictionary<(string, UrlPathMatchRule), UrlToFinalUrlConverter> _cache = new();

    /// <summary>
    /// 默认路径
    /// </summary>
    public string Path { get; set; } = UrlHelper.DefaultChatCompletionsPath;

    /// <summary>
    /// 路径匹配规则
    /// </summary>
    public UrlPathMatchRule Rule { get; set; } = UrlPathMatchRule.OpenAI;

    /// <summary>
    /// 将 URL 转换为最终请求的完整 URL
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url)
            return string.Empty;

        // ConverterParameter 优先级最高
        string path = parameter as string ?? Path;

        return UrlHelper.BuildFinalUrl(url, path, Rule);
    }

    /// <summary>
    /// 不支持反向转换
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(UrlToFinalUrlConverter)} does not support ConvertBack operation.");
    }

    /// <summary>
    /// 提供转换器实例 (使用缓存优化性能)
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // 默认配置使用单例
        if (Path == UrlHelper.DefaultChatCompletionsPath && Rule == UrlPathMatchRule.OpenAI)
            return _defaultInstance ??= new UrlToFinalUrlConverter();

        // 其他配置使用缓存
        var key = (Path, Rule);
        if (!_cache.TryGetValue(key, out var instance))
        {
            instance = new UrlToFinalUrlConverter { Path = Path, Rule = Rule };
            _cache[key] = instance;
        }

        return instance;
    }
}