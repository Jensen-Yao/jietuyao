using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using WindowsInput;

namespace STranslate.Helpers;

public class InputHelper
{
    private static readonly InputSimulator InputSimulator = new();
    private static readonly Settings Settings= Ioc.Default.GetRequiredService<Settings>();

    /// <summary>
    /// 输出文本（根据设置自动选择键盘输入或剪贴板粘贴）。
    /// </summary>
    /// <param name="obj">待输出的对象。</param>
    public static void PrintText(object? obj)
    {
        if (obj?.ToString() is { } text)
            PrintText(text);
    }

    /// <summary>
    /// 输出文本（根据设置自动选择键盘输入或剪贴板粘贴）。
    /// </summary>
    /// <param name="content">待输出文本。</param>
    public static void PrintText(string? content)
    {
        // 检查内容是否为空或仅包含空白字符
        if (string.IsNullOrEmpty(content)) return;

        if (Settings.UseClipboardOutput)
        {
            PrintTextWithClipboard(content);
            return;
        }

        // 分割字符串为多行
        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

        // 处理流式输出中单独的换行符号: \r\n  \r  \n  \n\n
        if (lines.All(x => x == ""))
        {
            // 一个换行会分割出两个空字符串，所以长度减一
            for (var i = 0; i < lines.Length - 1; i++) InputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);

            // 长度为1的情况为空字符串直接返回
            return;
        }

        foreach (var line in lines)
        {
            InputSimulator.Keyboard.TextEntry(line);
            // 模拟按下回车键，除了最后一行
            if (!line.Equals(lines.Last())) InputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
        }
    }

    /// <summary>
    ///     使用粘贴输出
    ///     * 避免部分用户无法正常输出的问题
    /// </summary>
    /// <param name="content">待输出文本。</param>
    public static void PrintTextWithClipboard(string content)
    {
        // 空内容直接返回，避免污染剪贴板。
        if (string.IsNullOrEmpty(content)) return;
        ClipboardHelper.SetText(content);
        ClipboardHelper.SendCtrlCV(false);
    }

    /// <summary>
    /// 删除指定数量的退格字符。
    /// </summary>
    /// <param name="count">退格次数。</param>
    public static void Backspace(int count = 1)
    {
        for (var i = 0; i < count; i++)
            InputSimulator.Keyboard.KeyPress(VirtualKeyCode.BACK);
    }
}
