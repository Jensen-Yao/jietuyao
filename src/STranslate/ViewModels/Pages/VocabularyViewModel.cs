using STranslate.Services;

namespace STranslate.ViewModels.Pages;

public partial class VocabularyViewModel(VocabularyService service) : ServiceViewModelBase<VocabularyService>(service)
{
}