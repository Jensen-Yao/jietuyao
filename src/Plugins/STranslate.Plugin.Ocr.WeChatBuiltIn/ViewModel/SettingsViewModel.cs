using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Plugin.Ocr.WeChatBuiltIn.ViewModel;

public partial class SettingsViewModel(IPluginContext context, Settings settings) : ObservableObject
{
    [ObservableProperty] public partial long UseCount { get; set; } = settings.UseCount;

    partial void OnUseCountChanged(long value)
    {
        settings.UseCount = value;
        context.SaveSettingStorage<Settings>();
    }
}