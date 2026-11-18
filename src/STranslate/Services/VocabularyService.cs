using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.Services;

public partial class VocabularyService : BaseService
{
    protected override ServiceType ServiceType => ServiceType.Vocabulary;
    [ObservableProperty] public partial bool HasActivedVocabulary { get; set; } = false;

    public VocabularyService(
        PluginManager pluginManager,
        ServiceManager serviceManager,
        PluginService PluginService,
        ServiceSettings serviceSettings,
        Internationalization i18n
    ) : base(pluginManager, serviceManager, PluginService, serviceSettings, i18n)
    {
        LoadPlugins<IVocabularyPlugin>();
        LoadServices<IVocabularyPlugin>();

        HasActivedVocabulary = Services?.Any(s => s.IsEnabled) ?? false;
        OnSvcPropertyChanged += OnSvcPropertyChangedHandler;
    }

    public override void Dispose()
    {
        OnSvcPropertyChanged -= OnSvcPropertyChangedHandler;
        base.Dispose();
    }

    private void OnSvcPropertyChangedHandler() => HasActivedVocabulary = Services.Any(s => s.IsEnabled);
}
