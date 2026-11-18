using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.OcrSpace.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    [ObservableProperty] public partial string ApiKey { get; set; }

    [ObservableProperty] public partial OcrSpaceEngine Engine { get; set; }

    public List<OcrSpaceEngine> EngineOptions { get; } = [OcrSpaceEngine.Engine1, OcrSpaceEngine.Engine2, OcrSpaceEngine.Engine3];

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        ApiKey = settings.ApiKey;
        Engine = settings.Engine;

        PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ApiKey):
                _settings.ApiKey = ApiKey;
                break;
            case nameof(Engine):
                _settings.Engine = Engine;
                break;
            default:
                return;
        }
        _context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= OnSettingsPropertyChanged;
}
