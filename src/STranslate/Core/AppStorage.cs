using System.IO;

namespace STranslate.Core;

public class AppStorage<T> : StorageBase<T> where T : new()
{
    public AppStorage()
    {
        DirectoryPath = DataLocation.SettingsDirectory;
        EnsureDirectoryExists();

        var filename = typeof(T).Name;
        FilePath = Path.Combine(DirectoryPath, $"{filename}{FileSuffix}");
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
}