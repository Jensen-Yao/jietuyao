using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Tts.MicrosoftEdge.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    [ObservableProperty]
    public partial string Url { get; set; }

    [ObservableProperty]
    public partial string Voice { get; set; }

    [ObservableProperty]
    public partial double Speed { get; set; }

    [ObservableProperty]
    public partial int Pitch { get; set; }

    [ObservableProperty]
    public partial string Style { get; set; }

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        Url = settings.Url;
        Voice = settings.Voice;
        Speed = settings.Speed;
        Pitch = settings.Pitch;
        Style = settings.Style;

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Url):
                _settings.Url = Url;
                break;
            case nameof(Voice):
                _settings.Voice = Voice;
                break;
            case nameof(Speed):
                _settings.Speed = Math.Round(Speed, 1);
                break;
            case nameof(Pitch):
                _settings.Pitch = Pitch;
                break;
            case nameof(Style):
                _settings.Style = Style;
                break;
        }

        _context.SaveSettingStorage<Settings>();
    }

    public void Dispose()
    {
        PropertyChanged -= OnPropertyChanged;
    }
}