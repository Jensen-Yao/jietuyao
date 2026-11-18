using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace STranslate.Core.ClipboardHistory;

/// <summary>剪贴板条目类型</summary>
public enum ClipboardItemType
{
    Text,
    Image,
    Files,
    Html,
}

/// <summary>剪贴板历史条目</summary>
public sealed class ClipboardItemRecord
{
    public long Id { get; set; }

    public ClipboardItemType Type { get; set; }

    /// <summary>文本内容（text/html 的纯文本 / files 的换行列表）</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>HTML 源码（仅 Html 类型）</summary>
    public string? Html { get; set; }

    /// <summary>图片相对路径（仅 Image 类型，相对 images 目录）</summary>
    public string? ImagePath { get; set; }

    /// <summary>文件路径 JSON 数组（仅 Files 类型）</summary>
    public List<string> Files { get; set; } = [];

    /// <summary>图片 PNG 字节（入库前临时承载，不持久化到此字段之外的存储）</summary>
    public byte[]? ImageData { get; init; }

    public bool Pinned { get; set; }

    public long PasteCount { get; set; }

    public long? GroupId { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string GroupEmoji { get; set; } = string.Empty;

    /// <summary>内容哈希（去重）</summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>列表预览文本</summary>
    public string Preview => Type switch
    {
        ClipboardItemType.Image => $"[图片] {ImageSizeHint}",
        ClipboardItemType.Files => $"[文件×{Files.Count}] {string.Join("; ", Files.Take(3))}",
        ClipboardItemType.Html => $"[网页] {OneLine(Text, 120)}",
        _ => OneLine(Text, 200),
    };

    /// <summary>类型图标</summary>
    public string Icon => Type switch
    {
        ClipboardItemType.Image => "🖼",
        ClipboardItemType.Files => "📁",
        ClipboardItemType.Html => "🌐",
        _ => "📄",
    };

    /// <summary>时间显示</summary>
    public string TimeText => CreatedAt.Date == DateTime.Today
        ? CreatedAt.ToString("HH:mm:ss")
        : CreatedAt.ToString("MM-dd HH:mm");

    /// <summary>分组徽标</summary>
    public string GroupBadge => string.IsNullOrEmpty(GroupName) ? string.Empty : $"{GroupEmoji}{GroupName}";

    /// <summary>粘贴次数显示</summary>
    public string PasteText => PasteCount > 0 ? $"粘贴×{PasteCount}" : string.Empty;

    /// <summary>置顶可见性</summary>
    public Visibility PinnedVisibility => Pinned ? Visibility.Visible : Visibility.Collapsed;

    private string ImageSizeHint => ImagePath is null ? string.Empty : Path.GetFileName(ImagePath);

    private static string OneLine(string s, int max)
    {
        var flattened = s.Replace("\r", "").Replace("\n", " ⏎ ");
        return flattened.Length <= max ? flattened : flattened[..max] + "…";
    }
}

/// <summary>剪贴板分组</summary>
public sealed class ClipboardGroupRecord
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Emoji { get; set; } = "📁";

    public int Sort { get; set; }
}

/// <summary>
/// 剪贴板历史 SQLite 存储
/// </summary>
public sealed class ClipboardHistoryStorage : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _imagesDirectory;

    private static string DbPath => Path.Combine(DataLocation.CacheDirectory, "clipboard_history.db");

    private static string ImagesRoot => Path.Combine(DataLocation.CacheDirectory, "clipboard_images");

    public ClipboardHistoryStorage()
    {
        Directory.CreateDirectory(DataLocation.CacheDirectory);
        _imagesDirectory = ImagesRoot;
        Directory.CreateDirectory(_imagesDirectory);

        _connection = new SqliteConnection($"Data Source={DbPath}");
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS items(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type TEXT NOT NULL,
                text TEXT,
                html TEXT,
                image_path TEXT,
                files TEXT,
                pinned INTEGER DEFAULT 0,
                paste_count INTEGER DEFAULT 0,
                group_id INTEGER,
                content_hash TEXT,
                created_at TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_items_created ON items(created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_items_hash ON items(content_hash);
            CREATE TABLE IF NOT EXISTS groups(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                emoji TEXT DEFAULT '📁',
                sort INTEGER DEFAULT 0
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>写入条目（重复内容置顶更新时间而非新增）</summary>
    public ClipboardItemRecord? Upsert(ClipboardItemRecord item, long limit)
    {
        var existing = FindByHash(item.ContentHash);
        if (existing != null)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "UPDATE items SET created_at = $now WHERE id = $id";
                cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("O"));
                cmd.Parameters.AddWithValue("$id", existing.Id);
                cmd.ExecuteNonQuery();
            }
            existing.CreatedAt = DateTime.Now;
            return existing;
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO items(type, text, html, image_path, files, pinned, paste_count, group_id, content_hash, created_at)
                VALUES($type, $text, $html, $img, $files, $pin, $pc, $gid, $hash, $now);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$type", item.Type.ToString());
            cmd.Parameters.AddWithValue("$text", item.Text);
            cmd.Parameters.AddWithValue("$html", (object?)item.Html ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$img", (object?)item.ImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$files", JsonSerializer.Serialize(item.Files));
            cmd.Parameters.AddWithValue("$pin", item.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$pc", item.PasteCount);
            cmd.Parameters.AddWithValue("$gid", (object?)item.GroupId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$hash", item.ContentHash);
            cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("O"));
            item.Id = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        Trim(limit);
        return item;
    }

    /// <summary>超限时裁剪（置顶豁免）</summary>
    private void Trim(long limit)
    {
        if (limit <= 0) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM items
            WHERE id IN (
                SELECT id FROM items WHERE pinned = 0 ORDER BY created_at DESC LIMIT -1 OFFSET $limit
            )
            """;
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.ExecuteNonQuery();
    }

    public ClipboardItemRecord? FindByHash(string hash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE content_hash = $hash LIMIT 1";
        cmd.Parameters.AddWithValue("$hash", hash);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    /// <summary>分页查询：类型过滤/搜索/分组/仅置顶</summary>
    public (List<ClipboardItemRecord> Items, long Total) Query(
        string? search = null,
        ClipboardItemType? type = null,
        long? groupId = null,
        bool pinnedOnly = false,
        int offset = 0,
        int count = 100)
    {
        var where = new List<string>();
        if (type != null) where.Add("i.type = $type");
        if (groupId == -1) where.Add("i.group_id IS NULL");
        else if (groupId != null) where.Add("i.group_id = $gid");
        if (pinnedOnly) where.Add("i.pinned = 1");
        if (!string.IsNullOrWhiteSpace(search)) where.Add("i.text LIKE $search");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

        long total;
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM items i {whereSql}";
            BindQueryParams(cmd, search, type, groupId);
            total = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        using var query = _connection.CreateCommand();
        query.CommandText = $"""
            SELECT i.*, g.name AS gname, g.emoji AS gemoji
            FROM items i LEFT JOIN groups g ON g.id = i.group_id
            {whereSql}
            ORDER BY i.pinned DESC, i.created_at DESC
            LIMIT $count OFFSET $offset
            """;
        BindQueryParams(query, search, type, groupId);
        query.Parameters.AddWithValue("$count", count);
        query.Parameters.AddWithValue("$offset", offset);

        var list = new List<ClipboardItemRecord>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
            list.Add(ReadItem(reader));
        return (list, total);
    }

    private static void BindQueryParams(SqliteCommand cmd, string? search, ClipboardItemType? type, long? groupId)
    {
        if (type != null) cmd.Parameters.AddWithValue("$type", type.ToString());
        if (groupId is > 0) cmd.Parameters.AddWithValue("$gid", groupId.Value);
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$search", $"%{search}%");
    }

    private static ClipboardItemRecord ReadItem(SqliteDataReader reader)
    {
        var filesRaw = reader["files"] as string;
        var item = new ClipboardItemRecord
        {
            Id = (long)reader["id"],
            Type = Enum.Parse<ClipboardItemType>((string)reader["type"]),
            Text = (reader["text"] as string) ?? string.Empty,
            Html = reader["html"] as string,
            ImagePath = reader["image_path"] as string,
            Files = string.IsNullOrEmpty(filesRaw) ? [] : JsonSerializer.Deserialize<List<string>>(filesRaw) ?? [],
            Pinned = (long)reader["pinned"] == 1,
            PasteCount = (long)reader["paste_count"],
            GroupId = reader["group_id"] is long g ? g : null,
            ContentHash = (reader["content_hash"] as string) ?? string.Empty,
            CreatedAt = DateTime.TryParse((string)reader["created_at"], out var t) ? t : DateTime.Now,
            GroupName = (reader["gname"] as string) ?? string.Empty,
            GroupEmoji = (reader["gemoji"] as string) ?? string.Empty,
        };
        return item;
    }

    public void SetPinned(long id, bool pinned)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE items SET pinned = $p WHERE id = $id";
        cmd.Parameters.AddWithValue("$p", pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void MoveToGroup(long id, long? groupId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE items SET group_id = $g WHERE id = $id";
        cmd.Parameters.AddWithValue("$g", (object?)groupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void IncrementPasteCount(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE items SET paste_count = paste_count + 1 WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        var item = GetById(id);
        if (item?.ImagePath != null)
        {
            try { File.Delete(Path.Combine(_imagesDirectory, item.ImagePath)); } catch { /* 并发删除 */ }
        }
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Clear(bool includePinned)
    {
        foreach (var item in Query(null, null, null, pinnedOnly: false, 0, 1_000_000).Items.Where(i => !i.Pinned || includePinned))
            Delete(item.Id);
    }

    public ClipboardItemRecord? GetById(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT i.*, g.name AS gname, g.emoji AS gemoji FROM items i LEFT JOIN groups g ON g.id = i.group_id WHERE i.id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    // ===== 分组 =====
    public List<ClipboardGroupRecord> GetGroups()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM groups ORDER BY sort, id";
        using var reader = cmd.ExecuteReader();
        var list = new List<ClipboardGroupRecord>();
        while (reader.Read())
        {
            list.Add(new ClipboardGroupRecord
            {
                Id = (long)reader["id"],
                Name = (string)reader["name"],
                Emoji = (reader["emoji"] as string) ?? "📁",
                Sort = (int)(long)reader["sort"],
            });
        }
        return list;
    }

    public ClipboardGroupRecord CreateGroup(string name, string emoji = "📁")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO groups(name, emoji) VALUES($n, $e); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$e", emoji);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new ClipboardGroupRecord { Id = id, Name = name, Emoji = emoji };
    }

    public void RenameGroup(long id, string name, string emoji)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE groups SET name = $n, emoji = $e WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$e", emoji);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGroup(long id)
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE items SET group_id = NULL WHERE group_id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM groups WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>保存图片并返回相对文件名</summary>
    public string SaveImage(byte[] pngBytes)
    {
        var name = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        File.WriteAllBytes(Path.Combine(_imagesDirectory, name), pngBytes);
        return name;
    }

    public byte[] LoadImage(string relativePath)
    {
        return File.ReadAllBytes(Path.Combine(_imagesDirectory, relativePath));
    }

    // ===== CSV 导入导出 =====

    /// <summary>导出全部条目为 CSV（文本类完整，图片导出其文本占位）</summary>
    public long ExportCsv(string path)
    {
        var (items, _) = Query(null, null, null, pinnedOnly: false, 0, 1_000_000);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("type,text,html,pinned,paste_count,created_at");
        foreach (var i in items)
        {
            sb.AppendLine(string.Join(",",
                Csv(i.Type.ToString()), Csv(i.Text), Csv(i.Html ?? string.Empty),
                i.Pinned ? "1" : "0", i.PasteCount.ToString(), i.CreatedAt.ToString("O")));
        }
        File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
        return items.Count;
    }

    /// <summary>导入 CSV（type,text,html,pinned,paste_count,created_at），返回导入条数</summary>
    public long ImportCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2)
            return 0;
        var imported = 0L;
        foreach (var line in lines[1..])
        {
            var fields = ParseCsvLine(line);
            if (fields.Count < 2 || !Enum.TryParse<ClipboardItemType>(fields[0], out var type))
                continue;
            var text = fields[1];
            if (string.IsNullOrEmpty(text))
                continue;
            var item = new ClipboardItemRecord
            {
                Type = type,
                Text = text,
                Html = type == ClipboardItemType.Html && fields.Count > 2 ? fields[2] : null,
                Pinned = fields.Count > 3 && fields[3] == "1",
                PasteCount = fields.Count > 4 && long.TryParse(fields[4], out var pc) ? pc : 0,
                CreatedAt = fields.Count > 5 && DateTime.TryParse(fields[5], out var t) ? t : DateTime.Now,
                ContentHash = ClipboardDataExchange.Hash(type + "\n" + text),
            };
            Upsert(item, 0);
            imported++;
        }
        return imported;
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped.Replace("\r", " ").Replace("\n", "⏎")}\"";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    public void Dispose() => _connection.Dispose();
}
