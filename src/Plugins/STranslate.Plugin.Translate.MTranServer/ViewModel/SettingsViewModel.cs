using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace STranslate.Plugin.Translate.MTranServer.ViewModel;

public partial class SettingsViewModel(IPluginContext context, Settings settings, Main main) : ObservableObject
{
    public Main Main { get; } = main;

    [ObservableProperty] public partial string Url { get; set; } = settings.Url;

    [ObservableProperty] public partial string ValidateResult { get; set; } = string.Empty;

    partial void OnUrlChanged(string value)
    {
        settings.Url = value;
        context.SaveSettingStorage<Settings>();
    }

    [RelayCommand]
    public async Task ValidateAsync()
    {
        try
        {
            var content = new
            {
                from = "en",
                to = "zh",
                text = "Hello world"
            };

            var response = await context.HttpService.PostAsync(settings.Url, content);

            // 解析Google翻译返回的JSON
            var jsonDoc = JsonDocument.Parse(response);
            var translatedText = jsonDoc.RootElement.GetProperty("result").GetString() ?? throw new Exception(response);

            ValidateResult = context.GetTranslation("ValidationSuccess");
        }
        catch (Exception ex)
        {
            ValidateResult = context.GetTranslation("ValidationFailure");
            context.Logger.LogError(ex, context.GetTranslation("ValidationFailure"));
        }
    }
}