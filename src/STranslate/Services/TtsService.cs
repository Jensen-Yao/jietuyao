using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.Services;

public partial class TtsService : BaseService
{
    protected override ServiceType ServiceType => ServiceType.TTS;

    public TtsService(
        PluginManager pluginManager,
        ServiceManager serviceManager,
        PluginService PluginService,
        ServiceSettings serviceSettings,
        Internationalization i18n
    ) : base(pluginManager, serviceManager, PluginService, serviceSettings, i18n)
    {
        LoadPlugins<ITtsPlugin>();
        LoadServices<ITtsPlugin>();
    }
}
