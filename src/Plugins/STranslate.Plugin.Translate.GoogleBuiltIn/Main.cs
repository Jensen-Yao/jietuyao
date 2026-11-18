using STranslate.Plugin.Translate.GoogleBuiltIn.View;
using STranslate.Plugin.Translate.GoogleBuiltIn.ViewModel;
using System.Text.Json;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.GoogleBuiltIn;

public class Main : TranslatePluginBase
{
    private const string GoogleTranslateUrl = "https://translate.google.com/translate_a/single";

    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings, this);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh-CN",
        LangEnum.ChineseTraditional => "zh-TW",
        LangEnum.Cantonese => "yue",
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
        LangEnum.Uyghur => "ug",
        _ => "auto"
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh-CN",
        LangEnum.ChineseTraditional => "zh-TW",
        LangEnum.Cantonese => "yue",
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
        LangEnum.Uyghur => "ug",
        _ => "auto"
    };

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

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

        var translatedText = await TranslateTextAsync(
            request.Text,
            sourceStr,
            targetStr,
            cancellationToken);

        result.Success(translatedText);
    }

    internal Task<string> TranslateTextAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        return Settings.RequestMode switch
        {
            RequestMode.Direct => TranslateDirectAsync(text, sourceLanguage, targetLanguage, cancellationToken),
            _ => TranslateWithCustomApiAsync(text, sourceLanguage, targetLanguage, cancellationToken)
        };
    }

    private async Task<string> TranslateWithCustomApiAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(Settings.ApiUrl, UriKind.Absolute, out var apiUri)
            || apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(Context.GetTranslation("STranslate_Plugin_Translate_GoogleBuiltIn_ApiUrl_Invalid"));
        }

        var content = new
        {
            text,
            source_lang = sourceLanguage,
            target_lang = targetLanguage
        };

        var response = await Context.HttpService.PostAsync(apiUri.ToString(), content, null, cancellationToken);
        using var jsonDocument = JsonDocument.Parse(response);

        if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement)
            && dataElement.ValueKind == JsonValueKind.String)
        {
            var translatedText = dataElement.GetString();
            if (!string.IsNullOrWhiteSpace(translatedText))
                return translatedText;
        }

        throw new Exception($"No result.\nRaw: {response}");
    }

    private async Task<string> TranslateDirectAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var options = new Options
        {
            QueryParams = new Dictionary<string, string>
            {
                { "client", "gtx" },
                { "dt", "t" },
                { "dj", "1" },
                { "ie", "UTF-8" },
                { "oe", "UTF-8" },
                { "sl", sourceLanguage.ToLowerInvariant() },
                { "tl", targetLanguage.ToLowerInvariant() },
                { "q", text }
            }
        };

        var response = await Context.HttpService.GetAsync(GoogleTranslateUrl, options, cancellationToken);
        using var jsonDocument = JsonDocument.Parse(response);

        if (!jsonDocument.RootElement.TryGetProperty("sentences", out var sentencesElement)
            || sentencesElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception($"No result.\nRaw: {response}");
        }

        var translatedParts = sentencesElement
            .EnumerateArray()
            .Select(sentence => sentence.TryGetProperty("trans", out var translation)
                ? translation.GetString() ?? string.Empty
                : string.Empty);
        var translatedText = string.Concat(translatedParts);

        if (string.IsNullOrWhiteSpace(translatedText))
            throw new Exception($"No result.\nRaw: {response}");

        return translatedText;
    }
}
