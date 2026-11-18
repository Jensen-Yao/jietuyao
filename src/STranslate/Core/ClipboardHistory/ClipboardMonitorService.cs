using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace STranslate.Core.ClipboardHistory;

/// <summary>
/// 剪贴板监听：消息窗口 + AddClipboardFormatListener。
/// 自己写入的内容跳过（序列号 + 窗口归属双重判定），突发更新去抖合并。
/// </summary>
public sealed class ClipboardMonitorService : IDisposable
{
    private HwndSource? _source;
    private readonly DispatcherTimer _debounce;
    private uint _lastOwnSequence;
    private bool _running;

    /// <summary>剪贴板更新（在 UI 线程触发，已去抖）</summary>
    public event Action? ClipboardUpdated;

    public bool IsRunning => _running;

    public ClipboardMonitorService()
    {
        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            ClipboardUpdated?.Invoke();
        };
    }

    /// <summary>开始监听（须在 UI 线程调用）</summary>
    public void Start()
    {
        if (_running || Application.Current?.Dispatcher.CheckAccess() != true)
            return;

        _source = new HwndSource(new HwndSourceParameters("jietuyao.clip.monitor")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        });
        _source.AddHook(WndProc);
        PInvoke.AddClipboardFormatListener(new HWND(_source.Handle));
        _running = true;
    }

    public void Stop()
    {
        if (!_running)
            return;
        _debounce.Stop();
        if (_source != null)
        {
            try { PInvoke.RemoveClipboardFormatListener(new HWND(_source.Handle)); } catch { /* 句柄已亡 */ }
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
        _running = false;
    }

    private unsafe nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_CLIPBOARDUPDATE = 0x031D;
        if (msg == WM_CLIPBOARDUPDATE)
        {
            handled = true;
            // 自己写入 → 跳过
            var seq = PInvoke.GetClipboardSequenceNumber();
            if (seq != 0 && seq == _lastOwnSequence)
                return 0;
            if (IsOwnedBySelf())
                return 0;

            _debounce.Stop();
            _debounce.Start();
        }
        return 0;
    }

    private static unsafe bool IsOwnedBySelf()
    {
        try
        {
            var ownerPtr = PInvoke.GetClipboardOwner();
            if (ownerPtr == null)
                return false;
            var owner = new HWND((nint)ownerPtr);
            PInvoke.GetWindowThreadProcessId(owner, out var pid);
            return pid == Environment.ProcessId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>写入剪贴板前登记（用于自写忽略）</summary>
    public uint MarkOwnWrite()
    {
        _lastOwnSequence = PInvoke.GetClipboardSequenceNumber();
        return _lastOwnSequence;
    }

    public void Dispose() => Stop();
}

/// <summary>
/// 剪贴板读取与写入工具（UI 线程）
/// </summary>
public static class ClipboardDataExchange
{
    /// <summary>读取当前剪贴板为条目；空/失败返回 null</summary>
    public static ClipboardItemRecord? Capture()
    {
        IDataObject? data = null;
        try
        {
            data = Clipboard.GetDataObject();
        }
        catch
        {
            return null;
        }
        if (data == null)
            return null;

        try
        {
            // 文件优先
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                if (data.GetData(DataFormats.FileDrop) is System.Collections.Specialized.StringCollection files && files.Count > 0)
                {
                    var list = files.Cast<string>().ToList();
                    return new ClipboardItemRecord
                    {
                        Type = ClipboardItemType.Files,
                        Files = list,
                        Text = string.Join("\n", list),
                        ContentHash = Hash("files\n" + string.Join("\n", list)),
                    };
                }
            }

            // HTML（附带纯文本）
            if (data.GetDataPresent(DataFormats.Html))
            {
                var html = data.GetData(DataFormats.Html) as string;
                var text = data.GetDataPresent(DataFormats.UnicodeText) ? data.GetData(DataFormats.UnicodeText) as string : null;
                if (!string.IsNullOrEmpty(html))
                {
                    return new ClipboardItemRecord
                    {
                        Type = ClipboardItemType.Html,
                        Html = html,
                        Text = text ?? StripHtml(html),
                        ContentHash = Hash("html\n" + html),
                    };
                }
            }

            // 图片
            if (data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent(DataFormats.Dib))
            {
                if (data.GetData(DataFormats.Bitmap) is System.Windows.Media.Imaging.BitmapSource source)
                {
                    var png = EncodePng(source);
                    return new ClipboardItemRecord
                    {
                        Type = ClipboardItemType.Image,
                        Text = string.Empty,
                        ImageData = png,
                        ContentHash = Hash(png),
                    };
                }
            }

            // 纯文本
            if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
            {
                var text = (data.GetData(DataFormats.UnicodeText) ?? data.GetData(DataFormats.Text)) as string;
                if (!string.IsNullOrEmpty(text))
                {
                    return new ClipboardItemRecord
                    {
                        Type = ClipboardItemType.Text,
                        Text = text,
                        ContentHash = Hash("text\n" + text),
                    };
                }
            }
        }
        catch
        {
            // 剪贴板被占用等
            return null;
        }
        return null;
    }

    private static byte[] EncodePng(System.Windows.Media.Imaging.BitmapSource source)
    {
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static string StripHtml(string html)
    {
        // 剪贴板 HTML 头之后取正文并去标签
        var start = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            var gt = html.IndexOf('>', start);
            if (gt >= 0) html = html[(gt + 1)..];
        }
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }

    public static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..32];
    }

    public static string Hash(byte[] content)
    {
        var bytes = SHA256.HashData(content);
        return Convert.ToHexString(bytes)[..32];
    }
}
