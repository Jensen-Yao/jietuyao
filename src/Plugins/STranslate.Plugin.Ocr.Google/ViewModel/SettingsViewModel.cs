using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Google.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        Url = settings.Url;
        ApiKey = settings.ApiKey;

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Url):
                _settings.Url = Url;
                break;
            case nameof(ApiKey):
                _settings.ApiKey = ApiKey;
                break;
            default:
                return;
        }
        _context.SaveSettingStorage<Settings>();
    }

    [ObservableProperty] public partial string Url { get; set; }
    [ObservableProperty] public partial string ApiKey { get; set; }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;
}
