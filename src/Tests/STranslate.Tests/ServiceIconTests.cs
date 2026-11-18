using STranslate.Core;
using STranslate.Converters;
using STranslate.Plugin;

namespace STranslate.Tests;

public class ServiceIconTests
{
    [Fact]
    public void ServiceData_IconPath_DefaultsToNull()
    {
        var data = new ServiceData("svc1", "Test", true);
        Assert.Null(data.IconPath);
    }

    [Fact]
    public void Service_IconPath_FallsBackToMetaData_WhenEmpty()
    {
        var svc = new Service
        {
            MetaData = new PluginMetaData { IconPath = "/plugins/demo/icon.png" }
        };
        Assert.Equal("/plugins/demo/icon.png", svc.IconPath);
    }

    [Fact]
    public void Service_IconPath_UsesCustom_WhenSet()
    {
        var svc = new Service
        {
            MetaData = new PluginMetaData { IconPath = "/plugins/demo/icon.png" }
        };
        svc.IconPath = "/settings/plugins/demo/icons/abc.png";
        Assert.Equal("/settings/plugins/demo/icons/abc.png", svc.IconPath);
    }

    [Fact]
    public void Service_IconPath_RaisesPropertyChanged()
    {
        var svc = new Service
        {
            MetaData = new PluginMetaData { IconPath = "/plugins/demo/icon.png" }
        };
        var fired = false;
        svc.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Service.IconPath)) fired = true; };
        svc.IconPath = "/custom.png";
        Assert.True(fired);
    }

    [Fact]
    public void Service_IconPath_SetSameValue_StillRaisesPropertyChanged()
    {
        // 图标文件可能在路径不变时被覆盖（同扩展名替换），setter 必须始终触发通知
        var svc = new Service
        {
            MetaData = new PluginMetaData { IconPath = "/plugins/demo/icon.png" }
        };
        svc.IconPath = "/custom.png";
        var fired = false;
        svc.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Service.IconPath)) fired = true; };
        svc.IconPath = "/custom.png"; // 相同值
        Assert.True(fired);
    }

    [Theory]
    [InlineData(@"C:\settings\plugins\demo\icons\abc.png", @"C:\settings\plugins\demo", "icons/abc.png")]
    public void ToRelativeIconPath_ConvertsAbsolute(string abs, string baseDir, string expected)
    {
        var rel = Helper.ToRelativeIconPath(abs, baseDir);
        Assert.Equal(expected, rel!.Replace('\\', '/'));
    }

    [Fact]
    public void ToRelativeIconPath_ReturnsNullForEmpty()
    {
        Assert.Null(Helper.ToRelativeIconPath("", @"C:\base"));
        Assert.Null(Helper.ToRelativeIconPath(null, @"C:\base"));
    }

    [Fact]
    public void ToAbsoluteIconPath_CombinesPaths()
    {
        var abs = Helper.ToAbsoluteIconPath("icons/abc.png", @"C:\base");
        // 标准化分隔符后比较，避免平台路径分隔符差异
        Assert.Equal(@"C:/base/icons/abc.png", abs.Replace('\\', '/'));
    }

    [Fact]
    public void ResetIcon_ClearsIconPath_ToEmpty()
    {
        // 模拟重置逻辑：置空后 getter 回退到 MetaData
        var svc = new Service
        {
            MetaData = new PluginMetaData { IconPath = "/plugins/demo/icon.png" }
        };
        svc.IconPath = "/custom.png";
        Assert.Equal("/custom.png", svc.IconPath);

        // 重置
        svc.IconPath = string.Empty;
        Assert.Equal("/plugins/demo/icon.png", svc.IconPath);
    }

    [Fact]
    public void ImagePathToSourceConverter_LoadsWithoutLockingFile()
    {
        // 转换器使用 BitmapCacheOption.OnLoad，加载后应释放文件句柄，
        // 使得加载后可立即删除该文件（修复更换/重置图标时文件被占用的问题）
        var tempFile = Path.Combine(Path.GetTempPath(), $"st_icontest_{Guid.NewGuid():N}.png");
        // 最小有效 1x1 透明 PNG
        File.WriteAllBytes(tempFile, Convert.FromHexString(
            "89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C489"
            + "0000000D49444154789C6300010000000500010D0A2DB40000000049454E44AE426082"));

        try
        {
            var converter = new ImagePathToSourceConverter();
            var image = converter.Convert(tempFile, typeof(object), null, System.Globalization.CultureInfo.InvariantCulture);

            // 加载后文件应可删除（未被锁定）
            File.Delete(tempFile);
            Assert.False(File.Exists(tempFile));
            Assert.NotNull(image);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
