using System.Collections.ObjectModel;

namespace STranslate.Plugin;

/// <summary>
/// 大语言模型接口
/// </summary>
public interface ILlm
{
    /// <summary>
    /// Prompts
    /// </summary>
    ObservableCollection<Prompt> Prompts { get; }
    
    /// <summary>
    /// 选择的Prompt
    /// </summary>
    Prompt? SelectedPrompt { get; set; }
    
    /// <summary>
    /// 选择Prompt
    /// </summary>
    /// <param name="prompt"></param>
    void SelectPrompt(Prompt prompt);
}
