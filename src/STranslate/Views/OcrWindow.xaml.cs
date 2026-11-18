using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using STranslate.Helpers;
using STranslate.ViewModels;
using System.ComponentModel;
using System.Diagnostics;

namespace STranslate.Views;

public partial class OcrWindow
{
    private readonly OcrWindowViewModel _viewModel;
    private readonly IServiceScope _serviceScope;

    public OcrWindow()
    {
        _serviceScope = Ioc.Default.CreateScope();
        try
        {
            _viewModel = _serviceScope.ServiceProvider.GetRequiredService<OcrWindowViewModel>();
            DataContext = _viewModel;

            InitializeComponent();
        }
        catch
        {
            _serviceScope.Dispose();
            throw;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelOperations();
        base.OnClosing(e);

        if (!e.Cancel)
            ModernWindowLifecycle.DetachModernWindowStyle(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        // VM 由独立 DI scope 持有，释放 scope 会触发 ViewModel.Dispose()，
        // 取消对 OcrService/Settings 等单例的事件订阅。
        ModernWindowLifecycle.Release(this, _serviceScope.Dispose);
        base.OnClosed(e);
    }

    private void OpenHyperlink(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        var link = e.Parameter.ToString();
        if (string.IsNullOrWhiteSpace(link))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = link,
            UseShellExecute = true
        });
    }
}
