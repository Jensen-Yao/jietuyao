using CommunityToolkit.Mvvm.Input;
using STranslate.Services;
using STranslate.Plugin;

namespace STranslate.ViewModels.Pages;

public partial class OcrViewModel(OcrService service) : ServiceViewModelBase<OcrService>(service)
{
    [RelayCommand]
    private void ActiveImTranOcr(Service svc) => Service.ActiveImTranOcr(svc);

    [RelayCommand]
    private void DeactiveImTranOcr() => Service.DeactiveImTranOcr();
}
