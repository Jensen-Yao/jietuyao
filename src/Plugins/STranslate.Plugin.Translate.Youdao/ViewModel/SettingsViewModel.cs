using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Youdao.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    [ObservableProperty] public partial string AppKey { get; set; }
    [ObservableProperty] public partial string AppSecret { get; set; }

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        AppKey = settings.AppKey;
        AppSecret = settings.AppSecret;

        PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppKey))
        {
            _settings.AppKey = AppKey;
        }
        else if (e.PropertyName == nameof(AppSecret))
        {
            _settings.AppSecret = AppSecret;
        }
        _context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= OnSettingsPropertyChanged;
}
