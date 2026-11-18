using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Core;

public partial class ProxySettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial ProxyType ProxyType { get; set; } = ProxyType.System;

    [ObservableProperty]
    public partial string ProxyAddress { get; set; } = "127.0.0.1";

    [ObservableProperty]
    public partial int ProxyPort { get; set; } = 8080;

    [ObservableProperty]
    public partial string ProxyUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProxyPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UseProxyForLocalAddresses { get; set; } = false;

    [ObservableProperty]
    public partial string BypassList { get; set; } = "localhost;127.*;10.*;192.168.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*";

    /// <summary>
    /// 获取代理URI字符串
    /// </summary>
    public string GetProxyUri()
    {
        if (!IsEnabled || ProxyType == ProxyType.System)
            return string.Empty;

        var scheme = ProxyType switch
        {
            ProxyType.Http => "http",
            ProxyType.Socks5 => "socks5",
            _ => "http"
        };

        if (string.IsNullOrEmpty(ProxyUsername))
        {
            return $"{scheme}://{ProxyAddress}:{ProxyPort}";
        }

        return $"{scheme}://{ProxyUsername}:{ProxyPassword}@{ProxyAddress}:{ProxyPort}";
    }
}

public enum ProxyType
{
    /// <summary>
    /// 系统代理
    /// </summary>
    System,
    
    /// <summary>
    /// HTTP代理
    /// </summary>
    Http,
    
    /// <summary>
    /// Socks5代理
    /// </summary>
    Socks5,
    
    /// <summary>
    /// 无代理
    /// </summary>
    None
}