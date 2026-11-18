using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace STranslate.Plugin;

/// <summary>
/// 服务实例 - 插件的运行时实例，包含具体的配置和状态
/// </summary>
public partial class Service : ObservableObject, IDisposable
{
    /// <summary>
    /// 服务实例唯一标识符
    /// </summary>
    public string ServiceID { get; internal set; } = string.Empty;

    /// <summary>
    /// 插件元数据引用
    /// </summary>
    public PluginMetaData MetaData { get; internal set; } = null!;

    /// <summary>
    /// 插件实例
    /// </summary>
    public IPlugin Plugin { get; set; } = null!;

    /// <summary>
    /// 插件上下文
    /// </summary>
    public IPluginContext Context { get; internal set; } = null!;

    /// <summary>
    /// 显示名称（可自定义）
    /// </summary>
    public string DisplayName
    {
        get => string.IsNullOrEmpty(field) ? MetaData.Name : field;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// 服务自定义图标绝对路径（为空时回退到插件默认图标）
    /// </summary>
    public string IconPath
    {
        get => string.IsNullOrEmpty(field) ? MetaData.IconPath : field;
        // 不使用 SetProperty：图标文件可能在路径不变时被覆盖（同扩展名替换），
        // 需要始终触发通知让 UI 重新加载。
        set
        {
            field = value;
            OnPropertyChanged(nameof(IconPath));
        }
    }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [ObservableProperty] public partial bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 特殊配置
    /// * 后续可根据需要通过泛型扩展成不同类型的配置
    /// </summary>
    [ObservableProperty] public partial TranslationOptions? Options { get; set; }

    partial void OnOptionsChanged(TranslationOptions? oldValue, TranslationOptions? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnOptionsPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnOptionsPropertyChanged;
        }
    }

    /// <summary>
    /// 将来自Options的属性变更通知冒泡到Service的订阅者
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(e);

    /// <summary>
    /// 初始化服务实例
    /// </summary>
    public void Initialize()
    {
        if (IsInitialized) return;

        Plugin.Init(Context);
        IsInitialized = true;
    }

    /// <summary>
    /// 字符串转换
    /// </summary>
    /// <returns></returns>
    public override string ToString() => DisplayName;

    /// <summary>
    /// 为了兼容现有代码，保持与 PluginPair 相同的 Equals 和 GetHashCode 逻辑
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj) =>
        obj is Service service &&
        service.MetaData.PluginID == MetaData.PluginID &&
        service.ServiceID == ServiceID;

    /// <summary>
    /// 获取哈希值
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => HashCode.Combine(MetaData.PluginID, ServiceID);

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        Options?.PropertyChanged -= OnOptionsPropertyChanged;

        // 删除服务配置文件
        Context.Dispose();

        // 释放插件资源
        Plugin.Dispose();

        GC.SuppressFinalize(this);
    }
}

public partial class TranslationOptions : ObservableObject
{
    [ObservableProperty] public partial ExecutionMode ExecMode { get; set; } = ExecutionMode.Automatic;
    [ObservableProperty]
    [JsonIgnore]
    public partial bool TemporaryDisplay { get; set; } = false;
    [ObservableProperty] public partial bool AutoBackTranslation { get; set; } = false;
}

/// <summary>
/// 翻译执行模式
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// 自动执行（默认）
    /// </summary>
    Automatic,

    /// <summary>
    /// 手动执行
    /// </summary>
    Manual,

    /// <summary>
    /// 钉住
    /// * 不显示在列表中
    /// * 手动执行
    /// </summary>
    Pinned,
}