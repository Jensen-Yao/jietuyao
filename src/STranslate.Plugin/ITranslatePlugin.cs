using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Plugin;

/// <summary>
/// 字典插件基类
/// </summary>
public abstract partial class DictionaryPluginBase : ObservableObject, IDictionaryPlugin
{
    /// <summary>
    /// 字典查询结果
    /// </summary>
    public DictionaryResult DictionaryResult { get; } = new();

    /// <summary>
    /// 获取设置UI
    /// </summary>
    /// <returns></returns>
    public abstract Control GetSettingUI();
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    public abstract void Init(IPluginContext context);
    /// <summary>
    /// 翻译
    /// </summary>
    /// <param name="content"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task TranslateAsync(string content, DictionaryResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放
    /// </summary>
    public abstract void Dispose();
}

/// <summary>
/// 翻译插件基类
/// </summary>
public abstract partial class TranslatePluginBase : ObservableObject, ITranslatePlugin
{
    /// <summary>
    /// 翻译结果
    /// </summary>
    public TranslateResult TransResult { get; } = new();
    /// <summary>
    /// 回译结果
    /// </summary>
    public TranslateResult TransBackResult { get; } = new();
    /// <summary>
    /// 获取设置UI
    /// </summary>
    /// <returns></returns>
    public abstract Control GetSettingUI();
    /// <summary>
    /// 获取源语言
    /// </summary>
    /// <param name="langEnum"></param>
    /// <returns></returns>
    public abstract string? GetSourceLanguage(LangEnum langEnum);
    /// <summary>
    /// 获取目标语言
    /// </summary>
    /// <param name="langEnum"></param>
    /// <returns></returns>
    public abstract string? GetTargetLanguage(LangEnum langEnum);
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    public abstract void Init(IPluginContext context);
    /// <summary>
    /// 翻译
    /// </summary>
    /// <param name="request"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放
    /// </summary>
    public abstract void Dispose();
}

/// <summary>
/// 大语言模型翻译插件基类
/// </summary>
public abstract class LlmTranslatePluginBase : TranslatePluginBase, ILlm
{
    /// <summary>
    /// Prompts
    /// </summary>
    public ObservableCollection<Prompt> Prompts { get; set; } = [];
    /// <summary>
    /// 选择的Prompt
    /// </summary>
    public Prompt? SelectedPrompt
    {
        get => Prompts.FirstOrDefault(p => p.IsEnabled);
        set => SelectPrompt(value);
    }

    /// <summary>
    /// 选择Prompt
    /// </summary>
    /// <param name="prompt"></param>
    public virtual void SelectPrompt(Prompt? prompt)
    {
        if (prompt == null) return;

        // 更新所有 Prompt 的 IsEnabled 状态
        foreach (var p in Prompts)
        {
            p.IsEnabled = p == prompt;
        }

        // 触发属性变更通知（如果需要）
        OnPropertyChanged(nameof(SelectedPrompt));
    }
}

/// <summary>
/// 定义翻译插件的接口，包含插件初始化、语言支持、配置校验和翻译功能。
/// </summary>
public interface ITranslatePlugin : IPlugin
{
    /// <summary>
    /// 翻译结果
    /// </summary>
    TranslateResult TransResult { get; }

    /// <summary>
    /// 回译结果
    /// </summary>
    TranslateResult TransBackResult { get; }

    /// <summary>
    /// 重置翻译结果
    /// </summary>
    void Reset()
    {
        TransResult.Text = string.Empty;
        TransResult.Duration = TimeSpan.Zero;
        TransResult.IsProcessing = false;
        TransResult.IsSuccess = true;

        ResetBack();
    }

    /// <summary>
    /// 重置回译结果
    /// </summary>
    void ResetBack()
    {

        TransBackResult.Text = string.Empty;
        TransBackResult.Duration = TimeSpan.Zero;
        TransBackResult.IsProcessing = false;
        TransBackResult.IsSuccess = true;
    }

    /// <summary>
    /// 获取源语言
    /// </summary>
    /// <param name="langEnum"></param>
    /// <returns></returns>
    string? GetSourceLanguage(LangEnum langEnum);
    
    /// <summary>
    /// 获取目标语言
    /// </summary>
    /// <param name="langEnum"></param>
    /// <returns></returns>
    string? GetTargetLanguage(LangEnum langEnum);

    /// <summary>
    /// 异步执行翻译操作。
    /// </summary>
    /// <param name="request">翻译请求参数。</param>
    /// <param name="result">翻译结果实例。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns></returns>
    Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示翻译请求的参数，包括待翻译文本、源语言和目标语言。
/// </summary>
/// <param name="Text">文本</param>
/// <param name="SourceLang">源语言</param>
/// <param name="TargetLang">目标语言</param>
public record TranslateRequest(string Text, LangEnum SourceLang, LangEnum TargetLang);

/// <summary>
/// 表示翻译结果，包括翻译后的文本、源语言、目标语言、耗时、是否成功及错误信息。
/// </summary>
public partial class TranslateResult : ObservableObject
{
    /// <summary>
    /// 成功
    /// </summary>
    /// <param name="text"></param>
    public void Success(string text)
    {
        Text = text;
        IsSuccess = true;
    }

    /// <summary>
    /// 失败
    /// </summary>
    /// <param name="text"></param>
    public void Fail(string text)
    {
        Text = text;
        IsSuccess = false;
    }

    /// <summary>
    /// 更新结果
    /// </summary>
    /// <param name="other"></param>
    public void Update(TranslateResult other)
    {
        Text = other.Text;
        IsSuccess = other.IsSuccess;
    }

    /// <summary>
    /// 是否成功
    /// </summary>
    [ObservableProperty] public partial bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 翻译后的文本内容。
    /// </summary>
    [ObservableProperty] public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// 翻译耗时。
    /// </summary>
    [ObservableProperty] public partial TimeSpan Duration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 是否正在翻译
    /// </summary>
    [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
}

/// <summary>
/// 字典插件接口
/// </summary>
public interface IDictionaryPlugin : IPlugin
{
    /// <summary>
    /// 字典查询结果
    /// </summary>
    DictionaryResult DictionaryResult { get; }

    /// <summary>
    /// 重置
    /// </summary>
    void Reset()
    {
        DictionaryResult.Text = string.Empty;
        DictionaryResult.Symbols.Clear();
        DictionaryResult.DictMeans.Clear();
        DictionaryResult.Plurals.Clear();
        DictionaryResult.PastTense.Clear();
        DictionaryResult.PastParticiple.Clear();
        DictionaryResult.PresentParticiple.Clear();
        DictionaryResult.ThirdPersonSingular.Clear();
        DictionaryResult.Comparative.Clear();
        DictionaryResult.Superlative.Clear();
        DictionaryResult.Tags.Clear();
        DictionaryResult.Sentences.Clear();
        DictionaryResult.Duration = TimeSpan.Zero;
        DictionaryResult.IsProcessing = false;
        DictionaryResult.ResultType = DictionaryResultType.None;
    }

    /// <summary>
    /// 翻译
    /// </summary>
    /// <param name="content"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task TranslateAsync(string content, DictionaryResult result, CancellationToken cancellationToken = default);
}

/// <summary>
/// 字典查询结果
/// </summary>
public partial class DictionaryResult : ObservableObject
{
    /// <summary>
    /// 更新结果
    /// </summary>
    /// <param name="other"></param>
    public void Update(DictionaryResult other)
    {
        Text = other.Text;
        ResultType = other.ResultType;

        Application.Current.Dispatcher.Invoke(() =>
        {
            other.Symbols.ToList().ForEach(Symbols.Add);
            other.DictMeans.ToList().ForEach(DictMeans.Add);
            other.Plurals.ToList().ForEach(Plurals.Add);
            other.PastTense.ToList().ForEach(PastTense.Add);
            other.PastParticiple.ToList().ForEach(PastParticiple.Add);
            other.PresentParticiple.ToList().ForEach(PresentParticiple.Add);
            other.ThirdPersonSingular.ToList().ForEach(ThirdPersonSingular.Add);
            other.Comparative.ToList().ForEach(Comparative.Add);
            other.Superlative.ToList().ForEach(Superlative.Add);
            other.Tags.ToList().ForEach(Tags.Add);
            other.Sentences.ToList().ForEach(Sentences.Add);
        });
    }
    /// <summary>
    /// 结果类型
    /// </summary>
    [ObservableProperty] public partial DictionaryResultType ResultType { get; set; } = DictionaryResultType.None;

    /// <summary>
    /// 文本
    /// </summary>
    [ObservableProperty] public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// 音标
    /// </summary>
    public ObservableCollection<Symbol> Symbols { get; set; } = [];

    /// <summary>
    /// 词典释义
    /// </summary>
    public ObservableCollection<DictMean> DictMeans { get; set; } = [];

    /// <summary>
    /// 复数形式
    /// </summary>
    public ObservableCollection<string> Plurals { get; set; } = [];

    /// <summary>
    /// 过去式
    /// </summary>
    public ObservableCollection<string> PastTense { get; set; } = [];

    /// <summary>
    /// 过去分词
    /// </summary>
    public ObservableCollection<string> PastParticiple { get; set; } = [];

    /// <summary>
    /// 现在分词/动名词
    /// </summary>
    public ObservableCollection<string> PresentParticiple { get; set; } = [];

    /// <summary>
    /// 第三人称单数
    /// </summary>
    public ObservableCollection<string> ThirdPersonSingular { get; set; } = [];

    /// <summary>
    /// 比较级
    /// </summary>
    public ObservableCollection<string> Comparative { get; set; } = [];

    /// <summary>
    /// 最高级
    /// </summary>
    public ObservableCollection<string> Superlative { get; set; } = [];

    /// <summary>
    /// 标签
    /// </summary>
    public ObservableCollection<string> Tags { get; set; } = [];

    /// <summary>
    /// 句子
    /// </summary>
    public ObservableCollection<string> Sentences { get; set; } = [];

    /// <summary>
    /// 翻译耗时
    /// </summary>
    [ObservableProperty] public partial TimeSpan Duration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 是否正在翻译
    /// </summary>
    [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
}

/// <summary>
/// 字典结果类型
/// </summary>
public enum DictionaryResultType
{
    /// <summary>
    /// 无
    /// </summary>
    None,
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    /// <summary>
    /// 错误
    /// </summary>
    Error,
    /// <summary>
    /// 无结果
    /// </summary>
    NoResult,
}

/// <summary>
/// 音标
/// </summary>
public partial class Symbol : ObservableObject
{
    /// <summary>
    /// 标签
    /// </summary>
    [ObservableProperty] public partial string Label { get; set; } = string.Empty;
    /// <summary>
    /// 音标
    /// </summary>
    [ObservableProperty] public partial string Phonetic { get; set; } = string.Empty;
    /// <summary>
    /// 音频地址
    /// </summary>
    [ObservableProperty] public partial string AudioUrl { get; set; } = string.Empty;
}

/// <summary>
/// 词典释义
/// </summary>
public partial class DictMean : ObservableObject
{
    /// <summary>
    /// 词性
    /// </summary>
    [ObservableProperty] public partial string PartOfSpeech { get; set; } = string.Empty;
    /// <summary>
    /// 释义
    /// </summary>
    public ObservableCollection<string> Means { get; set; } = [];
}