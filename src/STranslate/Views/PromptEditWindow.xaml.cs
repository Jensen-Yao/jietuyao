using STranslate.Helpers;
using STranslate.Plugin;
using STranslate.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace STranslate.Views;

public partial class PromptEditWindow
{
    private readonly PromptEditViewModel _viewModel;
    private bool _isDisposed;

    public PromptEditWindow(ObservableCollection<Prompt> prompts, List<string>? roles = default)
    {
        InitializeComponent();

        _viewModel = new PromptEditViewModel(prompts, roles);
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!e.Cancel)
            ModernWindowLifecycle.DetachModernWindowStyle(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        // 统一释放 VM：无论用户走 Save / Cancel / 标题栏 X / Alt+F4 哪条关闭路径，
        // 都在此处释放一次，取消 Prompts/PromptItems.CollectionChanged 与
        // 各 Prompt.PropertyChanged 订阅，避免被单例持有的 Prompt 集合反向钉住 VM。
        ModernWindowLifecycle.Release(this, ReleaseViewModel);
        base.OnClosed(e);
    }

    private void ReleaseViewModel()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _viewModel.Dispose();
    }}
