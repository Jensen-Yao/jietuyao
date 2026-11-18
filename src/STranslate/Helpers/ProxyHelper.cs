using Microsoft.Extensions.Logging;
using STranslate.Core;
using System.Net;
using System.Net.Http;

namespace STranslate.Helpers;

public class ProxyHelper
{
    /// <summary>
    /// 根据代理设置创建WebProxy实例
    /// </summary>
    public static IWebProxy? CreateWebProxy(ProxySettings proxySettings)
    {
        if (!proxySettings.IsEnabled)
            return null;

        return proxySettings.ProxyType switch
        {
            ProxyType.System => HttpClient.DefaultProxy,
            ProxyType.None => new WebProxy(),
            ProxyType.Http or ProxyType.Socks5 => CreateCustomProxy(proxySettings),
            _ => null
        };
    }

    /// <summary>
    /// 创建自定义代理实例
    /// </summary>
    private static WebProxy CreateCustomProxy(ProxySettings proxySettings)
    {
        var proxyUri = new Uri(proxySettings.GetProxyUri());
        var proxy = new WebProxy(proxyUri)
        {
            UseDefaultCredentials = string.IsNullOrEmpty(proxySettings.ProxyUsername),
            BypassProxyOnLocal = !proxySettings.UseProxyForLocalAddresses
        };

        // 设置身份验证
        if (!string.IsNullOrEmpty(proxySettings.ProxyUsername))
        {
            proxy.Credentials = new NetworkCredential(
                proxySettings.ProxyUsername, 
                proxySettings.ProxyPassword);
        }

        // 设置绕过列表
        if (!string.IsNullOrEmpty(proxySettings.BypassList))
        {
            var bypassList = proxySettings.BypassList
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            proxy.BypassList = bypassList;
        }

        return proxy;
    }

    /// <summary>
    /// 创建配置了代理的SocketsHttpHandler
    /// </summary>
    public static SocketsHttpHandler CreateHttpHandler(ProxySettings proxySettings)
    {
        CommunityToolkit.Mvvm.DependencyInjection
            .Ioc.Default.GetRequiredService<ILogger<ProxyHelper>>()
            .LogDebug("创建HttpHandler: {@ProxySettings}", new
            {
                proxySettings.IsEnabled,
                proxySettings.ProxyType,
                ProxyAddress = proxySettings.ProxyAddress ?? "未设置",
                proxySettings.ProxyPort,
                ProxyUsername = string.IsNullOrEmpty(proxySettings.ProxyUsername) ? "未设置" : proxySettings.ProxyUsername,
                proxySettings.UseProxyForLocalAddresses,
                BypassList = string.IsNullOrEmpty(proxySettings.BypassList) ? "未设置" : proxySettings.BypassList
            });

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            MaxConnectionsPerServer = 20,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        if (!proxySettings.IsEnabled || proxySettings.ProxyType == ProxyType.None)
        {
            handler.UseProxy = false;
            return handler;
        }

        var proxy = CreateWebProxy(proxySettings);
        if (proxy == null)
        {
            handler.UseProxy = false;
            return handler;
        }

        handler.UseProxy = true;
        handler.Proxy = proxy;

        return handler;
    }

    /// <summary>
    /// 测试代理连接
    /// </summary>
    public static async Task<bool> TestProxyConnectionAsync(ProxySettings proxySettings, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var handler = CreateHttpHandler(proxySettings);
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            // 测试连接到一个可靠的服务
            var response = await client.GetAsync("https://httpbin.org/ip", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取当前IP地址（用于测试代理是否生效）
    /// </summary>
    public static async Task<string> GetCurrentIpAsync(ProxySettings proxySettings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var handler = CreateHttpHandler(proxySettings);
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var response = await client.GetStringAsync("https://httpbin.org/ip", cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}