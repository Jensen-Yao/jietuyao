using STranslate.Plugin.Translate.Azure.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.Azure;

public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    private const string DefaultUrl = "https://api.cognitive.microsofttranslator.com/translate";

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "",
        LangEnum.ChineseSimplified => "zh-Hans",
        LangEnum.ChineseTraditional => "zh-Hant",
        LangEnum.Cantonese => "zh-Hant",
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
        LangEnum.MongolianCyrillic => "mn-MN",
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => "km",
        LangEnum.NorwegianBokmal => "nb",
        LangEnum.NorwegianNynorsk => "nn",
        LangEnum.Persian => "fa",
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => "ug",
        _ => ""
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => GetSourceLanguage(langEnum);

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        var sourceStr = GetSourceLanguage(request.SourceLang);
        var targetStr = GetTargetLanguage(request.TargetLang);
        if (targetStr is null || targetStr.Length == 0)
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        var baseUrl = string.IsNullOrWhiteSpace(Settings.Endpoint)
            ? DefaultUrl
            : Settings.Endpoint.TrimEnd('/') + "/translate";

        var queryParams = new Dictionary<string, string>
        {
            ["api-version"] = "3.0",
            ["to"] = targetStr,
        };
        if (!string.IsNullOrEmpty(sourceStr))
            queryParams["from"] = sourceStr;

        var options = new Options
        {
            QueryParams = queryParams,
            Headers = new Dictionary<string, string>
            {
                ["Ocp-Apim-Subscription-Key"] = Settings.ApiKey,
            },
        };
        if (!string.IsNullOrWhiteSpace(Settings.Region))
            options.Headers!["Ocp-Apim-Subscription-Region"] = Settings.Region;

        var payload = new[] { new { Text = request.Text } };
        var response = await Context.HttpService.PostAsync(baseUrl, payload, options, cancellationToken);

        var parsed = JsonNode.Parse(response);
        if (parsed is not JsonArray array || array.Count == 0)
        {
            result.Fail($"Invalid response\nRaw:{response}");
            return;
        }

        var error = array[0]?["error"];
        if (error != null)
        {
            result.Fail($"{error["code"]}: {error["message"]}");
            return;
        }

        var data = string.Join(Environment.NewLine,
            (array[0]?["translations"] as JsonArray)?
                .Select(t => t?["text"]?.ToString())
                .Where(t => !string.IsNullOrEmpty(t)) ?? []);
        if (data.Length == 0)
        {
            result.Fail($"No result\nRaw:{response}");
            return;
        }

        var detected = array[0]?["detectedLanguage"]?["language"]?.ToString();
        result.Success(data);
    }
}
