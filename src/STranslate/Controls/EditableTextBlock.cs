using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace STranslate.Controls;

/// <summary>
/// 提供双击或命令触发的内联文本编辑能力。
/// </summary>
public class EditableTextBlock : Control
{
    /// <summary>
    /// 初始化可编辑文本控件及其编辑命令。
    /// </summary>
    public EditableTextBlock()
    {
        BeginEditCommand = new RelayCommand(BeginEdit);
    }

    static EditableTextBlock()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EditableTextBlock),
            new FrameworkPropertyMetadata(typeof(EditableTextBlock)));
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(EditableTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public static readonly DependencyProperty IsEditingProperty =
        DependencyProperty.Register(nameof(IsEditing), typeof(bool), typeof(EditableTextBlock),
            new FrameworkPropertyMetadata(false));

    /// <summary>
    /// 获取使控件进入编辑状态的命令。
    /// </summary>
    public ICommand BeginEditCommand { get; }

    public ICommand? UpdateTextCommand
    {
        get => (ICommand?)GetValue(UpdateTextCommandProperty);
        set => SetValue(UpdateTextCommandProperty, value);
    }

    public static readonly DependencyProperty UpdateTextCommandProperty =
        DependencyProperty.Register(
            nameof(UpdateTextCommand),
            typeof(ICommand),
            typeof(EditableTextBlock));

    public bool DisallowSpecialCharacters
    {
        get => (bool)GetValue(DisallowSpecialCharactersProperty);
        set => SetValue(DisallowSpecialCharactersProperty, value);
    }

    public static readonly DependencyProperty DisallowSpecialCharactersProperty =
        DependencyProperty.Register(
            nameof(DisallowSpecialCharacters),
            typeof(bool),
            typeof(EditableTextBlock),
            new PropertyMetadata(false));

    private string _oldText = string.Empty;
    private TextBlock? _templateTextBlock;
    private TextBox? _templateTextBox;

    public override void OnApplyTemplate()
    {
        DetachTemplateEvents();

        base.OnApplyTemplate();

        _templateTextBlock = GetTemplateChild("PART_TextBlock") as TextBlock;
        _templateTextBox = GetTemplateChild("PART_TextBox") as TextBox;

        if (_templateTextBlock != null)
        {
            _templateTextBlock.MouseDown += OnTextBlockMouseDown;
        }

        if (_templateTextBox != null)
        {
            _templateTextBox.LostFocus += OnTextBoxLostFocus;
            _templateTextBox.KeyDown += OnTextBoxKeyDown;
        }
    }

    private void DetachTemplateEvents()
    {
        if (_templateTextBlock != null)
        {
            _templateTextBlock.MouseDown -= OnTextBlockMouseDown;
        }

        if (_templateTextBox != null)
        {
            _templateTextBox.LostFocus -= OnTextBoxLostFocus;
            _templateTextBox.KeyDown -= OnTextBoxKeyDown;
        }
    }

    private void OnTextBlockMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock || e.ClickCount != 2)
            return;

        BeginEdit();
    }

    private void BeginEdit()
    {
        if (IsEditing)
            return;

        IsEditing = true;
        _oldText = Text;

        // 等待编辑模板完成切换，避免文本框尚未显示时聚焦失败。
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _templateTextBox?.Focus();
            _templateTextBox?.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnTextBoxLostFocus(object sender, RoutedEventArgs e) => CommitEdit();

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                CommitEdit();
                e.Handled = true;
                break;
            case Key.Escape:
                // 取消编辑时回退原值
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                IsEditing = false;
                e.Handled = true;
                break;
        }
    }

    private void CommitEdit()
    {
        if (!IsEditing || _templateTextBox == null)
            return;

        UpdateTextCommand?.Execute((_oldText, _templateTextBox.Text));
        IsEditing = false;
    }
}
