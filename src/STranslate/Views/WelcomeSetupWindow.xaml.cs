using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using STranslate.Helpers;
using STranslate.ViewModels;
using System.ComponentModel;

namespace STranslate.Views;

public partial class WelcomeSetupWindow
{
    private readonly WelcomeSetupViewModel _viewModel;
    private readonly IServiceScope _serviceScope;

    public WelcomeSetupWindow()
    {
        _serviceScope = Ioc.Default.CreateScope();
        try
        {
            _viewModel = _serviceScope.ServiceProvider.GetRequiredService<WelcomeSetupViewModel>();
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
        _viewModel.SaveAll();
        base.OnClosing(e);

        if (!e.Cancel)
        {
            // 先断开插件设置 UI 的引用，再拆除 titlebar。
            // 插件 Main 单例会用 ??= 缓存 GetSettingUI() 返回的控件，该控件被 ContentControl
            // 嵌入本窗口视觉树后，其依赖属性(Parent/InheritanceContext)会反向引用窗口。
            // 若不先置空，Main._settingUi 持有的控件会把已关闭窗口钉死无法 GC。
            _viewModel.TranslateSettingUI = null;
            _viewModel.OcrSettingUI = null;

            ModernWindowLifecycle.DetachModernWindowStyle(this);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // VM 由独立 DI scope 持有，释放 scope 会触发 WelcomeSetupViewModel.Dispose()，
        // 取消 CollectionChanged 订阅并脱离 root provider 跟踪。
        ModernWindowLifecycle.Release(this, _serviceScope.Dispose);
        base.OnClosed(e);
    }}
