using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.OpenAI.View;
using STranslate.Plugin.Ocr.OpenAI.ViewModel;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.OpenAI;

public class Main : ObservableObject, IOcrPlugin, ILlm
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages => Enum.GetValues<LangEnum>();

    public ObservableCollection<Prompt> Prompts { get; set; } = [];

    public Prompt? SelectedPrompt
    {
        get => Prompts.FirstOrDefault(p => p.IsEnabled);
        set => SelectPrompt(value);
    }

    public void SelectPrompt(Prompt? prompt)
    {
        if (prompt == null) return;

        // 更新所有 Prompt 的 IsEnabled 状态
        foreach (var p in Prompts)
        {
            p.IsEnabled = p == prompt;
        }

        OnPropertyChanged(nameof(SelectedPrompt));

        // 保存到配置
        Settings.Prompts = [.. Prompts.Select(p => p.Clone())];
        Context.SaveSettingStorage<Settings>();
    }

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings, this);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();

        // 加载 Prompt 列表
        Settings.Prompts.ForEach(Prompts.Add);
    }

    public void Dispose() => _viewModel?.Dispose();

    public string? GetLanguage(LangEnum langEnum) => null;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        var url = UrlHelper.BuildFinalUrl(Settings.Url);

        if (Context.ImageQuality == ImageQuality.High)
            return new OcrResult().Fail($"Not supported, please use {Context.GetTranslation("ImageQualityLow")} or {Context.GetTranslation("ImageQualityMedium")}");

        // 处理图片数据
        var base64Str = Convert.ToBase64String(request.ImageData);
        // https://www.volcengine.com/docs/82379/1362931#%E5%9B%BE%E7%89%87%E6%A0%BC%E5%BC%8F%E8%AF%B4%E6%98%8E
        var formatStr = Context.ImageQuality switch
        {
            ImageQuality.Low => "image/jpeg",
            ImageQuality.Medium => "image/png",
            ImageQuality.High => "image/bmp",
            _ => "image/png"
        };

        // 选择模型
        var model = Settings.Model.Trim();
        model = string.IsNullOrEmpty(model) ? "gpt-4o" : model;

        // 替换Prompt关键字
        var messages = (Prompts.FirstOrDefault(x => x.IsEnabled) ?? throw new Exception("请先完善Prompt配置"))
            .Clone()
            .Items;
        messages.ToList()
            .ForEach(item =>
                item.Content = item.Content.Replace("$target", ConvertLanguage(request.Language)));

        // 温度限定
        var temperature = Math.Clamp(Settings.Temperature, 0, 2);
        var userPrompt = messages.LastOrDefault() ?? throw new Exception("Prompt配置为空");
        messages.Remove(userPrompt);
        var messages2 = new List<object>();
        foreach (var item in messages)
        {
            messages2.Add(new
            {
                role = item.Role,
                content = item.Content
            });
        }
        messages2.Add(new
        {
            role = "user",
            content = new object[]
            {
                new
                {
                    type = "text",
                    text = userPrompt.Content
                },
                new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:{formatStr};base64,{base64Str}"
                    }
                }
            }
        });

        var content = new
        {
            model,
            messages = messages2.ToArray(),
            temperature
        };

        var option = new Options
        {
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + Settings.ApiKey }
            }
        };

        var response = await Context.HttpService.PostAsync(url, content, option, cancellationToken);
        // 解析Google翻译返回的JSON
        var jsonDoc = JsonDocument.Parse(response);
        var rawData = jsonDoc
            .RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new Exception($"反序列化失败: {response}");

        var result = new OcrResult();
        foreach (var item in rawData.ToString().Split("\n").ToList().Select(item => new OcrContent { Text = item}))
        {
            result.OcrContents.Add(item);
        }

        return result;
    }

    private string ConvertLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "Requires you to identify automatically",
        LangEnum.ChineseSimplified => "Simplified Chinese",
        LangEnum.ChineseTraditional => "Traditional Chinese",
        LangEnum.Cantonese => "Cantonese",
        LangEnum.English => "English",
        LangEnum.Japanese => "Japanese",
        LangEnum.Korean => "Korean",
        LangEnum.French => "French",
        LangEnum.Spanish => "Spanish",
        LangEnum.Russian => "Russian",
        LangEnum.German => "German",
        LangEnum.Italian => "Italian",
        LangEnum.Turkish => "Turkish",
        LangEnum.PortuguesePortugal => "Portuguese",
        LangEnum.PortugueseBrazil => "Portuguese",
        LangEnum.Vietnamese => "Vietnamese",
        LangEnum.Indonesian => "Indonesian",
        LangEnum.Thai => "Thai",
        LangEnum.Malay => "Malay",
        LangEnum.Arabic => "Arabic",
        LangEnum.Hindi => "Hindi",
        LangEnum.MongolianCyrillic => "Mongolian",
        LangEnum.MongolianTraditional => "Mongolian",
        LangEnum.Khmer => "Central Khmer",
        LangEnum.NorwegianBokmal => "Norwegian Bokmål",
        LangEnum.NorwegianNynorsk => "Norwegian Nynorsk",
        LangEnum.Persian => "Persian",
        LangEnum.Swedish => "Swedish",
        LangEnum.Polish => "Polish",
        LangEnum.Dutch => "Dutch",
        LangEnum.Ukrainian => "Ukrainian",
        LangEnum.Uzbek => "Uzbek",
        LangEnum.Uyghur => "Uyghur",
        _ => "Requires you to identify automatically"
    };
}
