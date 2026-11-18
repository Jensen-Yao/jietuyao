using CommunityToolkit.Mvvm.Input;
using STranslate.Controls;
using STranslate.Core;
using System.Windows.Input;

namespace STranslate.ViewModels.Pages;

public partial class HotkeyViewModel(
    HotkeySettings settings,
    Internationalization i18n,
    DataProvider dataProvider
    ) : SearchViewModelBase(i18n, "Hotkey_")
{
    public HotkeySettings HotkeySettings { get; } = settings;

    public DataProvider DataProvider { get; } = dataProvider;

    public Internationalization I18n { get; } = i18n;

    [RelayCommand]
    private async Task IncrementalTranslateKeyAsync()
    {
        var cache = HotkeySettings.IncrementalTranslateKey;
        var dialog = new HotkeyControlDialog(
            HotkeyType.Global,
            cache.ToString(),
            "None",
            I18n.GetTranslation("Hotkey_IncrementalTranslate"),
            singleKeyMode: true);

        // remove first
        HotkeySettings.IncrementalTranslateKey = Key.None;

        await dialog.ShowAsync();
        if (dialog.ReturnType == HotkeyControlDialog.HkReturnType.Save)
        {
            HotkeySettings.IncrementalTranslateKey = dialog.ResultValue switch
            {
                "Space" => Key.Space,
                "~" => Key.Oem3,
                _ => Enum.Parse<Key>(dialog.ResultValue)
            };
        }
        else if (dialog.ReturnType == HotkeyControlDialog.HkReturnType.Delete)
            HotkeySettings.IncrementalTranslateKey = Key.None;
        else
            HotkeySettings.IncrementalTranslateKey = cache;
    }
}