using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.WindowsOcr.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        LanguageTag = Settings.LanguageTag;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LanguageTag)) return;
        Settings.LanguageTag = LanguageTag?.Trim() ?? string.Empty;
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;

    [ObservableProperty] public partial string LanguageTag { get; set; }
}
