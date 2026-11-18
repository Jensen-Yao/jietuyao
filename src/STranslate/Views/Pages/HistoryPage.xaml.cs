using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class HistoryPage
{
    public HistoryPage(HistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public HistoryViewModel ViewModel { get; }
}