using STranslate.Plugin.Ocr.WeChatBuiltIn.View;
using STranslate.Plugin.Ocr.WeChatBuiltIn.ViewModel;
using System.IO;
using System.Windows.Controls;
using WeChatOcr;

namespace STranslate.Plugin.Ocr.WeChatBuiltIn;

public class Main : IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages =>
    [
        LangEnum.Auto,
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
    ];

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();

        // 设置必要依赖数据目录
        var pluginName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
        var baseLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", pluginName);
        DataLocation.SetBaseDirectory(baseLocation);
    }

    public void Dispose() { }

    public string? GetLanguage(LangEnum langEnum) => null;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<OcrResult>();

        using var ocr = new ImageOcr();
        ocr.Run(request.ImageData, (path, result) =>
        {
            if (result == null) return;
            var ocrResult = new OcrResult();
            var list = result?.OcrResult?.SingleResult;
            if (list == null)
            {
                //避免重复set
                tcs.SetResult(ocrResult.Fail("WeChatOCR get result is null"));
                return;
            }

            for (var i = 0; i < list?.Count; i++)
            {
                if (list[i] is not { } item || string.IsNullOrEmpty(item.SingleStrUtf8))
                    continue;

                var content = new OcrContent { Text = item.SingleStrUtf8 };
                var width = item.Right - item.Left;
                var height = item.Bottom - item.Top;
                var x = item.Left;
                var y = item.Top;
                Converter(x, y, width, height).ForEach(pg =>
                {
                    //仅位置不全为0时添加
                    if (!pg.X.Equals(pg.Y) || pg.X != 0)
                        content.BoxPoints.Add(new BoxPoint(pg.X, pg.Y));
                });
                ocrResult.OcrContents.Add(content);
            }

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore
            }

            tcs.SetResult(ocrResult);
        }, ImageType.Png);

        var timeoutTask = Task.Delay(10000, cancellationToken);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask) throw new TimeoutException("WeChatOCR operation timed out.");
        // 提取content的值
        var finalResult = await tcs.Task;

        if (_viewModel != null)
            _viewModel.UseCount++;
        return finalResult;
    }

    private List<BoxPoint> Converter(float x, float y, float width, float height)
    {
        return
        [
            //left top
            new BoxPoint(x, y),

            //right top
            new BoxPoint(x + width, y),

            //right bottom
            new BoxPoint(x + width, y + height),

            //left bottom
            new BoxPoint(x, y + height)
        ];
    }
}
