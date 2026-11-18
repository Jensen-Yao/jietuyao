using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class HotkeyPage
{
    public HotkeyPage(HotkeyViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public HotkeyViewModel ViewModel { get; }
}