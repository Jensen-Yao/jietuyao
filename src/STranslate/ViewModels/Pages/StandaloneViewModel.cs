using STranslate.Core;

namespace STranslate.ViewModels.Pages;

public partial class StandaloneViewModel(Settings settings, DataProvider dataProvider, Internationalization i18n)
    : SearchViewModelBase(i18n, "Standalone_")
{
    public Settings Settings { get; } = settings;

    public DataProvider DataProvider { get; } = dataProvider;
}
