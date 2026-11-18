using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace STranslate.Controls;

public class OutputControl : ItemsControl
{
    static OutputControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(OutputControl),
            new FrameworkPropertyMetadata(typeof(OutputControl)));
    }

    public ICommand? CopyCommand
    {
        get => (ICommand?)GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    public static readonly DependencyProperty CopyCommandProperty =
        DependencyProperty.Register(
            nameof(CopyCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? InsertCommand
    {
        get => (ICommand?)GetValue(InsertCommandProperty);
        set => SetValue(InsertCommandProperty, value);
    }

    public static readonly DependencyProperty InsertCommandProperty =
        DependencyProperty.Register(
            nameof(InsertCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? CleanTransBackCommand
    {
        get => (ICommand?)GetValue(CleanTransBackCommandProperty);
        set => SetValue(CleanTransBackCommandProperty, value);
    }

    public static readonly DependencyProperty CleanTransBackCommandProperty =
        DependencyProperty.Register(
            nameof(CleanTransBackCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(
            nameof(RetryCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? TransBackCommand
    {
        get => (ICommand?)GetValue(TransBackCommandProperty);
        set => SetValue(TransBackCommandProperty, value);
    }

    public static readonly DependencyProperty TransBackCommandProperty =
        DependencyProperty.Register(
            nameof(TransBackCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(
            nameof(NavigateCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? PlayAudioCommand
    {
        get => (ICommand?)GetValue(PlayAudioCommandProperty);
        set => SetValue(PlayAudioCommandProperty, value);
    }

    public static readonly DependencyProperty PlayAudioCommandProperty =
        DependencyProperty.Register(
            nameof(PlayAudioCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? PlayAudioCancelCommand
    {
        get => (ICommand?)GetValue(PlayAudioCancelCommandProperty);
        set => SetValue(PlayAudioCancelCommandProperty, value);
    }

    public static readonly DependencyProperty PlayAudioCancelCommandProperty =
        DependencyProperty.Register(
            nameof(PlayAudioCancelCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? PlayAudioUrlCommand
    {
        get => (ICommand?)GetValue(PlayAudioUrlCommandProperty);
        set => SetValue(PlayAudioUrlCommandProperty, value);
    }

    public static readonly DependencyProperty PlayAudioUrlCommandProperty =
        DependencyProperty.Register(
            nameof(PlayAudioUrlCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public bool ShowPrompt
    {
        get => (bool)GetValue(ShowPromptProperty);
        set => SetValue(ShowPromptProperty, value);
    }

    public static readonly DependencyProperty ShowPromptProperty =
        DependencyProperty.Register(
            nameof(ShowPrompt),
            typeof(bool),
            typeof(OutputControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置生成回译结果时使用的源语种。
    /// </summary>
    public LangEnum SourceLanguage
    {
        get => (LangEnum)GetValue(SourceLanguageProperty);
        set => SetValue(SourceLanguageProperty, value);
    }

    public static readonly DependencyProperty SourceLanguageProperty =
        DependencyProperty.Register(
            nameof(SourceLanguage),
            typeof(LangEnum),
            typeof(OutputControl),
            new PropertyMetadata(LangEnum.Auto));

    /// <summary>
    /// 获取或设置自动识别出的源语种。
    /// </summary>
    public LangEnum DetectedSourceLanguage
    {
        get => (LangEnum)GetValue(DetectedSourceLanguageProperty);
        set => SetValue(DetectedSourceLanguageProperty, value);
    }

    public static readonly DependencyProperty DetectedSourceLanguageProperty =
        DependencyProperty.Register(
            nameof(DetectedSourceLanguage),
            typeof(LangEnum),
            typeof(OutputControl),
            new PropertyMetadata(LangEnum.Auto));

    /// <summary>
    /// 获取或设置普通翻译结果使用的目标语种。
    /// </summary>
    public LangEnum TargetLanguage
    {
        get => (LangEnum)GetValue(TargetLanguageProperty);
        set => SetValue(TargetLanguageProperty, value);
    }

    public static readonly DependencyProperty TargetLanguageProperty =
        DependencyProperty.Register(
            nameof(TargetLanguage),
            typeof(LangEnum),
            typeof(OutputControl),
            new PropertyMetadata(LangEnum.Auto));

    public ICommand? ExplainCommand
    {
        get => (ICommand?)GetValue(ExplainCommandProperty);
        set => SetValue(ExplainCommandProperty, value);
    }

    public static readonly DependencyProperty ExplainCommandProperty =
        DependencyProperty.Register(
            nameof(ExplainCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? CopyPascalCaseCommand
    {
        get => (ICommand?)GetValue(CopyPascalCaseCommandProperty);
        set => SetValue(CopyPascalCaseCommandProperty, value);
    }

    public static readonly DependencyProperty CopyPascalCaseCommandProperty =
        DependencyProperty.Register(
            nameof(CopyPascalCaseCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? CopyCamelCaseCommand
    {
        get => (ICommand?)GetValue(CopyCamelCaseCommandProperty);
        set => SetValue(CopyCamelCaseCommandProperty, value);
    }

    public static readonly DependencyProperty CopyCamelCaseCommandProperty =
        DependencyProperty.Register(
            nameof(CopyCamelCaseCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public ICommand? CopySnakeCaseCommand
    {
        get => (ICommand?)GetValue(CopySnakeCaseCommandProperty);
        set => SetValue(CopySnakeCaseCommandProperty, value);
    }

    public static readonly DependencyProperty CopySnakeCaseCommandProperty =
        DependencyProperty.Register(
            nameof(CopySnakeCaseCommand),
            typeof(ICommand),
            typeof(OutputControl));

    public bool ShowPascalCase
    {
        get => (bool)GetValue(ShowPascalCaseProperty);
        set => SetValue(ShowPascalCaseProperty, value);
    }

    public static readonly DependencyProperty ShowPascalCaseProperty =
        DependencyProperty.Register(
            nameof(ShowPascalCase),
            typeof(bool),
            typeof(OutputControl),
            new PropertyMetadata(true));

    public bool ShowCamelCase
    {
        get => (bool)GetValue(ShowCamelCaseProperty);
        set => SetValue(ShowCamelCaseProperty, value);
    }

    public static readonly DependencyProperty ShowCamelCaseProperty =
        DependencyProperty.Register(
            nameof(ShowCamelCase),
            typeof(bool),
            typeof(OutputControl),
            new PropertyMetadata(false));

    public bool ShowSnakeCase
    {
        get => (bool)GetValue(ShowSnakeCaseProperty);
        set => SetValue(ShowSnakeCaseProperty, value);
    }

    public static readonly DependencyProperty ShowSnakeCaseProperty =
        DependencyProperty.Register(
            nameof(ShowSnakeCase),
            typeof(bool),
            typeof(OutputControl),
            new PropertyMetadata(true));

    public bool ShowInsert
    {
        get => (bool)GetValue(ShowInsertProperty);
        set => SetValue(ShowInsertProperty, value);
    }

    public static readonly DependencyProperty ShowInsertProperty =
        DependencyProperty.Register(
            nameof(ShowInsert),
            typeof(bool),
            typeof(OutputControl),
            new PropertyMetadata(true));

    public bool ShowBackTranslation
    {
        get => (bool)GetValue(ShowBackTranslationProperty);
        set => SetValue(ShowBackTranslationProperty, value);
    }

    public static readonly DependencyProperty ShowBackTranslationProperty =
        DependencyProperty.Register(
            nameof(ShowBackTranslation),
            typeof(bool),
            typeof(OutputControl),
            new PropertyMetadata(true));

    public ICommand? SaveToVocabularyWithNoteCommand
    {
        get => (ICommand?)GetValue(SaveToVocabularyWithNoteCommandProperty);
        set => SetValue(SaveToVocabularyWithNoteCommandProperty, value);
    }

    public static readonly DependencyProperty SaveToVocabularyWithNoteCommandProperty =
        DependencyProperty.Register(nameof(SaveToVocabularyWithNoteCommand), typeof(ICommand), typeof(OutputControl));

    public bool HasActivedVocabulary
    {
        get => (bool)GetValue(HasActivedVocabularyProperty);
        set => SetValue(HasActivedVocabularyProperty, value);
    }

    public static readonly DependencyProperty HasActivedVocabularyProperty =
        DependencyProperty.Register(nameof(HasActivedVocabulary), typeof(bool), typeof(OutputControl), new PropertyMetadata(false));
}
