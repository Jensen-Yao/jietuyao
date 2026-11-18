using STranslate.Plugin;
using STranslate.ViewModels.Pages;
using STranslate.Views;

namespace STranslate.Tests;

/// <summary>
/// 验证设置窗口把服务选中状态应用到当前导航页面的 ViewModel。
/// </summary>
public class SettingsWindowNavigationTests
{
    /// <summary>
    /// 验证导航携带的服务会成为页面的当前选中项。
    /// </summary>
    [Fact]
    public void ApplySelectedServiceSetsSelectionOnNavigatedPageViewModel()
    {
        var service = new Service();
        var viewModel = new TestServiceSelectionViewModel();

        SettingsWindow.ApplySelectedService(viewModel, service);

        Assert.Same(service, viewModel.SelectedItem);
    }

    /// <summary>
    /// 验证普通导航不会清除页面已有的服务选中状态。
    /// </summary>
    [Fact]
    public void ApplySelectedServiceDoesNotClearExistingSelectionWhenNoServiceWasProvided()
    {
        var service = new Service();
        var viewModel = new TestServiceSelectionViewModel { SelectedItem = service };

        SettingsWindow.ApplySelectedService(viewModel, null);

        Assert.Same(service, viewModel.SelectedItem);
    }

    /// <summary>
    /// 验证受保护操作执行期间会进入导航状态，并在结束后恢复原状态。
    /// </summary>
    [Fact]
    public void RunWithNavigationProtectionSetsAndRestoresNavigationState()
    {
        var navigation = new TestNavigation();

        SettingsWindow.RunWithNavigationProtection(
            navigation,
            () => Assert.True(navigation.IsNavigated));

        Assert.False(navigation.IsNavigated);
    }

    /// <summary>
    /// 验证导航保护支持嵌套调用，并保留调用前已经存在的导航状态。
    /// </summary>
    [Fact]
    public void RunWithNavigationProtectionPreservesExistingStateWhenNested()
    {
        var navigation = new TestNavigation { IsNavigated = true };

        SettingsWindow.RunWithNavigationProtection(
            navigation,
            () => SettingsWindow.RunWithNavigationProtection(
                navigation,
                () => Assert.True(navigation.IsNavigated)));

        Assert.True(navigation.IsNavigated);
    }

    /// <summary>
    /// 验证受保护操作抛出异常时仍会恢复导航状态。
    /// </summary>
    [Fact]
    public void RunWithNavigationProtectionRestoresStateWhenActionThrows()
    {
        var navigation = new TestNavigation();

        Assert.Throws<InvalidOperationException>(() =>
            SettingsWindow.RunWithNavigationProtection(
                navigation,
                () => throw new InvalidOperationException("test")));

        Assert.False(navigation.IsNavigated);
    }

    private sealed class TestServiceSelectionViewModel : IServiceSelectionViewModel
    {
        public Service? SelectedItem { get; set; }
    }

    private sealed class TestNavigation : INavigation
    {
        public bool IsNavigated { get; set; }
    }
}
