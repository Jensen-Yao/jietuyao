using STranslate.Plugin.Translate.YandexBuiltIn.View;
using STranslate.Plugin.Translate.YandexBuiltIn.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.YandexBuiltIn;

public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    private const string ApiUrl = "https://translate.yandex.net/api/v1/tr.json";
    private const string DefaultUserAgent = "ru.yandex.translate/3.20.2024";
    private CachedObject<Guid>? _cachedUcid;

    /// <summary>
    ///     https://github.com/d4n3436/GTranslate/blob/master/src/GTranslate/Translators/YandexTranslator.cs
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private class CachedObject<T>(T value, TimeSpan expirationPeriod)
    {
        public T Value { get; } = value;
        private readonly DateTime _expirationTime = DateTime.UtcNow.Add(expirationPeriod);

        public bool IsExpired() => DateTime.UtcNow > _expirationTime;
    }

    private Guid GetOrUpdateUcid()
    {
        if (_cachedUcid == null || _cachedUcid.IsExpired())
        {
            _cachedUcid = new CachedObject<Guid>(Guid.NewGuid(), TimeSpan.FromSeconds(360));
        }
        return _cachedUcid.Value;
    }

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel();
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    /// <summary>
    ///     https://zh.wikipedia.org/wiki/ISO_639-1
    /// </summary>
    /// <param name="lang"></param>
    /// <returns></returns>
    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh",
        LangEnum.ChineseTraditional => "zh",
        LangEnum.Cantonese => "zh",
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn",
        LangEnum.MongolianTraditional => "mn",
        LangEnum.Khmer => "km",
        LangEnum.NorwegianBokmal => "no",
        LangEnum.NorwegianNynorsk => "no",
        LangEnum.Persian => "fa",
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => null,
        _ => "auto"
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh",
        LangEnum.ChineseTraditional => "zh",
        LangEnum.Cantonese => "zh",
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn",
        LangEnum.MongolianTraditional => "mn",
        LangEnum.Khmer => "km",
        LangEnum.NorwegianBokmal => "no",
        LangEnum.NorwegianNynorsk => "no",
        LangEnum.Persian => "fa",
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => null,
        _ => "auto"
    };

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() { }

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        if (GetSourceLanguage(request.SourceLang) is not string sourceStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedSourceLang"));
            return;
        }
        if (GetTargetLanguage(request.TargetLang) is not string targetStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        string query = $"?ucid={GetOrUpdateUcid():N}&srv=android&format=text";

        var url = $"{ApiUrl}/translate{query}";

        var options = new Options
        {
            Headers = new Dictionary<string, string>
            {
                { "User-Agent", DefaultUserAgent }
            }
        };

        var formData = new Dictionary<string, string>
        {
            { "text", request.Text },
            { "lang", sourceStr == null ? targetStr : $"{sourceStr}-{targetStr}" }
        };

        var response = await Context.HttpService.PostFormAsync(url, formData, options, cancellationToken);
        var textNode = JsonNode.Parse(response)?["text"];
        var data = textNode is JsonArray arr && arr.Count > 0
            ? arr[0]?.ToString() ?? throw new Exception($"No result.\nRaw: {response}")
            : throw new Exception($"No result.\nRaw: {response}");
        result.Success(data);
    }
}
