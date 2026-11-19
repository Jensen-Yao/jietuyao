using System.IO;
using System.Text.Json;

namespace STranslate.Core.ClipboardHistory;

/// <summary>译图历史条目（原图+译图成对存档，吸收自 SnipTrans HistoryService）</summary>
public sealed class CaptureHistoryItem
{
    public string Dir { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
}

/// <summary>
/// 译图历史存储：%Cache%\captures\{条目目录}\ orig.png + trans.png + meta.json，上限 100（最旧先删）
/// </summary>
public static class CaptureHistoryStore
{
    private const int Capacity = 100;
    private static readonly object IoLock = new();

    private static string Root => Directory.CreateDirectory(
        Path.Combine(Core.DataLocation.CacheDirectory, "captures")).FullName;

    public static CaptureHistoryItem Save(System.Drawing.Bitmap original, System.Drawing.Bitmap translated,
        string target, string preview)
    {
        lock (IoLock)
        {
            var dir = Path.Combine(Root, $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid().ToString("N")[..6]}");
            Directory.CreateDirectory(dir);
            original.Save(Path.Combine(dir, "orig.png"), System.Drawing.Imaging.ImageFormat.Png);
            translated.Save(Path.Combine(dir, "trans.png"), System.Drawing.Imaging.ImageFormat.Png);
            File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(new
            {
                time = DateTime.Now,
                target,
                preview = preview.Length > 300 ? preview[..300] : preview,
            }));
            PruneLocked();
            return new CaptureHistoryItem { Dir = dir, Time = DateTime.Now, Target = target, Preview = preview };
        }
    }

    public static List<CaptureHistoryItem> List()
    {
        lock (IoLock)
        {
            var items = new List<CaptureHistoryItem>();
            foreach (var dir in Directory.GetDirectories(Root).OrderByDescending(d => Path.GetFileName(d)))
            {
                try
                {
                    var metaPath = Path.Combine(dir, "meta.json");
                    if (!File.Exists(metaPath))
                        continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    var root = doc.RootElement;
                    items.Add(new CaptureHistoryItem
                    {
                        Dir = dir,
                        Time = root.TryGetProperty("time", out var t) && t.TryGetDateTime(out var time) ? time : DateTime.Now,
                        Target = root.TryGetProperty("target", out var tg) ? tg.GetString() ?? "" : "",
                        Preview = root.TryGetProperty("preview", out var p) ? p.GetString() ?? "" : "",
                    });
                }
                catch
                {
                    // 单条损坏跳过
                }
            }
            return items;
        }
    }

    public static (System.Drawing.Bitmap? Original, System.Drawing.Bitmap? Translated) LoadImages(CaptureHistoryItem item)
    {
        lock (IoLock)
        {
            System.Drawing.Bitmap? orig = null, trans = null;
            try { orig = new(Path.Combine(item.Dir, "orig.png")); } catch { }
            try { trans = new(Path.Combine(item.Dir, "trans.png")); } catch { }
            return (orig, trans);
        }
    }

    public static void Delete(CaptureHistoryItem item)
    {
        lock (IoLock)
        {
            try { Directory.Delete(item.Dir, true); } catch { /* 占用 */ }
        }
    }

    public static void Clear()
    {
        lock (IoLock)
        {
            foreach (var dir in Directory.GetDirectories(Root))
                try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void PruneLocked()
    {
        var dirs = Directory.GetDirectories(Root).OrderBy(d => Path.GetFileName(d)).ToList();
        for (var i = 0; i < dirs.Count - Capacity; i++)
            try { Directory.Delete(dirs[i], true); } catch { }
    }
}
