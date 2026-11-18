namespace STranslate.Plugin;

/// <summary>
/// 表示可保存的插件接口。
/// </summary>
public interface ISavable
{
    /// <summary>
    /// 保存插件的状态或数据。
    /// </summary>
    void Save();

    /// <summary>
    /// 删除插件的状态或数据
    /// </summary>
    void Delete();
}