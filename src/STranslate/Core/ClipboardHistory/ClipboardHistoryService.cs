using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using STranslate.Helpers;

namespace STranslate.Core.ClipboardHistory;

/// <summary>
/// 剪贴板历史门面：监听入库 + 查询 + 粘贴/复制动作 + 窗口管理
/// </summary>
public sealed class ClipboardHistoryService : IDisposable
{
    private readonly Settings _settings;
    private readonly ILogger<ClipboardHistoryService>? _logger;
    private readonly ClipboardMonitorService _monitor;
    private readonly ClipboardHistoryStorage _storage;
    private Views.ClipboardHistoryWindow? _window;

    /// <summary>历史变化（窗口刷新）</summary>
    public event Action? HistoryChanged;

    public ClipboardHistoryStorage Storage => _storage;

    public bool IsMonitoring => _monitor.IsRunning;

    public ClipboardHistoryService(Settings settings, ILogger<ClipboardHistoryService>? logger = null)
    {
        _settings = settings;
        _logger = logger;
        _monitor = new ClipboardMonitorService();
        _storage = new ClipboardHistoryStorage();

        _monitor.ClipboardUpdated += OnClipboardUpdated;
        if (_settings.ClipboardHistoryEnabled)
            StartMonitor();
    }

    public void StartMonitor()
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(() => _monitor.Start());
    }

    public void StopMonitor() => _monitor.Stop();

    private void OnClipboardUpdated()
    {
        if (!_settings.ClipboardHistoryEnabled)
            return;
        try
        {
            var item = ClipboardDataExchange.Capture();
            if (item == null)
                return;
            // 过滤过短/噪音内容
            if (item.Type == ClipboardItemType.Text && item.Text.Trim().Length == 0)
                return;

            if (item.Type == ClipboardItemType.Image && item.ImageData != null)
                item.ImagePath = _storage.SaveImage(item.ImageData);

            var stored = _storage.Upsert(item, _settings.ClipboardHistoryLimit);
            if (stored != null)
                HistoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "剪贴板入库失败");
        }
    }

    // ===== 动作 =====

    /// <summary>仅复制回剪贴板（不粘贴）</summary>
    public bool CopyToClipboard(ClipboardItemRecord item)
    {
        var ok = WriteToClipboard(item);
        if (ok)
            _storage.IncrementPasteCount(item.Id);
        return ok;
    }

    /// <summary>复制并自动粘贴（模拟 Ctrl+V，可关）</summary>
    public async Task PasteAsync(ClipboardItemRecord item)
    {
        if (!WriteToClipboard(item))
            return;
        _storage.IncrementPasteCount(item.Id);
        _storage.Upsert(item, _settings.ClipboardHistoryLimit); // 触发置顶
        HistoryChanged?.Invoke();

        if (!_settings.ClipboardHistoryAutoPaste)
            return;

        await Task.Delay(120);
        ClipboardHelper.SendCtrlCV(false);
    }

    /// <summary>变换后粘贴（特殊粘贴）</summary>
    public async Task PasteTransformedAsync(ClipboardItemRecord item, string transformId)
    {
        if (item.Type != ClipboardItemType.Text && item.Type != ClipboardItemType.Html)
            return;
        var transformed = TextTransforms.ApplyById(transformId, item.Text);
        if (transformed == null)
            return;

        _monitor.MarkOwnWrite();
        try { Clipboard.SetText(transformed); } catch { return; }
        _storage.IncrementPasteCount(item.Id);

        if (_settings.ClipboardHistoryAutoPaste)
        {
            await Task.Delay(120);
            ClipboardHelper.SendCtrlCV(false);
        }
    }

    private bool WriteToClipboard(ClipboardItemRecord item)
    {
        _monitor.MarkOwnWrite();
        try
        {
            switch (item.Type)
            {
                case ClipboardItemType.Files:
                {
                    var collection = new System.Collections.Specialized.StringCollection();
                    collection.AddRange(item.Files.ToArray());
                    Clipboard.SetFileDropList(collection);
                    return true;
                }
                case ClipboardItemType.Image:
                {
                    if (item.ImagePath == null)
                        return false;
                    var bytes = _storage.LoadImage(item.ImagePath);
                    var image = new BitmapImage();
                    using (var stream = new MemoryStream(bytes))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                    }
                    image.Freeze();
                    Clipboard.SetImage(image);
                    return true;
                }
                case ClipboardItemType.Html when item.Html != null:
                {
                    var data = new DataObject();
                    data.SetText(item.Text, TextDataFormat.UnicodeText);
                    data.SetText(item.Html, TextDataFormat.Html);
                    Clipboard.SetDataObject(data, true);
                    return true;
                }
                default:
                    if (string.IsNullOrEmpty(item.Text))
                        return false;
                    Clipboard.SetText(item.Text);
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "写剪贴板失败");
            return false;
        }
    }

    // ===== 窗口 =====

    public void ToggleWindow()
    {
        if (_window != null && _window.IsVisible)
        {
            _window.Hide();
            return;
        }
        _window ??= new Views.ClipboardHistoryWindow(this, _settings);
        _window.RefreshData();
        _window.ShowAtCursor();
    }

    public void CloseWindow()
    {
        _window?.Close();
        _window = null;
    }

    public void Dispose()
    {
        _monitor.ClipboardUpdated -= OnClipboardUpdated;
        _monitor.Dispose();
        _storage.Dispose();
        _window?.Close();
    }
}
