using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using STranslate.Core;
using STranslate.Core.ClipboardHistory;

namespace STranslate.Views;

/// <summary>
/// 剪贴板历史窗口（jietuyao v2）
/// </summary>
public sealed partial class ClipboardHistoryWindow : Window
{
    private const int PageSize = 100;

    private readonly ClipboardHistoryService _service;
    private readonly Settings _settings;
    private readonly DispatcherTimer _searchDebounce;

    private int _page = 1;
    private ClipboardItemType? _typeFilter;
    private long? _groupFilter;
    private bool _pinnedOnly;
    private string _search = string.Empty;
    private long _total;

    public ClipboardHistoryWindow(ClipboardHistoryService service, Settings settings)
    {
        _service = service;
        _settings = settings;
        InitializeComponent();

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); _page = 1; LoadItems(); };

        AutoPasteCheck.IsChecked = _settings.ClipboardHistoryAutoPaste;
        MonitorCheck.IsChecked = _service.IsMonitoring;

        _service.HistoryChanged += OnHistoryChanged;
        Deactivated += (_, _) => Hide();
        Loaded += (_, _) => LoadGroups();
        Closed += (_, _) => _service.HistoryChanged -= OnHistoryChanged;
    }

    private void OnHistoryChanged() =>
        Dispatcher.Invoke(() => { if (IsVisible) { _page = 1; LoadItems(); } });

    /// <summary>在光标附近显示</summary>
    public void ShowAtCursor()
    {
        LoadGroups();
        _page = 1;
        LoadItems();

        // Win32 光标坐标换 dip 定位
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        if (Windows.Win32.PInvoke.GetCursorPos(out var cursor))
        {
            Left = Math.Clamp(cursor.X / scale - Width / 2, 0, Math.Max(0, SystemParameters.VirtualScreenWidth - Width));
            Top = Math.Clamp(cursor.Y / scale + 20, 0, Math.Max(0, SystemParameters.VirtualScreenHeight - Height));
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        Show();
        Activate();
        HistoryList.Focus();
        if (HistoryList.Items.Count > 0)
            HistoryList.SelectedIndex = 0;
    }

    public void RefreshData()
    {
        _page = 1;
        LoadGroups();
        LoadItems();
    }

    // ===== 数据加载 =====
    private void LoadItems()
    {
        var (items, total) = _service.Storage.Query(
            string.IsNullOrWhiteSpace(_search) ? null : _search,
            _typeFilter, _groupFilter, _pinnedOnly, 0, _page * PageSize);
        _total = total;
        HistoryList.ItemsSource = items;
        CountText.Text = _total == 0 ? "暂无记录" : $"共 {_total} 条";
        LoadMoreButton.Visibility = items.Count < _total ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadGroups()
    {
        var groups = _service.Storage.GetGroups();
        var selection = GroupFilterBox.SelectedIndex;
        GroupFilterBox.Items.Clear();
        GroupFilterBox.Items.Add(new ComboBoxItem { Content = "全部分组", Tag = null });
        GroupFilterBox.Items.Add(new ComboBoxItem { Content = "未分组", Tag = -1L });
        foreach (var g in groups)
            GroupFilterBox.Items.Add(new ComboBoxItem { Content = $"{g.Emoji} {g.Name}", Tag = g.Id });
        GroupFilterBox.Items.Add(new Separator());
        GroupFilterBox.Items.Add(new ComboBoxItem { Content = "➕ 新建分组…", Tag = "create" });
        GroupFilterBox.Items.Add(new ComboBoxItem { Content = "⚙ 管理分组…", Tag = "manage" });
        GroupFilterBox.SelectedIndex = Math.Max(0, selection);
    }

    // ===== 事件 =====
    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down || e.Key == Key.Enter)
        {
            HistoryList.Focus();
            if (HistoryList.Items.Count > 0 && HistoryList.SelectedIndex < 0)
                HistoryList.SelectedIndex = 0;
            e.Handled = true;
        }
    }

    private void OnTypeFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio || radio.Tag is not string tag)
            return;
        _typeFilter = tag switch
        {
            "Text" => ClipboardItemType.Text,
            "Image" => ClipboardItemType.Image,
            "Files" => ClipboardItemType.Files,
            "Html" => ClipboardItemType.Html,
            _ => null,
        };
        _pinnedOnly = tag == "pinned";
        _page = 1;
        LoadItems();
    }

    private void OnGroupFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupFilterBox.SelectedItem is not ComboBoxItem item)
            return;
        if (item.Tag is string s)
        {
            GroupFilterBox.SelectedIndex = 0;
            if (s == "create")
                CreateGroup();
            else if (s == "manage")
            {
                var dialog = new ClipboardGroupManagerDialog(_service.Storage) { Owner = this };
                dialog.ShowDialog();
                LoadGroups();
                LoadItems();
            }
            return;
        }
        _groupFilter = item.Tag switch
        {
            long id => id,
            _ => null,
        };
        _page = 1;
        LoadItems();
    }

    private void CreateGroup()
    {
        var name = InputDialog.Show("新建剪贴板分组", "分组名称：", "");
        if (string.IsNullOrWhiteSpace(name))
            return;
        _service.Storage.CreateGroup(name.Trim());
        LoadGroups();
    }

    private void OnLoadMore(object sender, RoutedEventArgs e)
    {
        _page++;
        LoadItems();
    }

    private async void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardItemRecord item)
            return;
        Hide();
        await _service.PasteAsync(item);
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && HistoryList.SelectedItem is ClipboardItemRecord item)
        {
            Hide();
            _ = _service.PasteAsync(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && HistoryList.SelectedItem is ClipboardItemRecord del)
        {
            _service.Storage.Delete(del.Id);
            LoadItems();
            e.Handled = true;
        }
    }

    private void OnItemRightClick(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(HistoryList);
        var source = e.OriginalSource as DependencyObject;
        var listBoxItem = FindAncestor<ListBoxItem>(source);
        if (listBoxItem?.Content is not ClipboardItemRecord item)
            return;
        HistoryList.SelectedItem = item;
        BuildItemContextMenu(item).IsOpen = true;
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T matched)
                return matched;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private ContextMenu BuildItemContextMenu(ClipboardItemRecord item)
    {
        var menu = new ContextMenu();
        void Add(string header, Action action, bool separator = false)
        {
            if (separator)
                menu.Items.Add(new Separator());
            var menuItem = new MenuItem { Header = header };
            menuItem.Click += (_, _) => action();
            menu.Items.Add(menuItem);
        }

        Add("粘贴 (Enter)", async () => { Hide(); await _service.PasteAsync(item); });
        Add("仅复制", () => _service.CopyToClipboard(item));
        Add(item.Pinned ? "取消置顶" : "置顶", () => { _service.Storage.SetPinned(item.Id, !item.Pinned); LoadItems(); });
        Add("删除 (Del)", () => { _service.Storage.Delete(item.Id); LoadItems(); });

        // 移动到分组
        var moveMenu = new MenuItem { Header = "移动到分组" };
        var groups = _service.Storage.GetGroups();
        var noneItem = new MenuItem { Header = "（未分组）" };
        noneItem.Click += (_, _) => { _service.Storage.MoveToGroup(item.Id, null); LoadItems(); };
        moveMenu.Items.Add(noneItem);
        foreach (var g in groups)
        {
            var gm = new MenuItem { Header = $"{g.Emoji} {g.Name}" };
            var gid = g.Id;
            gm.Click += (_, _) => { _service.Storage.MoveToGroup(item.Id, gid); LoadItems(); };
            moveMenu.Items.Add(gm);
        }
        var newGroup = new MenuItem { Header = "➕ 新建分组…" };
        newGroup.Click += (_, _) => { CreateGroup(); };
        moveMenu.Items.Add(new Separator());
        moveMenu.Items.Add(newGroup);
        menu.Items.Add(moveMenu);

        // 特殊粘贴（文本类）
        if (item.Type is ClipboardItemType.Text or ClipboardItemType.Html)
        {
            var transformMenu = new MenuItem { Header = "特殊粘贴" };
            foreach (var t in TextTransforms.All)
            {
                var tm = new MenuItem { Header = t.Name };
                var id = t.Id;
                tm.Click += (_, _) => { Hide(); _ = _service.PasteTransformedAsync(item, id); };
                transformMenu.Items.Add(tm);
            }
            menu.Items.Add(transformMenu);
        }

        return menu;
    }

    private void OnAutoPasteChanged(object sender, RoutedEventArgs e)
    {
        _settings.ClipboardHistoryAutoPaste = AutoPasteCheck.IsChecked == true;
    }

    private void OnMonitorChanged(object sender, RoutedEventArgs e)
    {
        if (MonitorCheck.IsChecked == true)
        {
            _settings.ClipboardHistoryEnabled = true;
            _service.StartMonitor();
        }
        else
        {
            _settings.ClipboardHistoryEnabled = false;
            _service.StopMonitor();
        }
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this, "清空全部未置顶的历史记录？", "确认",
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.OK)
        {
            _service.Storage.Clear(includePinned: false);
            LoadItems();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Hide();

    private void OnMoreActions(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var export = new MenuItem { Header = "导出 CSV…" };
        export.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出剪贴板历史",
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"jietuyao_clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };
            if (dialog.ShowDialog() == true)
            {
                var count = _service.Storage.ExportCsv(dialog.FileName);
                MessageBox.Show(this, $"已导出 {count} 条", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        var import = new MenuItem { Header = "导入 CSV…" };
        import.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入剪贴板历史",
                Filter = "CSV 文件 (*.csv)|*.csv",
            };
            if (dialog.ShowDialog() == true)
            {
                var count = _service.Storage.ImportCsv(dialog.FileName);
                LoadItems();
                MessageBox.Show(this, $"已导入 {count} 条", "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        menu.Items.Add(export);
        menu.Items.Add(import);
        if (sender is System.Windows.Controls.Primitives.ButtonBase source)
        {
            menu.PlacementTarget = source;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
