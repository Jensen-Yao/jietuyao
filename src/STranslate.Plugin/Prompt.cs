using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace STranslate.Plugin;

/// <summary>
/// Prompt
/// </summary>
public partial class Prompt : ObservableObject
{
    /// <summary>
    /// 名称
    /// </summary>
    [ObservableProperty] public partial string Name { get; set; }
    
    /// <summary>
    /// Items
    /// </summary>
    public ObservableCollection<PromptItem> Items { get; set; } = [];
    
    /// <summary>
    /// 是否启用
    /// </summary>
    [ObservableProperty] public partial bool IsEnabled { get; set; }

    /// <summary>
    /// Prompt
    /// </summary>
    public Prompt()
    {
        Name = "New Prompt";
        IsEnabled = false;
    }

    /// <summary>
    /// Prompt
    /// </summary>
    /// <param name="name"></param>
    /// <param name="prompts"></param>
    /// <param name="isEnabled"></param>
    public Prompt(string name, IEnumerable<PromptItem> prompts, bool isEnabled = false)
    {
        Name = name;
        IsEnabled = isEnabled;
        foreach (var prompt in prompts)
        {
            Items.Add(prompt.Clone());
        }
    }
    
    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns></returns>
    public Prompt Clone()
    {
        return new Prompt(Name, Items.Select(p => p.Clone()), IsEnabled);
    }
}

/// <summary>
/// PromptItem
/// </summary>
public partial class PromptItem : ObservableObject
{
    /// <summary>
    /// 角色
    /// </summary>
    [ObservableProperty]
    [JsonPropertyName("role")]
    public partial string Role { get; set; } = "";

    /// <summary>
    /// 内容
    /// </summary>
    [ObservableProperty]
    [JsonPropertyName("content")]
    public partial string Content { get; set; } = "";

    /// <summary>
    /// PromptItem
    /// </summary>
    public PromptItem() { }

    /// <summary>
    /// PromptItem
    /// </summary>
    /// <param name="role"></param>
    public PromptItem(string role)
    {
        Role = role;
        Content = "";
    }

    /// <summary>
    /// PromptItem
    /// </summary>
    /// <param name="role"></param>
    /// <param name="content"></param>
    public PromptItem(string role, string content)
    {
        Role = role;
        Content = content;
    }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns></returns>
    public PromptItem Clone()
    {
        return new PromptItem(Role, Content);
    }
}