using System.IO;

namespace STranslate.Plugin;

/// <summary>
/// Http请求选项
/// </summary>
public class Options
{
    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }
    
    /// <summary>
    /// 查询参数
    /// </summary>
    public Dictionary<string, string>? QueryParams { get; set; }
    
    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan? Timeout { get; set; }
    
    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = "application/json";
}

/// <summary>
/// 下载进度
/// </summary>
public class DownloadProgress
{
    /// <summary>
    /// 总字节数
    /// </summary>
    public long TotalBytes { get; set; }
    
    /// <summary>
    /// 已下载字节数
    /// </summary>
    public long DownloadedBytes { get; set; }
    
    /// <summary>
    /// 下载百分比
    /// </summary>
    public double Percentage => TotalBytes > 0 ? Math.Round((double)DownloadedBytes / TotalBytes * 100, 2) : 0;
    
    /// <summary>
    /// 已用时间
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }
    
    /// <summary>
    /// 下载速度
    /// </summary>
    public double Speed { get; set; } // bytes per second
}

/// <summary>
/// Http服务接口
/// </summary>
public interface IHttpService
{
    // GET 请求
    /// <summary>
    /// Get请求
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get请求
    /// </summary>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetAsync(string url, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get请求
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T?> GetAsync<T>(string url, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get请求返回字节数组
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> GetAsBytesAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get请求返回字节数组
    /// </summary>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> GetAsBytesAsync(string url, Options? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get请求返回流
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Stream> GetAsStreamAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get请求返回流
    /// </summary>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Stream> GetAsStreamAsync(string url, Options? options = null, CancellationToken cancellationToken = default);

    // POST 请求  
    /// <summary>
    /// Post请求
    /// </summary>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostAsync(string url, object content, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Post请求
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T?> PostAsync<T>(string url, object content, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Post请求返回字节数组
    /// </summary>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> PostAsBytesAsync(string url, object content, Options? options = null, CancellationToken cancellationToken = default);

    // Form 请求
    /// <summary>
    /// Post表单请求
    /// </summary>
    /// <param name="url"></param>
    /// <param name="formData"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostFormAsync(string url, Dictionary<string, string> formData, Options? options = null, CancellationToken cancellationToken = default);

    // 流式请求
    /// <summary>
    /// 流式Post请求
    /// </summary>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="onDataReceived"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StreamPostAsync(string url, object content, Action<string> onDataReceived, Options? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式Post请求（IAsyncEnumerable模式）
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="content">请求体</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步流，每项是一行SSE数据</returns>
    IAsyncEnumerable<string> StreamPostAsyncEnumerable(
        string url,
        object content,
        Options? options = null,
        CancellationToken cancellationToken = default);

    // 文件下载
    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url"></param>
    /// <param name="savePath"></param>
    /// <param name="fileName"></param>
    /// <param name="options"></param>
    /// <param name="progress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> DownloadFileAsync(string url, string savePath, string fileName, Options? options = null, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="savePath"></param>
    /// <param name="fileName"></param>
    /// <param name="options"></param>
    /// <param name="progress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> DownloadFileAsync(string url, object content, string savePath, string fileName, Options? options = null, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    // 使用指定服务名称的客户端
    /**
     * 
    配置分离：不同服务不同配置（BaseAddress、Timeout、Headers等）
    连接池隔离：避免不同服务之间的连接竞争
    策略分离：不同的重试、超时、熔断策略
    中间件分离：不同的认证、加密、日志处理
    资源管理：针对不同服务的连接数、性能调优
    监控分离：可以分别监控不同服务的调用情况
    */
    
    /// <summary>
    /// Get请求
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetAsync(string serviceName, string url, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get请求返回字节数组
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> GetAsBytesAsync(string serviceName, string url, Options? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get请求返回流
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Stream> GetAsStreamAsync(string serviceName, string url, Options? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post请求
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostAsync(string serviceName, string url, object content, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Post请求返回字节数组
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> PostAsBytesAsync(string serviceName, string url, object content, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Post表单请求
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="formData"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostFormAsync(string serviceName, string url, Dictionary<string, string> formData, Options? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 流式Post请求
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="onDataReceived"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StreamPostAsync(string serviceName, string url, object content, Action<string> onDataReceived, Options? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式Post请求（IAsyncEnumerable模式）
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="url">请求URL</param>
    /// <param name="content">请求体</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步流，每项是一行SSE数据</returns>
    IAsyncEnumerable<string> StreamPostAsyncEnumerable(
        string serviceName,
        string url,
        object content,
        Options? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="savePath"></param>
    /// <param name="fileName"></param>
    /// <param name="options"></param>
    /// <param name="progress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> DownloadFileAsync(string serviceName, string url, string savePath, string fileName, Options? options = null, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="url"></param>
    /// <param name="content"></param>
    /// <param name="savePath"></param>
    /// <param name="fileName"></param>
    /// <param name="options"></param>
    /// <param name="progress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> DownloadFileAsync(string serviceName, string url, object content, string savePath, string fileName, Options? options = null, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试代理连接
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> TestProxyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前IP地址
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetCurrentIpAsync(CancellationToken cancellationToken = default);
}
