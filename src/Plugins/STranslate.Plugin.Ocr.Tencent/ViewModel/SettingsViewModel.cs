using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Tencent.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        Action = settings.Action;
        SecretId = settings.SecretId;
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
            case nameof(SecretId):
                _settings.SecretId = SecretId;
                break;
            case nameof(SecretKey):
                _settings.SecretKey = SecretKey;
                break;
            default:
                return;
        }
        _context.SaveSettingStorage<Settings>();
    }

    public List<TencentOCRAction> Actions => [.. Enum.GetValues<TencentOCRAction>()];

    [ObservableProperty] public partial TencentOCRAction Action { get; set; }
    [ObservableProperty] public partial string SecretId { get; set; }
    [ObservableProperty] public partial string SecretKey { get; set; }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;
}
