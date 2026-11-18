using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class OcrPage
{
    public OcrPage(OcrViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public OcrViewModel ViewModel { get; }
}