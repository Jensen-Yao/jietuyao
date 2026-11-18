using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;

namespace STranslate.Plugin;

/// <summary>
/// 表示插件的元数据，包括插件的标识、名称、作者、版本、描述、网站、执行文件路径、图标路径等信息。
/// </summary>
public partial class PluginMetaData : ObservableObject
{
    /// <summary>
    /// 插件唯一标识符。
    /// </summary>
    public string PluginID { get; set; } = string.Empty;

    /// <summary>
    /// 插件名称。
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// 插件作者。
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 插件版本号。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 插件描述信息。
    /// </summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>
    /// 插件官方网站。
    /// </summary>
    public string Website { get; set; } = string.Empty;

    /// <summary>
    /// 插件执行文件的完整路径。
    /// </summary>
    public string ExecuteFilePath { get; private set; } = string.Empty;

    /// <summary>
    /// 插件执行文件名。
    /// </summary>
    public string ExecuteFileName { get; set; } = string.Empty;

    /// <summary>
    /// 插件程序集名称。
    /// </summary>
    [JsonIgnore]
    public string AssemblyName { get; internal set; } = string.Empty;

    /// <summary>
    /// 插件类型（加载后设置）
    /// </summary>
    [JsonIgnore]
    public Type? PluginType { get; internal set; }

    private string _pluginDirectory = string.Empty;

    /// <summary>
    /// 插件源目录。
    /// </summary>
    public string PluginDirectory
    {
        get => _pluginDirectory;
        internal set
        {
            _pluginDirectory = value;
            ExecuteFilePath = Path.Combine(value, ExecuteFileName);
            IconPath = Path.Combine(value, IconPath);
        }
    }

    /// <summary>
    /// 插件图标路径。
    /// </summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否为预装插件
    /// </summary>
    public bool IsPrePlugin { get; set; } = false;

    /// <summary>
    /// 创建插件实例
    /// </summary>
    /// <returns>新的插件实例</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public IPlugin CreatePluginService()
    {
        _ = PluginType ?? throw new InvalidOperationException($"Plugin type not loaded for {Name}");

        return Activator.CreateInstance(PluginType) is not IPlugin plugin
            ? throw new InvalidOperationException($"Failed to create plugin instance for {Name}")
            : plugin;
    }

    /// <summary>
    /// 插件设置目录路径（未验证），用于存储插件设置文件和数据文件。
    /// 插件删除时，用户可选择是否保留该目录。
    /// </summary>
    public string PluginSettingsDirectoryPath { get; internal set; } = string.Empty;

    /// <summary>
    /// 插件缓存目录路径（未验证），用于存储缓存文件。
    /// 插件删除时，该目录会被删除。
    /// </summary>
    public string PluginCacheDirectoryPath { get; internal set; } = string.Empty;

    /// <summary>
    /// 将 <see cref="PluginMetaData"/> 转换为字符串，返回插件名称。
    /// </summary>
    /// <returns>插件名称字符串。</returns>
    public override string ToString() => Name;

    /// <summary>
    /// 创建当前插件元数据实例的副本。
    /// </summary>
    /// <returns>当前插件元数据的副本。</returns>
    public PluginMetaData Clone() => new()
    {
        PluginID = PluginID,
        Name = Name,
        Author = Author,
        Version = Version,
        Description = Description,
        Website = Website,
        ExecuteFileName = ExecuteFileName,
        AssemblyName = AssemblyName,
        PluginType = PluginType,
        IconPath = IconPath,
        IsPrePlugin = IsPrePlugin,
        PluginSettingsDirectoryPath = PluginSettingsDirectoryPath,
        PluginCacheDirectoryPath = PluginCacheDirectoryPath,
        PluginDirectory = PluginDirectory // 最后设置，因为它会影响其他路径属性
    };
}