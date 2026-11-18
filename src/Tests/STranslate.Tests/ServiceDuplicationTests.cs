using Microsoft.Extensions.Logging.Abstractions;
using STranslate.Core;
using STranslate.Plugin;
using System.Text.Json;
using System.Windows.Controls;

namespace STranslate.Tests;

public class ServiceDuplicationTests
{
    [Fact]
    public void DuplicateService_Translation_CopiesPersistedConfigurationAsIndependentSnapshot()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var (manager, serviceSettings, source) = CreateSourceService(testDirectory);
            serviceSettings.ReplaceSvcID = source.ServiceID;
            serviceSettings.ImageTranslateSvcID = source.ServiceID;

            var duplicate = manager.DuplicateService(source, ServiceType.Translation);

            Assert.NotNull(duplicate);
            Assert.NotEqual(source.ServiceID, duplicate.ServiceID);
            Assert.Equal("Original_New", duplicate.DisplayName);
            Assert.True(duplicate.IsEnabled);
            Assert.Equal(ExecutionMode.Manual, duplicate.Options?.ExecMode);
            Assert.True(duplicate.Options?.AutoBackTranslation);
            Assert.NotSame(source.Options, duplicate.Options);

            Assert.Equal([source, duplicate], manager.AllServices);
            Assert.Equal([source.ServiceID, duplicate.ServiceID], serviceSettings.TranSvcDatas.Select(data => data.SvcID));
            Assert.Equal("Original_New", serviceSettings.TranSvcDatas[1].Name);
            Assert.Equal(ExecutionMode.Manual, serviceSettings.TranSvcDatas[1].Options?.ExecMode);
            Assert.True(serviceSettings.TranSvcDatas[1].Options?.AutoBackTranslation);
            Assert.Equal(source.ServiceID, serviceSettings.ReplaceSvcID);
            Assert.Equal(source.ServiceID, serviceSettings.ImageTranslateSvcID);

            var duplicatePlugin = Assert.IsType<TestPlugin>(duplicate.Plugin);
            Assert.Equal("source-key", duplicatePlugin.Settings.ApiKey);
            Assert.Equal(["model-a", "model-b"], duplicatePlugin.Settings.Models);

            var duplicateIconPath = duplicate.IconPath;
            Assert.NotEqual(source.IconPath, duplicateIconPath);
            Assert.Equal($"{duplicate.ServiceID}.png", Path.GetFileName(duplicateIconPath));
            Assert.Equal(File.ReadAllBytes(source.IconPath), File.ReadAllBytes(duplicateIconPath));

            duplicatePlugin.Settings.ApiKey = "duplicate-key";
            duplicatePlugin.Save();
            duplicate.Options!.ExecMode = ExecutionMode.Automatic;

            var sourceSettingsPath = Path.Combine(source.MetaData.PluginSettingsDirectoryPath, $"{source.ServiceID}.json");
            var persistedSourceSettings = JsonSerializer.Deserialize<TestPluginSettings>(File.ReadAllText(sourceSettingsPath));
            Assert.Equal("source-key", persistedSourceSettings?.ApiKey);
            Assert.Equal(ExecutionMode.Manual, source.Options?.ExecMode);

            File.Delete(source.IconPath);
            Assert.True(File.Exists(duplicateIconPath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(ServiceType.OCR)]
    [InlineData(ServiceType.TTS)]
    [InlineData(ServiceType.Vocabulary)]
    public void DuplicateService_SingleEnabledType_CopiesConfigurationAndKeepsDuplicateDisabled(ServiceType type)
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var (manager, serviceSettings, source) = CreateSourceService(testDirectory, type);

            var duplicate = manager.DuplicateService(source, type);

            Assert.NotNull(duplicate);
            Assert.Equal("Original_New", duplicate.DisplayName);
            Assert.False(duplicate.IsEnabled);
            Assert.Equal([source, duplicate], manager.AllServices);

            var serviceDataCollection = GetServiceDataCollection(serviceSettings, type);
            Assert.Equal([source.ServiceID, duplicate.ServiceID], serviceDataCollection.Select(data => data.SvcID));
            Assert.False(serviceDataCollection[1].IsEnabled);

            var duplicatePlugin = Assert.IsType<TestPlugin>(duplicate.Plugin);
            Assert.Equal("source-key", duplicatePlugin.Settings.ApiKey);
            Assert.Equal(["model-a", "model-b"], duplicatePlugin.Settings.Models);
            Assert.NotEqual(source.IconPath, duplicate.IconPath);
            Assert.Equal(File.ReadAllBytes(source.IconPath), File.ReadAllBytes(duplicate.IconPath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void DuplicateService_WhenCustomIconCopyFails_RollsBackCopiedConfiguration()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var (manager, serviceSettings, source) = CreateSourceService(testDirectory);
            File.Delete(source.IconPath);

            var duplicate = manager.DuplicateService(source, ServiceType.Translation);

            Assert.Null(duplicate);
            Assert.Equal([source], manager.AllServices);
            Assert.Equal([source.ServiceID], serviceSettings.TranSvcDatas.Select(data => data.SvcID));
            Assert.Equal(
                [$"{source.ServiceID}.json"],
                Directory.EnumerateFiles(source.MetaData.PluginSettingsDirectoryPath, "*.json").Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static (ServiceManager Manager, ServiceSettings Settings, Service Source) CreateSourceService(
        string testDirectory,
        ServiceType type = ServiceType.Translation)
    {
        var pluginSettingsDirectory = Path.Combine(testDirectory, "settings");
        var pluginIconPath = Path.Combine(testDirectory, "plugin.png");
        File.WriteAllBytes(pluginIconPath, [0x01, 0x02, 0x03]);

        var metaData = new PluginMetaData
        {
            PluginID = "test-translate-plugin",
            Name = "Test Translate",
            IconPath = pluginIconPath,
            PluginType = typeof(TestPlugin),
            PluginSettingsDirectoryPath = pluginSettingsDirectory
        };

        var serviceSettings = new ServiceSettings();
        serviceSettings.SetStorage(new NoOpServiceSettingsStorage());
        var manager = new ServiceManager(null!, serviceSettings, NullLogger<ServiceManager>.Instance);
        var source = manager.AddService(metaData, type);

        source.DisplayName = "Original";
        source.IsEnabled = true;
        source.Options!.ExecMode = ExecutionMode.Manual;
        source.Options.AutoBackTranslation = true;

        var sourceData = Assert.Single(GetServiceDataCollection(serviceSettings, type));
        sourceData.Name = source.DisplayName;
        sourceData.IsEnabled = source.IsEnabled;
        sourceData.Options = new TranslationOptions
        {
            ExecMode = source.Options.ExecMode,
            AutoBackTranslation = source.Options.AutoBackTranslation
        };

        var sourcePlugin = Assert.IsType<TestPlugin>(source.Plugin);
        sourcePlugin.Settings.ApiKey = "source-key";
        sourcePlugin.Settings.Models = ["model-a", "model-b"];
        sourcePlugin.Save();

        var iconsDirectory = Path.Combine(pluginSettingsDirectory, "icons");
        Directory.CreateDirectory(iconsDirectory);
        var sourceIconPath = Path.Combine(iconsDirectory, $"{source.ServiceID}.png");
        File.WriteAllBytes(sourceIconPath, [0x10, 0x20, 0x30]);
        source.IconPath = sourceIconPath;
        sourceData.IconPath = Helper.ToRelativeIconPath(sourceIconPath, pluginSettingsDirectory);

        return (manager, serviceSettings, source);
    }

    private static List<ServiceData> GetServiceDataCollection(ServiceSettings settings, ServiceType type) => type switch
    {
        ServiceType.Translation => settings.TranSvcDatas,
        ServiceType.OCR => settings.OcrSvcDatas,
        ServiceType.TTS => settings.TtsSvcDatas,
        ServiceType.Vocabulary => settings.VocabularySvcDatas,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "不支持的服务类型")
    };

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"STranslate_Duplicate_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class NoOpServiceSettingsStorage : AppStorage<ServiceSettings>
    {
        public override void Save()
        {
        }
    }

    internal sealed class TestPlugin : TranslatePluginBase, IOcrPlugin, ITtsPlugin, IVocabularyPlugin
    {
        private IPluginContext _context = null!;

        internal TestPluginSettings Settings { get; private set; } = null!;

        public IEnumerable<LangEnum> SupportedLanguages => [LangEnum.English];

        public override Control GetSettingUI() => null!;

        public override string? GetSourceLanguage(LangEnum langEnum) => langEnum.ToString();

        public override string? GetTargetLanguage(LangEnum langEnum) => langEnum.ToString();

        public override void Init(IPluginContext context)
        {
            _context = context;
            Settings = context.LoadSettingStorage<TestPluginSettings>();
        }

        public override Task TranslateAsync(
            TranslateRequest request,
            TranslateResult result,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<OcrResult> RecognizeAsync(
            OcrRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(new OcrResult());

        public Task PlayAudioAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<VocabularyResult> SaveAsync(
            string text,
            CancellationToken cancellationToken = default) => Task.FromResult(new VocabularyResult());

        public override void Dispose()
        {
        }

        internal void Save() => _context.SaveSettingStorage<TestPluginSettings>();
    }

    internal sealed class TestPluginSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public List<string> Models { get; set; } = [];
    }
}
