using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Amazon.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        Region = Settings.Region;
        AccessKeyId = Settings.AccessKeyId;
        SecretAccessKey = Settings.SecretAccessKey;
        SessionToken = Settings.SessionToken;
        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Region): Settings.Region = Region; break;
            case nameof(AccessKeyId): Settings.AccessKeyId = AccessKeyId; break;
            case nameof(SecretAccessKey): Settings.SecretAccessKey = SecretAccessKey; break;
            case nameof(SessionToken): Settings.SessionToken = SessionToken; break;
            default: return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string Region { get; set; }
    [ObservableProperty] public partial string AccessKeyId { get; set; }
    [ObservableProperty] public partial string SecretAccessKey { get; set; }
    [ObservableProperty] public partial string SessionToken { get; set; }
}
