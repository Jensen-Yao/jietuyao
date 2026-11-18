using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using STranslate.Core.ClipboardHistory;

namespace STranslate.Views;

/// <summary>
/// 剪贴板分组管理对话框：新建 / 重命名（含 emoji）/ 删除
/// </summary>
public sealed class ClipboardGroupManagerDialog : Window
{
    private readonly ClipboardHistoryStorage _storage;
    private readonly ListBox _list;

    public ClipboardGroupManagerDialog(ClipboardHistoryStorage storage)
    {
        _storage = storage;

        WindowStyle = WindowStyle.ToolWindow;
        Title = "分组管理";
        Width = 380;
        Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var panel = new StackPanel { Margin = new Thickness(14) };

        _list = new ListBox
        {
            Height = 190,
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
        };
        panel.Children.Add(_list);

        var hint = new TextBlock
        {
            Text = "格式：emoji 名称（如 📁 工作）",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
            Margin = new Thickness(0, 6, 0, 6),
        };
        panel.Children.Add(hint);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var create = Btn("新建");
        create.Click += (_, _) =>
        {
            var input = InputDialog.Show("新建分组", "emoji 名称：", "📁 ");
            if (string.IsNullOrWhiteSpace(input))
                return;
            var (emoji, name) = SplitEmoji(input.Trim());
            if (name.Length == 0)
                return;
            _storage.CreateGroup(name, emoji);
            Reload();
        };
        var rename = Btn("重命名");
        rename.Click += (_, _) =>
        {
            if (_list.SelectedItem is not ClipboardGroupRecord g)
                return;
            var input = InputDialog.Show("重命名分组", "emoji 名称：", $"{g.Emoji} {g.Name}", this);
            if (string.IsNullOrWhiteSpace(input))
                return;
            var (emoji, name) = SplitEmoji(input.Trim());
            if (name.Length == 0)
                return;
            _storage.RenameGroup(g.Id, name, emoji);
            Reload();
        };
        var delete = Btn("删除");
        delete.Click += (_, _) =>
        {
            if (_list.SelectedItem is not ClipboardGroupRecord g)
                return;
            if (MessageBox.Show(this, $"删除分组“{g.Name}”？组内条目将变为未分组。", "确认",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            _storage.DeleteGroup(g.Id);
            Reload();
        };
        var close = Btn("关闭");
        close.Click += (_, _) => Close();
        buttons.Children.Add(create);
        buttons.Children.Add(rename);
        buttons.Children.Add(delete);
        buttons.Children.Add(close);
        panel.Children.Add(buttons);

        Content = panel;
        Reload();
    }

    private static (string Emoji, string Name) SplitEmoji(string input)
    {
        var emoji = string.Empty;
        var rest = input;
        foreach (var ch in input)
        {
            if (char.IsHighSurrogate(ch) || char.IsLowSurrogate(ch) ||
                ch >= 0x1F300 || (ch >= 0x2190 && ch <= 0x27BF))
            {
                emoji += ch;
                rest = rest[1..];
            }
            else if (char.IsWhiteSpace(ch) && emoji.Length > 0)
            {
                rest = rest[1..];
                break;
            }
            else
            {
                break;
            }
        }
        return (emoji.Length > 0 ? emoji : "📁", rest.Trim());
    }

    private Button Btn(string text)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
        };
        return button;
    }

    private void Reload()
    {
        var selected = (_list.SelectedItem as ClipboardGroupRecord)?.Id;
        _list.Items.Clear();
        foreach (var g in _storage.GetGroups())
        {
            _list.Items.Add(g);
            if (g.Id == selected)
                _list.SelectedItem = g;
        }
    }
}
