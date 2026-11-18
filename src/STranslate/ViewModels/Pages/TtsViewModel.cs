using STranslate.Services;

namespace STranslate.ViewModels.Pages;

public partial class TtsViewModel(TtsService service) : ServiceViewModelBase<TtsService>(service)
{
}