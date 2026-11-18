using System.Windows.Controls;

namespace STranslate.Plugin;

/// <summary>
///     通用插件接口（内部使用）
///     <para>⚠️ 请不要直接实现本接口</para>
///     <para>🤟 <see cref="ITranslatePlugin"/>、<see cref="ITtsPlugin"/>、<see cref="IOcrPlugin"/></para>
/// </summary>
public interface IPlugin : IDisposable
{
    /// <summary>
    ///     初始化插件
    /// </summary>
    /// <param name="context">插件上下文</param>
    void Init(IPluginContext context);

    /// <summary>
    ///     创建插件配置面板
    /// </summary>
    /// <returns>配置面板的 UserControl</returns>
    Control GetSettingUI();
}