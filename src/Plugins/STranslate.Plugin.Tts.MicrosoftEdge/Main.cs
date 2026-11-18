using STranslate.Plugin.Tts.MicrosoftEdge.View;
using STranslate.Plugin.Tts.MicrosoftEdge.ViewModel;
using System.Windows.Controls;

namespace STranslate.Plugin.Tts.MicrosoftEdge;

public class Main : ITtsPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() => _viewModel?.Dispose();

    public async Task PlayAudioAsync(string text, CancellationToken cancellationToken = default)
    {
        var content = new
        {
            input = text,
            voice = Settings.Voice,
            speed = Settings.Speed,
            pitch = Settings.Pitch.ToString(),
            style = Settings.Style
        };
        var response = await Context.HttpService.PostAsBytesAsync(Settings.Url, content, cancellationToken: cancellationToken);
        await Context.AudioPlayer.PlayAsync(response, cancellationToken);
    }
}