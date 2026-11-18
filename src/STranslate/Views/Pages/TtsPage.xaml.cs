using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class TtsPage
{
    public TtsPage(TtsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public TtsViewModel ViewModel { get; }
}