using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using Rect = System.Windows.Rect;
using Size = System.Drawing.Size;

namespace STranslate.Core;

/// <summary>
/// 长截图拼接：行级量化哈希 + 最优连续重叠搜索（C# 移植 v1 Rust 思路）
/// </summary>
public static class ScrollStitcher
{
    /// <summary>
    /// 拼接帧序列（自上而下滚动）。返回null当帧数为0。
    /// </summary>
    public static Bitmap Stitch(IReadOnlyList<Bitmap> frames, int directionDown = 1)
    {
        if (frames.Count == 0)
            return new Bitmap(1, 1);
        var list = directionDown == 1 ? frames : frames.Reverse().ToList();

        var first = list[0];
        var width = first.Width;

        // 预计算每帧行哈希
        var hashes = list.Select(f => ComputeRowHashes(f)).ToArray();
        var heights = list.Select(f => f.Height).ToArray();

        // 逐帧求最优重叠
        var offsets = new int[list.Count]; // 每帧在结果中的 y 偏移
        var totalHeight = heights[0];
        for (var i = 1; i < list.Count; i++)
        {
            var overlap = FindBestOverlap(hashes[i - 1], hashes[i], heights[i]);
            totalHeight += heights[i] - overlap;
            offsets[i] = totalHeight - heights[i];
            // 上移已拼接内容的哈希参照：用累积底缘（简化：逐对相邻帧比较足够稳）
        }

        // 修正：相邻帧比较已给出 offsets，直接绘制
        var result = new Bitmap(width, totalHeight, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(result))
        {
            for (var i = 0; i < list.Count; i++)
                g.DrawImage(list[i], 0, offsets[i], width, heights[i]);
        }
        return result;
    }

    /// <summary>计算整图内容指纹（用于跳过未滚动的重复帧）</summary>
    public static ulong ImageFingerprint(Bitmap bitmap)
    {
        var rows = ComputeRowHashes(bitmap);
        if (rows.Length == 0) return 0;
        var acc = 0UL;
        foreach (var r in rows)
            acc = acc * 31 + r;
        return acc;
    }

    /// <summary>
    /// 行哈希：每行采样量化（每4像素取 B 通道高4位），滚动时的亚像素差异可容忍
    /// </summary>
    internal static ulong[] ComputeRowHashes(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var hashes = new ulong[height];
            var row = new byte[width * 4];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                var hash = 14695981039346656037UL; // FNV offset
                for (var x = 0; x < width; x += 4)
                {
                    // 取 B/G/R 高 4 位（抗轻微压缩/渲染抖动）
                    var b = row[x * 4] >> 4;
                    var g = row[x * 4 + 1] >> 4;
                    var r = row[x * 4 + 2] >> 4;
                    hash = (hash ^ (ulong)((r << 8) | (g << 4) | b)) * 1099511628211UL;
                }
                hashes[y] = hash;
            }
            return hashes;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>
    /// 在上一帧（作为累积底缘）与本帧顶缘之间找最优重叠行数。
    /// 要求从交界处开始的连续匹配；无有效匹配时按 35% 高度盲拼。
    /// </summary>
    internal static int FindBestOverlap(ulong[] previous, ulong[] current, int currentHeight, int minMatch = 10)
    {
        var maxOverlap = Math.Min(previous.Length, currentHeight) - 2;
        var minOverlap = Math.Max(minMatch, currentHeight / 10);
        if (maxOverlap <= minOverlap)
            return Math.Max(0, maxOverlap / 2);

        var bestOverlap = -1;
        var bestRun = 0;
        for (var overlap = maxOverlap; overlap >= minOverlap; overlap--)
        {
            var run = 0;
            for (var i = 0; i < overlap; i++)
            {
                if (previous[previous.Length - overlap + i] == current[i])
                    run++;
                else
                    break;
            }
            if (run > bestRun)
            {
                bestRun = run;
                bestOverlap = overlap;
            }
            if (run == overlap)
                break; // 完美匹配
        }

        if (bestRun >= minMatch)
            return bestOverlap;

        // 未找到匹配（可能是纯色/滚动过快），按 35% 盲拼保守处理
        return (int)(currentHeight * 0.35);
    }

    /// <summary>
    /// 截取屏幕物理区域（冻结单帧）
    /// </summary>
    public static Bitmap CaptureRegion(Rect region)
    {
        var w = Math.Max(1, (int)Math.Round(region.Width));
        var h = Math.Max(1, (int)Math.Round(region.Height));
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen((int)region.X, (int)region.Y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        return bitmap;
    }
}
