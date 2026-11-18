using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class VocabularyPage
{
    public VocabularyPage(VocabularyViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public VocabularyViewModel ViewModel { get; }
}