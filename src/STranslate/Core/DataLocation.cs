using System.IO;

namespace STranslate.Core;

public class DataLocation
{
    public static readonly string PortableDataPath =
        Path.Combine(Constant.ProgramDirectory, Constant.PortableFolderName);

    public static readonly string RoamingDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constant.AppName);

    public static readonly string AppExePath = Path.Combine(Constant.ProgramDirectory, Constant.AppName + ".exe");
    public static readonly string HostExePath = Path.Combine(Constant.ProgramDirectory, Constant.HostExeName);
    public static readonly string StartupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    public static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public static readonly string StartupShortcutPath = Path.Combine(StartupPath, Constant.AppName + ".lnk");
    public static readonly string DesktopShortcutPath = Path.Combine(DesktopPath, Constant.AppName + ".lnk");
    public static readonly string InfoFilePath = Path.Combine(Constant.ProgramDirectory, Constant.InfoFileName);
    public static readonly string BackupFilePath = Path.Combine(Constant.ProgramDirectory, Constant.BackupFileName);
    public static readonly string TmpConfigDirectory = Path.Combine(Path.GetTempPath(), Constant.TmpConfigFolderName);

    public static string VersionLogDirectory => Path.Combine(LogDirectory, Constant.Version);
    public static string LogDirectory => Path.Combine(DataDirectory(), Constant.Logs);
    public static readonly string CacheDirectory = Path.Combine(DataDirectory(), Constant.Cache);
    public static readonly string SettingsDirectory = Path.Combine(DataDirectory(), Constant.Settings);
    public static readonly string PluginSettingsDirectory = Path.Combine(SettingsDirectory, Constant.Plugins);
    public static readonly string PluginCacheDirectory = Path.Combine(DataDirectory(), Constant.Cache, Constant.Plugins);
    public static readonly string PluginsDirectory = Path.Combine(DataDirectory(), Constant.Plugins);

    public static readonly string DbConnectionString = $"Data Source={CacheDirectory}\\history.db";

    /// <summary>
    ///     Plugin搜索目录
    /// </summary>
    public static readonly string[] PluginDirectories = [Constant.PreinstalledDirectory, PluginsDirectory];

    public static string DataDirectory() => PortableDataLocationInUse() ? PortableDataPath : RoamingDataPath;

    public static bool PortableDataLocationInUse() => Directory.Exists(PortableDataPath);
}