using iNKORE.UI.WPF.Modern.Controls;
using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class GeneralPage : Page
{
    public GeneralPage(GeneralViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public GeneralViewModel ViewModel { get; }
}