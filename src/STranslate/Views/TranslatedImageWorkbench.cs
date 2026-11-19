using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using STranslate.Plugin;
using STranslate.Core.ClipboardHistory;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace STranslate.Views;

/// <summary>
/// 译图工作台（深度吸收 SnipTrans Workbench）：原图/译图对照、可编辑 OCR 再翻译、
/// 语言对切换重渲染、译文/译图复制贴存、成对历史。
/// </summary>
public sealed class TranslatedImageWorkbench : Window
{
    private static readonly (string Label, string Tag)[] Targets =
    [
        ("自动推断", ""),
        ("中文(简体)", "zh-Hans"), ("中文(繁體)", "zh-Hant"),
        ("English", "en"), ("日本語", "ja"), ("한국어", "ko"),
        ("Français", "fr"), ("Deutsch", "de"), ("Español", "es"), ("Русский", "ru"),
    ];

    private readonly Settings _settings;
    private readonly Rect _physicalBounds;
    private readonly List<TranslatedLine> _lines;

    private Bitmap _original;
    private Bitmap _translated;
    private LangEnum _target;
    private string _targetTag = "";
    private string _info = string.Empty;

    private readonly System.Windows.Controls.Image _viewer = new();
    private readonly TextBox _ocrBox = new();
    private readonly TextBox _resultBox = new();
    private readonly ComboBox _targetBox = new();
    private readonly TextBlock _infoText = new();
    private readonly ToggleButton _origTab;
    private readonly ToggleButton _transTab;
    private readonly Button _regenButton;
    private readonly Button _recaptureButton;
    private readonly Button _translateTextButton;
    private readonly ScrollViewer _scroll;

    public TranslatedImageWorkbench(Bitmap original, Bitmap translated, List<TranslatedLine> lines,
        string info, LangEnum target, Rect physicalBounds)
    {
        _original = original;
        _translated = translated;
        _lines = lines;
        _info = info;
        _target = target;
        _targetTag = TagFromLang(target);
        _physicalBounds = physicalBounds;
        _settings = Ioc.Default.GetRequiredService<Settings>();

        WindowStyle = WindowStyle.ToolWindow;
        Title = "jietuyao 译图工作台";
        Width = 1080;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(38, 38, 38));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(210, GridUnitType.Pixel) });

        // ===== 顶栏 =====
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _origTab = TabBtn("原图");
        _transTab = TabBtn("译图");
        _origTab.Click += (_, _) => ShowImage(false);
        _transTab.Click += (_, _) => ShowImage(true);
        top.Children.Add(_origTab);
        top.Children.Add(_transTab);

        _targetBox.MinWidth = 120;
        _targetBox.Margin = new Thickness(12, 0, 6, 0);
        _targetBox.VerticalAlignment = VerticalAlignment.Center;
        foreach (var (label, tag) in Targets)
            _targetBox.Items.Add(new KeyValuePair<string, string>(label, tag));
        _targetBox.DisplayMemberPath = "Key";
        _targetBox.SelectedValuePath = "Value";
        _targetBox.SelectedValue = _targetTag;
        _targetBox.SelectionChanged += async (_, _) =>
        {
            _targetTag = _targetBox.SelectedValue as string ?? "";
            var parsed = TranslatedImageController.ParseTarget(_targetTag);
            if (parsed is { } lang && lang != _target)
                await RegenerateAsync(lang);
        };
        top.Children.Add(_targetBox);

        _regenButton = Btn("重新生成 (R)", async () => await RegenerateAsync(EffectiveTarget()), accent: true);
        top.Children.Add(_regenButton);
        _recaptureButton = Btn("重新识别 (F5)", async () => await RecaptureAsync());
        top.Children.Add(_recaptureButton);
        top.Children.Add(Btn("复制译文", CopyText));
        top.Children.Add(Btn("复制译图", () => CopyImage(_translated)));
        top.Children.Add(Btn("贴图 (T)", () => PinImage(_translated)));
        top.Children.Add(Btn("保存 (S)", () => SaveImage(_translated)));
        top.Children.Add(Btn("历史…", OpenHistory));
        top.Children.Add(Btn("关闭 (Esc)", Close));

        _infoText.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
        _infoText.FontSize = 12;
        _infoText.VerticalAlignment = VerticalAlignment.Center;
        _infoText.Margin = new Thickness(10, 0, 0, 0);
        _infoText.TextTrimming = TextTrimming.CharacterEllipsis;
        _infoText.MaxWidth = 260;
        _infoText.ToolTip = info;
        top.Children.Add(_infoText);

        Grid.SetRow(top, 0);
        root.Children.Add(top);

        // ===== 图像区 =====
        _scroll = new ScrollViewer
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _viewer.Stretch = Stretch.None;
        _viewer.Margin = new Thickness(8);
        _scroll.Content = _viewer;
        Grid.SetRow(_scroll, 1);
        root.Children.Add(_scroll);

        // ===== 文本区 =====
        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        var ocrGroup = Group("OCR 原文（可编辑）", _ocrBox);
        _ocrBox.AcceptsReturn = true;
        _ocrBox.TextWrapping = TextWrapping.Wrap;
        _ocrBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _ocrBox.FontFamily = new FontFamily("Consolas");
        _ocrBox.Text = string.Join("\n", _lines.Select(l => l.Original));
        Grid.SetColumn(ocrGroup, 0);
        bottom.Children.Add(ocrGroup);

        _resultBox.AcceptsReturn = true;
        _resultBox.IsReadOnly = true;
        _resultBox.TextWrapping = TextWrapping.Wrap;
        _resultBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _resultBox.FontFamily = new FontFamily("Consolas");
        _resultBox.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        _resultBox.Text = string.Join("\n", _lines.Select(l => l.Translation));
        var resultGroup = Group("译文", _resultBox);
        Grid.SetColumn(resultGroup, 1);
        bottom.Children.Add(resultGroup);

        _translateTextButton = Btn("翻译文本框内容 →", async () =>
        {
            var text = await TranslatedImageController.TranslateTextAsync(_ocrBox.Text, EffectiveTarget());
            if (!string.IsNullOrEmpty(text))
                _resultBox.Text = text;
        });
        Grid.SetColumn(_translateTextButton, 1);
        Panel.SetZIndex(_translateTextButton, 5);
        _translateTextButton.HorizontalAlignment = HorizontalAlignment.Right;
        _translateTextButton.VerticalAlignment = VerticalAlignment.Bottom;
        _translateTextButton.Margin = new Thickness(0, 0, 30, 8);
        bottom.Children.Add(_translateTextButton);

        bottom.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);

        Content = root;

        PreviewKeyDown += OnKey;
        Closed += (_, _) =>
        {
            _original.Dispose();
            _translated.Dispose();
        };

        ShowImage(true);
        ApplyAutoActions(firstTime: true);
    }

    private static string TagFromLang(LangEnum lang) => lang switch
    {
        LangEnum.ChineseSimplified => "zh-Hans",
        LangEnum.ChineseTraditional => "zh-Hant",
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.German => "de",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        _ => "",
    };

    private LangEnum EffectiveTarget() =>
        TranslatedImageController.ParseTarget(_targetTag) ?? _target;

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.R when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
            case Key.R:
                _ = RegenerateAsync(EffectiveTarget());
                e.Handled = true;
                break;
            case Key.F5:
                _ = RecaptureAsync();
                e.Handled = true;
                break;
            case Key.T:
                PinImage(_translated);
                e.Handled = true;
                break;
            case Key.S:
                SaveImage(_translated);
                e.Handled = true;
                break;
        }
    }

    private void ShowImage(bool translated)
    {
        _viewer.Source = ScreenshotCapturer.ToBitmapSource(translated ? _translated : _original);
        _origTab.IsChecked = !translated;
        _transTab.IsChecked = translated;
    }

    private async Task RegenerateAsync(LangEnum target)
    {
        _regenButton.IsEnabled = false;
        try
        {
            var outcome = await TranslatedImageController.RenderWithTargetAsync(_original, _lines, target);
            if (outcome.Image is { } image)
            {
                _translated.Dispose();
                _translated = image;
                _target = outcome.Target;
                _targetTag = TagFromLang(_target);
                if (_targetBox.SelectedValue as string != _targetTag)
                    _targetBox.SelectedValue = _targetTag;
                _resultBox.Text = string.Join("\n", outcome.Lines.Select(l => l.Translation));
                ReplaceLines(outcome.Lines);
                _info = outcome.Info;
                _infoText.Text = _info;
                ShowImage(true);
                ArchiveCurrent();
                if (_settings.ImageOverlayAutoCopyText)
                    CopyText();
            }
        }
        finally
        {
            _regenButton.IsEnabled = true;
        }
    }

    private async Task RecaptureAsync()
    {
        _recaptureButton.IsEnabled = false;
        try
        {
            var outcome = await TranslatedImageController.CreateAsync(_original, EffectiveTarget());
            if (outcome.Image is { } image)
            {
                _translated.Dispose();
                _translated = image;
                _target = outcome.Target;
                _targetTag = TagFromLang(_target);
                if (_targetBox.SelectedValue as string != _targetTag)
                    _targetBox.SelectedValue = _targetTag;
                ReplaceLines(outcome.Lines);
                _ocrBox.Text = string.Join("\n", _lines.Select(l => l.Original));
                _resultBox.Text = string.Join("\n", _lines.Select(l => l.Translation));
                _info = outcome.Info;
                _infoText.Text = _info;
                ShowImage(true);
                ArchiveCurrent();
            }
        }
        finally
        {
            _recaptureButton.IsEnabled = true;
        }
    }

    private void ReplaceLines(List<TranslatedLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
    }

    private void ApplyAutoActions(bool firstTime)
    {
        if (firstTime)
        {
            ArchiveCurrent();
            if (_settings.ImageOverlayAutoPin)
                PinImage(_translated);
        }
        if (_settings.ImageOverlayAutoCopyText)
            CopyText();
    }

    private void ArchiveCurrent()
    {
        try
        {
            CaptureHistoryStore.Save(_original, _translated, TagFromLang(_target),
                string.Join("\n", _lines.Take(5).Select(l => l.Original)));
        }
        catch
        {
            // 历史失败不阻塞主流程
        }
    }

    private void CopyText()
    {
        try { Clipboard.SetText(string.Join("\n", _lines.Select(l => l.Translation))); }
        catch { /* 占用 */ }
    }

    private static void CopyImage(Bitmap bitmap)
    {
        using var clone = new Bitmap(bitmap);
        ScreenshotOutput.CopyToClipboard(clone);
    }

    private void PinImage(Bitmap bitmap)
    {
        using var clone = new Bitmap(bitmap);
        PinImageManager.Instance.Pin(clone, new Rect(
            _physicalBounds.X, _physicalBounds.Y, clone.Width, clone.Height), 1.0);
    }

    private static void SaveImage(Bitmap bitmap)
    {
        using var clone = new Bitmap(bitmap);
        ScreenshotOutput.SaveToFile(clone);
    }

    private void OpenHistory()
    {
        var dialog = new CaptureHistoryWindow { Owner = this };
        dialog.LoadRequested += item =>
        {
            var (orig, trans) = CaptureHistoryStore.LoadImages(item);
            if (orig == null || trans == null)
                return;
            _original.Dispose();
            _translated.Dispose();
            _original = orig;
            _translated = trans;
            _lines.Clear();
            ShowImage(true);
        };
        dialog.Show();
        dialog.Activate();
    }

    private ToggleButton TabBtn(string text) => new()
    {
        Content = text,
        Padding = new Thickness(14, 4, 14, 4),
        Margin = new Thickness(0, 0, 6, 0),
        Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
        BorderThickness = new Thickness(0),
    };

    private Button Btn(string text, Action action, bool accent = false) => new()
    {
        Content = text,
        Margin = new Thickness(4, 0, 4, 0),
        Padding = new Thickness(12, 4, 12, 4),
        Cursor = Cursors.Hand,
        Background = accent
            ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
            : new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
        BorderThickness = new Thickness(0),
    };

    private static Border Group(string header, TextBox box)
    {
        var panel = new DockPanel();
        var label = new TextBlock
        {
            Text = header,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
            Margin = new Thickness(2, 0, 0, 4),
        };
        DockPanel.SetDock(label, Dock.Top);
        panel.Children.Add(label);
        box.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        box.Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));
        box.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
        box.Padding = new Thickness(6);
        panel.Children.Add(box);
        return new Border { Child = panel, Margin = new Thickness(0, 0, 6, 0) };
    }
}

/// <summary>译图历史窗口：双击载入工作台 / 右键删除 / 清空</summary>
public sealed class CaptureHistoryWindow : Window
{
    private readonly ListBox _list;

    public event Action<CaptureHistoryItem>? LoadRequested;

    public CaptureHistoryWindow()
    {
        WindowStyle = WindowStyle.ToolWindow;
        Title = "译图历史";
        Width = 560;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(38, 38, 38));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var panel = new DockPanel { Margin = new Thickness(10) };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var clear = new Button
        {
            Content = "清空历史",
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderThickness = new Thickness(0),
        };
        clear.Click += (_, _) => { CaptureHistoryStore.Clear(); Reload(); };
        var hint = new TextBlock
        {
            Text = "双击载入 · 右键删除",
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 12,
        };
        bar.Children.Add(clear);
        bar.Children.Add(hint);
        DockPanel.SetDock(bar, Dock.Top);
        panel.Children.Add(bar);

        _list = new ListBox
        {
            Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderThickness = new Thickness(0),
        };
        _list.MouseDoubleClick += (_, _) =>
        {
            if ((_list.SelectedItem as ListBoxItem)?.Tag is CaptureHistoryItem item)
                LoadRequested?.Invoke(item);
        };
        _list.PreviewMouseRightButtonUp += (_, e) =>
        {
            if ((_list.SelectedItem as ListBoxItem)?.Tag is CaptureHistoryItem item)
            {
                CaptureHistoryStore.Delete(item);
                Reload();
                e.Handled = true;
            }
        };
        panel.Children.Add(_list);
        Content = panel;
        Reload();
    }

    private void Reload()
    {
        _list.Items.Clear();
        foreach (var item in CaptureHistoryStore.List())
        {
            var oneLine = item.Preview.Replace("\r", "").Replace("\n", " ⏎ ");
            _list.Items.Add(new ListBoxItem
            {
                Content = $"{item.Time:MM-dd HH:mm}  [{item.Target}]  {oneLine}",
                Tag = item,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            });
        }
    }
}
