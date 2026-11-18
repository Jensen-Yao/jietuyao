using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.PpOnnx.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        DetModelPath = Settings.DetModelPath;
        RecModelPath = Settings.RecModelPath;
        Threads = Settings.Threads;
        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DetModelPath): Settings.DetModelPath = DetModelPath; break;
            case nameof(RecModelPath): Settings.RecModelPath = RecModelPath; break;
            case nameof(Threads): Settings.Threads = Math.Clamp(Threads, 1, 8); break;
            default: return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string DetModelPath { get; set; }
    [ObservableProperty] public partial string RecModelPath { get; set; }
    [ObservableProperty] public partial int Threads { get; set; }
}
