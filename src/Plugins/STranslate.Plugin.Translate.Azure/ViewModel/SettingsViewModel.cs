using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Azure.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        ApiKey = Settings.ApiKey;
        Region = Settings.Region;
        Endpoint = Settings.Endpoint;
        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ApiKey): Settings.ApiKey = ApiKey; break;
            case nameof(Region): Settings.Region = Region; break;
            case nameof(Endpoint): Settings.Endpoint = Endpoint; break;
            default: return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string ApiKey { get; set; }
    [ObservableProperty] public partial string Region { get; set; }
    [ObservableProperty] public partial string Endpoint { get; set; }
}
