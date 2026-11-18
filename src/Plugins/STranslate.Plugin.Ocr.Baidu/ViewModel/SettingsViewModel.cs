using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Baidu.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        Action = settings.Action;
        ApiKey = settings.ApiKey;
        SecretKey = settings.SecretKey;

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Action):
                _settings.Action = Action;
                break;
            case nameof(ApiKey):
                _settings.ApiKey = ApiKey;
                break;
            case nameof(SecretKey):
                _settings.SecretKey = SecretKey;
                break;
            default:
                return;
        }
        _context.SaveSettingStorage<Settings>();
    }

    public List<BaiduOCRAction> Actions => [.. Enum.GetValues<BaiduOCRAction>()];

    [ObservableProperty] public partial BaiduOCRAction Action { get; set; }
    [ObservableProperty] public partial string ApiKey { get; set; }
    [ObservableProperty] public partial string SecretKey { get; set; }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;
}