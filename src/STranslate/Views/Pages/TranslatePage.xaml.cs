using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class TranslatePage
{
    public TranslatePage(TranslateViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public TranslateViewModel ViewModel { get; }
}