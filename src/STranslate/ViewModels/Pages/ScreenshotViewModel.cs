using STranslate.Core;

namespace STranslate.ViewModels.Pages;

public partial class ScreenshotViewModel(Settings settings)
{
    public Settings Settings { get; } = settings;
}
