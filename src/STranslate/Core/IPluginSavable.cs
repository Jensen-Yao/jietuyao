using STranslate.Plugin;

namespace STranslate.Core;

/// <summary>
/// 插件可保存接口。
/// </summary>
public interface IPluginSavable : ISavable
{
    /// <summary>
    /// 删除插件设置目录（如果为空）。
    /// </summary>
    void Clean();
}
