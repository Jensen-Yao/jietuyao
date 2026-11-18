using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Baidu.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;

        AppID = Settings.AppID;
        AppKey = Settings.AppKey;

        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppID):
                Settings.AppID = AppID;
                break;
            case nameof(AppKey):
                Settings.AppKey = AppKey;
                break;
            default:
                return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string AppID { get; set; }
    [ObservableProperty] public partial string AppKey { get; set; }
}