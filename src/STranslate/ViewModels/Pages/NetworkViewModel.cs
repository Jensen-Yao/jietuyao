using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.ViewModels.Pages;

public partial class NetworkViewModel : SearchViewModelBase
{
    private readonly IHttpService _httpService;
    private readonly Internationalization _i18n;
    private readonly ILogger<NetworkViewModel> _logger;
    private readonly ExternalCallService _externalCallService;
    private readonly Action<string> _externalActionHandler;

    [ObservableProperty]
    public partial string TestResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTesting { get; set; } = false;

    [ObservableProperty] public partial string ExternalServiceStatus { get; set; } = string.Empty;

    public Settings Settings { get; }
    public DataProvider DataProvider { get; }

    public NetworkViewModel(
        Settings settings,
        DataProvider dataProvider,
        IHttpService httpService,
        Internationalization i18n,
        ExternalCallService externalCallService,
        ILogger<NetworkViewModel> logger) : base(i18n, "Network_")
    {
        Settings = settings;
        DataProvider = dataProvider;
        _httpService = httpService;
        _i18n = i18n;
        _logger = logger;
        _externalCallService = externalCallService;

        _externalActionHandler = msg => ExternalServiceStatus = msg;
        _externalCallService.OnActionOccurred += _externalActionHandler;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsTesting) return;

        IsTesting = true;
        TestResult = _i18n.GetTranslation("ProxyTesting");

        try
        {
            var isConnected = await _httpService.TestProxyAsync();
            if (isConnected)
            {
                var ip = await _httpService.GetCurrentIpAsync();
                TestResult = string.Format(_i18n.GetTranslation("ProxyTestSuccess"), ip);
            }
            else
            {
                TestResult = _i18n.GetTranslation("ProxyTestFailed");
            }
        }
        catch (Exception ex)
        {
            TestResult = string.Format(_i18n.GetTranslation("ProxyTestError"), ex.Message);
            _logger.LogError(ex, "代理连接测试失败");
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        Settings.Proxy.IsEnabled = true;
        Settings.Proxy.ProxyType = ProxyType.System;
        Settings.Proxy.ProxyAddress = "127.0.0.1";
        Settings.Proxy.ProxyPort = 8080;
        Settings.Proxy.ProxyUsername = string.Empty;
        Settings.Proxy.ProxyPassword = string.Empty;
        TestResult = string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _externalCallService.OnActionOccurred -= _externalActionHandler;
        }

        base.Dispose(disposing);
    }
}
