using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Core;
using STranslate.Plugin;
using System.Collections.ObjectModel;

namespace STranslate.Services;

public partial class PluginService : ObservableObject
{
    private readonly PluginManager _pluginManager;

    [ObservableProperty] public partial ObservableCollection<PluginMetaData> PluginMetaDatas { get; set; } = [];

    public PluginService(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        foreach (var plugin in pluginManager.AllPluginMetaDatas)
        {
            PluginMetaDatas.Add(plugin);
        }
    }

    internal PluginInstallResult InstallPlugin(string spkgPluginFilePath)
    {
        var result = _pluginManager.InstallPlugin(spkgPluginFilePath);
        if (result.Succeeded && result.NewPlugin != null)
        {
            PluginMetaDatas.Add(result.NewPlugin);
        }

        return result;
    }

    internal bool UpgradePlugin(PluginMetaData oldPlugin, string spkgFilePath)
    {
        return _pluginManager.UpgradePlugin(oldPlugin, spkgFilePath);
    }

    internal bool UninstallPlugin(PluginMetaData pluginMetaData)
    {
        // ServiceManager卸载（考虑到各个实例的清除，本来ServiceManager里就有加载配置清理无效服务，先不管这部分了

        // PluginManager卸载
        var result = _pluginManager.UninstallPlugin(pluginMetaData);
        if (result)
        {
            App.Current.Dispatcher.Invoke(() => PluginMetaDatas.Remove(pluginMetaData));
        }
        return result;
    }
}