using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STranslate.Core;
using STranslate.Plugin;
using STranslate.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.ViewModels;

/// <summary>
/// Provides the state and commands for the first-run quick setup wizard.
/// </summary>
public partial class WelcomeSetupViewModel : ObservableObject, IDisposable
{
    private const int TotalStepCount = 5;
    private const int LastStepIndex = TotalStepCount - 1;

    private readonly NotifyCollectionChangedEventHandler _translateServicesChangedHandler;
    private readonly NotifyCollectionChangedEventHandler _ocrServicesChangedHandler;
    private readonly Internationalization _i18n;
    private bool _disposed;

    /// <summary>
    /// Creates wizard state from the existing settings and service managers.
    /// </summary>
    public WelcomeSetupViewModel(
        Settings settings,
        HotkeySettings hotkeySettings,
        ServiceSettings serviceSettings,
        DataProvider dataProvider,
        Internationalization i18n,
        TranslateService translateService,
        OcrService ocrService)
    {
        Settings = settings;
        HotkeySettings = hotkeySettings;
        ServiceSettings = serviceSettings;
        DataProvider = dataProvider;
        _i18n = i18n;
        Languages = i18n.LoadAvailableLanguages();
        TranslateService = translateService;
        OcrService = ocrService;

        _translateServicesChangedHandler = (_, _) => RefreshTranslateServiceOptions();
        _ocrServicesChangedHandler = (_, _) => RefreshOcrServiceOptions();

        TranslateService.Services.CollectionChanged += _translateServicesChangedHandler;
        OcrService.Services.CollectionChanged += _ocrServicesChangedHandler;

        RefreshWelcomePluginOptions();

        SelectedTranslatePlugin = WelcomeTranslatePlugins.FirstOrDefault();
        SelectedOcrPlugin = WelcomeOcrPlugins.FirstOrDefault();

        SelectedTranslateService = TranslateService.Services.FirstOrDefault(s => s.IsEnabled)
            ?? TranslateService.Services.FirstOrDefault();
        SelectedOcrService = OcrService.Services.FirstOrDefault(s => s.IsEnabled)
            ?? OcrService.Services.FirstOrDefault();

        RefreshTranslateServiceOptions();
        RefreshOcrServiceOptions();
    }

    public Settings Settings { get; }

    public HotkeySettings HotkeySettings { get; }

    public ServiceSettings ServiceSettings { get; }

    public DataProvider DataProvider { get; }

    public List<I18nPair> Languages { get; }

    public TranslateService TranslateService { get; }

    public OcrService OcrService { get; }

    public ObservableCollection<Service> AvailableTranslateSpecialServices { get; } = [];

    public ObservableCollection<Service> AvailableOcrServices { get; } = [];

    public ObservableCollection<PluginMetaData> WelcomeTranslatePlugins { get; } = [];

    public ObservableCollection<PluginMetaData> WelcomeOcrPlugins { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepProgressText))]
    [NotifyCanExecuteChangedFor(nameof(MovePreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveNextCommand))]
    public partial int SelectedStepIndex { get; set; }

    public string StepProgressText => $"{SelectedStepIndex + 1}/{TotalStepCount}";

    [ObservableProperty] public partial PluginMetaData? SelectedTranslatePlugin { get; set; }

    [ObservableProperty] public partial PluginMetaData? SelectedOcrPlugin { get; set; }

    [ObservableProperty] public partial Service? SelectedTranslateService { get; set; }

    [ObservableProperty] public partial Service? SelectedOcrService { get; set; }

    [ObservableProperty] public partial Control? TranslateSettingUI { get; set; }

    [ObservableProperty] public partial Control? OcrSettingUI { get; set; }

    public Service? SelectedImageTranslateService
    {
        get => TranslateService.ImageTranslateService;
        set
        {
            if (value == TranslateService.ImageTranslateService)
                return;

            if (value == null)
                TranslateService.DeactiveImTran();
            else
                TranslateService.ActiveImTran(value);

            OnPropertyChanged();
        }
    }

    public Service? SelectedReplaceService
    {
        get => TranslateService.ReplaceService;
        set
        {
            if (value == TranslateService.ReplaceService)
                return;

            if (value == null)
                TranslateService.DeactiveReplace();
            else
                TranslateService.ActiveReplace(value);

            OnPropertyChanged();
        }
    }

    public Service? SelectedImageTranslateOcrService
    {
        get => OcrService.ImageTranslateOcrService;
        set
        {
            if (value == OcrService.ImageTranslateOcrService)
                return;

            if (value == null)
                OcrService.DeactiveImTranOcr();
            else
                OcrService.ActiveImTranOcr(value);

            OnPropertyChanged();
        }
    }

    partial void OnSelectedTranslateServiceChanged(Service? value)
    {
        TranslateSettingUI = value?.Plugin.GetSettingUI();
    }

    partial void OnSelectedOcrServiceChanged(Service? value)
    {
        OcrSettingUI = value?.Plugin.GetSettingUI();
    }

    [RelayCommand]
    private void AddTranslateService()
    {
        if (SelectedTranslatePlugin == null)
            return;

        var service = TranslateService.AddFromPlugin(SelectedTranslatePlugin);
        service.IsEnabled = true;
        SelectedTranslateService = service;

        if (service.Plugin is not IDictionaryPlugin)
        {
            if (SelectedImageTranslateService == null)
                SelectedImageTranslateService = service;
            if (SelectedReplaceService == null)
                SelectedReplaceService = service;
        }
    }

    [RelayCommand]
    private void AddOcrService()
    {
        if (SelectedOcrPlugin == null)
            return;

        var service = OcrService.AddFromPlugin(SelectedOcrPlugin);
        service.IsEnabled = true;
        SelectedOcrService = service;

        if (SelectedImageTranslateOcrService == null &&
            OcrService.IsImageTranslateOcrService(service))
        {
            RefreshOcrServiceOptions();
            SelectedImageTranslateOcrService = service;
        }
    }

    [RelayCommand]
    private void ClearImageTranslateService() => SelectedImageTranslateService = null;

    [RelayCommand]
    private void ClearReplaceService() => SelectedReplaceService = null;

    [RelayCommand]
    private void ClearImageTranslateOcrService() => SelectedImageTranslateOcrService = null;

    private bool CanMovePrevious() => SelectedStepIndex > 0;

    [RelayCommand(CanExecute = nameof(CanMovePrevious))]
    private void MovePrevious() => SelectedStepIndex--;

    private bool CanMoveNext() => SelectedStepIndex < LastStepIndex;

    [RelayCommand(CanExecute = nameof(CanMoveNext))]
    private void MoveNext() => SelectedStepIndex++;

    [RelayCommand]
    private void Finish(Window window) => SaveAndClose(window);

    [RelayCommand]
    private void Skip(Window window) => SaveAndClose(window);

    /// <summary>
    /// Saves every settings file touched by the wizard.
    /// </summary>
    public void SaveAll()
    {
        Settings.Save();
        HotkeySettings.Save();
        ServiceSettings.Save();
    }

    /// <summary>
    /// Releases collection subscriptions owned by the wizard view model.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            TranslateService.Services.CollectionChanged -= _translateServicesChangedHandler;
            OcrService.Services.CollectionChanged -= _ocrServicesChangedHandler;
        }

        _disposed = true;
    }

    private void SaveAndClose(Window window)
    {
        SaveAll();
        window.Close();
    }

    private void RefreshTranslateServiceOptions()
    {
        ReplaceItems(AvailableTranslateSpecialServices, TranslateService.Services.Where(s => s.Plugin is not IDictionaryPlugin));
        OnPropertyChanged(nameof(SelectedImageTranslateService));
        OnPropertyChanged(nameof(SelectedReplaceService));
    }

    private void RefreshOcrServiceOptions()
    {
        ReplaceItems(AvailableOcrServices, OcrService.GetImageTranslateOcrServices());
        OnPropertyChanged(nameof(SelectedImageTranslateOcrService));
    }

    private void RefreshWelcomePluginOptions()
    {
        ReplaceItems(WelcomeTranslatePlugins, SortWelcomePlugins(TranslateService.Plugins));
        ReplaceItems(WelcomeOcrPlugins, SortWelcomePlugins(OcrService.Plugins));
    }

    private IEnumerable<PluginMetaData> SortWelcomePlugins(IEnumerable<PluginMetaData> plugins) =>
        plugins
            .OrderBy(GetWelcomePluginRank)
            .ThenBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase);

    private int GetWelcomePluginRank(PluginMetaData plugin)
    {
        if (IsBuiltInPlugin(plugin))
            return 0;

        return plugin.IsPrePlugin ? 1 : 2;
    }

    private bool IsBuiltInPlugin(PluginMetaData plugin)
    {
        if (!plugin.IsPrePlugin)
            return false;

        var builtInText = _i18n.GetTranslation("BuiltIn");
        return plugin.Name.Contains(builtInText, StringComparison.OrdinalIgnoreCase) ||
               plugin.Name.Contains("Built-in", StringComparison.OrdinalIgnoreCase) ||
               plugin.Name.Contains("BuiltIn", StringComparison.OrdinalIgnoreCase) ||
               plugin.AssemblyName.Contains("BuiltIn", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        var sourceItems = source.ToList();

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!sourceItems.Contains(target[index]))
                target.RemoveAt(index);
        }

        foreach (var item in sourceItems)
        {
            if (!target.Contains(item))
                target.Add(item);
        }
    }
}
