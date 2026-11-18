using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class StandalonePage
{
    public StandalonePage(StandaloneViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public StandaloneViewModel ViewModel { get; }
}