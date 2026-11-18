using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class NetworkPage
{
    public NetworkPage(NetworkViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public NetworkViewModel ViewModel { get; }
}