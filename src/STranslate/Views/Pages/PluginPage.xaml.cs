using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class PluginPage
{
    public PluginPage(PluginViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public PluginViewModel ViewModel { get; }
}