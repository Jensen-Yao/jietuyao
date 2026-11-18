using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class AboutPage
{
    public AboutPage(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public AboutViewModel ViewModel { get; }
}