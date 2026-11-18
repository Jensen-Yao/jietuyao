namespace STranslate.Plugin;

/// <summary>
/// 通知接口
/// </summary>
public interface INotification
{
    /// <summary>
    /// 显示
    /// </summary>
    /// <param name="title"></param>
    /// <param name="subTitle"></param>
    /// <param name="iconPath"></param>
    void Show(string title, string subTitle, string? iconPath = null);
    
    /// <summary>
    /// 显示带按钮的通知
    /// </summary>
    /// <param name="title"></param>
    /// <param name="buttonText"></param>
    /// <param name="buttonAction"></param>
    /// <param name="subTitle"></param>
    /// <param name="iconPath"></param>
    void ShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string? iconPath = null);
}
