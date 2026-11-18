using STranslate.Plugin;
using System.IO;

namespace STranslate.Core;

public class PluginStorage<T> : StorageBase<T>, IPluginSavable where T : new()
{
    public PluginStorage(PluginMetaData metaData, string serviceId)
    {
        DirectoryPath = metaData.PluginSettingsDirectoryPath;
        // 插件配置目录如果未配置可以不创建
        //EnsureDirectoryExists();

        FilePath = Path.Combine(DirectoryPath, $"{serviceId}{FileSuffix}");
    }

    public override void Save()
    {
        try
        {
            base.Save();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save ST settings to path: {FilePath}", e);
        }
    }

    public override async Task SaveAsync()
    {
        try
        {
            await base.SaveAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save ST settings to path: {FilePath}", e);
        }
    }

    /// <summary>
    /// 尝试删除未使用的插件设置目录
    /// </summary>
    public void Clean()
    {
        if (!Directory.Exists(DirectoryPath))
            return;

        if (Directory.EnumerateFileSystemEntries(DirectoryPath).Any())
            return;
        try
        {
            Directory.Delete(DirectoryPath);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to clean unused plugin settings directory: {DirectoryPath}", e);
        }
    }
}