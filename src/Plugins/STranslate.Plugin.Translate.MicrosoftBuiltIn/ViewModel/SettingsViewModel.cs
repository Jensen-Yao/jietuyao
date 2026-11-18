using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.MicrosoftBuiltIn.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    /// <summary>
    /// 初始化设置视图模型并绑定持久化存储。
    /// </summary>
    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;
        RequestMode = settings.RequestMode;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RequestMode))
            return;

        _settings.RequestMode = RequestMode;
        _context.SaveSettingStorage<Settings>();
    }

    [ObservableProperty] public partial RequestMode RequestMode { get; set; }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;
}
