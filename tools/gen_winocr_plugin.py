#!/usr/bin/env python3
"""生成 Windows.Media.Ocr 本地 OCR 插件（吸收自 SnipTrans，零依赖离线）并注册 slnx"""
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "src" / "Plugins"
WECHAT = ROOT / "STranslate.Plugin.Ocr.WeChatBuiltIn"
NAME = "STranslate.Plugin.Ocr.WindowsOcr"
root = ROOT / NAME
for sub in ["View", "ViewModel", "Languages"]:
    (root / sub).mkdir(parents=True, exist_ok=True)

csproj = """<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <!-- 需版本化 windows TFM 以获得 WinRT (Windows.Media.Ocr) 投影 -->
        <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
        <UseWPF>true</UseWPF>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
        <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
        <!-- Windows OCR：系统内置本地引擎，零依赖零上传 -->
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
        <DebugSymbols>true</DebugSymbols>
        <DebugType>portable</DebugType>
        <Optimize>false</Optimize>
        <OutputPath>..\\..\\.artifacts\\Debug\\Plugins\\%s\\</OutputPath>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
        <DebugType>none</DebugType>
        <Optimize>true</Optimize>
        <OutputPath>..\\..\\.artifacts\\Release\\Plugins\\%s\\</OutputPath>
        <DefineConstants>TRACE;RELEASE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <ItemGroup>
        <Content Include="Languages\\*.*">
            <Generator>MSBuild:Compile</Generator>
            <SubType>Designer</SubType>
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </Content>
        <Content Include="icon.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
        <Content Include="plugin.json">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\\..\\STranslate.Plugin\\STranslate.Plugin.csproj" />
    </ItemGroup>

</Project>
""" % (NAME, NAME)

plugin_json = json.dumps({
    "PluginID": "b7e2c5f01a9d4e38b6c4d2e7f5a81093",
    "Name": "Windows OCR (Local)",
    "Description": "Zero-dependency local OCR via Windows.Media.Ocr (absorbed from SnipTrans)",
    "Author": "Jensen-Yao",
    "Version": "1.0.0",
    "Website": "https://github.com/Jensen-Yao/jietuyao",
    "ExecuteFileName": NAME + ".dll",
    "IconPath": "icon.png",
}, indent=2, ensure_ascii=False)

main_cs = r'''using System.IO;
using System.Windows.Controls;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using STranslate.Plugin.Ocr.WindowsOcr.ViewModel;

namespace STranslate.Plugin.Ocr.WindowsOcr;

/// <summary>
/// Windows 本地 OCR（Windows.Media.Ocr）：系统内置引擎，零依赖零上传；
/// 行级包围盒（词框聚合，移植自 SnipTrans OcrService）。
/// </summary>
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
        LangEnum.Japanese,
        LangEnum.Korean,
        LangEnum.French,
        LangEnum.German,
        LangEnum.Italian,
        LangEnum.Spanish,
        LangEnum.Russian,
        LangEnum.PortuguesePortugal,
        LangEnum.PortugueseBrazil,
    ];

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() { }

    /// <summary>LangEnum → BCP-47（Auto/未映射返回 null = 跟随系统）</summary>
    private static string? GetBcp47(LangEnum lang) => lang switch
    {
        LangEnum.ChineseSimplified => "zh-CN",
        LangEnum.ChineseTraditional => "zh-TW",
        LangEnum.Cantonese => "zh-HK",
        LangEnum.English => "en-US",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr-FR",
        LangEnum.German => "de-DE",
        LangEnum.Italian => "it-IT",
        LangEnum.Spanish => "es-ES",
        LangEnum.Russian => "ru-RU",
        LangEnum.PortuguesePortugal => "pt-PT",
        LangEnum.PortugueseBrazil => "pt-BR",
        _ => null,
    };

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        var result = new OcrResult();
        try
        {
            var engine = CreateEngine(GetBcp47(request.Language) ?? Settings.LanguageTag);
            if (engine == null)
                return result.Fail("系统无可用 OCR 语言包：设置 → 时间和语言 → 语言 → 添加语言并勾选\"联机语音/OCR\"可选功能");

            using var bitmap = Decode(request.ImageData);
            if (bitmap == null)
                return result.Fail("图片解码失败");

            var software = await CreateSoftwareBitmapAsync(bitmap);
            var ocr = await engine.RecognizeAsync(software);

            // 一个区域：每行一个段落（段落内一行），行包围盒 = 词框 min/max 聚合
            var region = new OcrRegion();
            foreach (var line in ocr.Lines)
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach (var word in line.Words)
                {
                    var r = word.BoundingRect;
                    minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y);
                    maxX = Math.Max(maxX, r.Right); maxY = Math.Max(maxY, r.Bottom);
                }
                if (minX > maxX) continue;

                var content = new OcrContent { Text = line.Text };
                content.BoxPoints.Add(new BoxPoint((float)minX, (float)minY));
                content.BoxPoints.Add(new BoxPoint((float)maxX, (float)minY));
                content.BoxPoints.Add(new BoxPoint((float)maxX, (float)maxY));
                content.BoxPoints.Add(new BoxPoint((float)minX, (float)maxY));
                result.OcrContents.Add(content);

                var paragraph = new OcrParagraph();
                paragraph.Lines.Add(content);
                region.Paragraphs.Add(paragraph);
            }
            result.Regions.Add(region);
            return result;
        }
        catch (Exception ex)
        {
            return result.Fail($"Windows OCR 失败：{ex.Message}");
        }
    }

    internal static OcrEngine? CreateEngine(string? bcp47)
    {
        if (!string.IsNullOrWhiteSpace(bcp47))
        {
            var lang = new Language(bcp47);
            if (OcrEngine.IsLanguageSupported(lang))
            {
                var byLang = OcrEngine.TryCreateFromLanguage(lang);
                if (byLang != null) return byLang;
            }
        }
        // 设置页手填优先 → 指定语言 → 用户系统语言列表
        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    internal static System.Drawing.Bitmap? Decode(byte[] data)
    {
        using var ms = new MemoryStream(data);
        try { return new System.Drawing.Bitmap(ms); }
        catch { return null; }
    }

    internal static async Task<SoftwareBitmap> CreateSoftwareBitmapAsync(System.Drawing.Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        png.Position = 0;
        var decoder = await BitmapDecoder.CreateAsync(png.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync();
    }
}
'''.lstrip()

settings_cs = """namespace STranslate.Plugin.Ocr.WindowsOcr;

public class Settings
{
    /// <summary>首选语言 BCP-47 标签（空 = 请求语言/系统自动）</summary>
    public string LanguageTag { get; set; } = string.Empty;
}
"""

vm_cs = """using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.WindowsOcr.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        LanguageTag = Settings.LanguageTag;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LanguageTag)) return;
        Settings.LanguageTag = LanguageTag?.Trim() ?? string.Empty;
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= OnPropertyChanged;

    [ObservableProperty] public partial string LanguageTag { get; set; }
}
"""

view_xaml = """<UserControl
    x:Class="STranslate.Plugin.Ocr.WindowsOcr.View.SettingsView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
    xmlns:vm="clr-namespace:STranslate.Plugin.Ocr.WindowsOcr.ViewModel"
    d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <ikw:SimpleStackPanel Spacing="12">
        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_WinOcr_Tag}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.LocalLanguage_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox MinWidth="220" Text="{Binding LanguageTag, UpdateSourceTrigger=PropertyChanged}" />
        </ui:SettingsCard>

        <ui:SettingsCard Description="{DynamicResource STranslate_Plugin_Ocr_WinOcr_InfoDesc}" Header="{DynamicResource STranslate_Plugin_Ocr_WinOcr_Info}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Info_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <ui:HyperlinkButton Content="ms-settings:regionlanguage" NavigateUri="ms-settings:regionlanguage" />
        </ui:SettingsCard>
    </ikw:SimpleStackPanel>
</UserControl>
"""

view_cs = """namespace STranslate.Plugin.Ocr.WindowsOcr.View;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();
}
"""

(root / (NAME + ".csproj")).write_text(csproj, encoding="utf-8")
(root / "plugin.json").write_text(plugin_json + "\n", encoding="utf-8")
(root / "Main.cs").write_text(main_cs, encoding="utf-8")
(root / "Settings.cs").write_text(settings_cs, encoding="utf-8")
(root / "ViewModel" / "SettingsViewModel.cs").write_text(vm_cs, encoding="utf-8")
(root / "View" / "SettingsView.xaml").write_text(view_xaml, encoding="utf-8")
(root / "View" / "SettingsView.xaml.cs").write_text(view_cs, encoding="utf-8")
shutil.copy(WECHAT / "icon.png", root / "icon.png")

LOCALES = {
    "zh-cn": ("Windows 本地识别", "系统内置 OCR（Windows.Media.Ocr），零依赖离线运行",
              ("首选语言标签（BCP-47，可空）", "关于", "如 zh-CN / en-US / ja；留空按请求语言或系统自动。语言包在 Windows 设置中添加。")),
    "en": ("Windows OCR (Local)", "Built-in system OCR (Windows.Media.Ocr), zero-dependency offline",
           ("Preferred language tag (BCP-47, optional)", "About", "e.g. zh-CN / en-US / ja; empty = auto. Language packs are added in Windows Settings.")),
}
NS = "STranslate_Plugin_Ocr_WinOcr"
for locale in ["en", "zh-cn", "zh-tw", "ja", "ko", "ru", "tr", "uk", "fa"]:
    name_, desc_, (tag, about, info) = LOCALES.get(locale, LOCALES["en"])
    entries = "\n".join([
        f'    <sys:String x:Key="{NS}_Tag">{tag}</sys:String>',
        f'    <sys:String x:Key="{NS}_Info">{about}</sys:String>',
        f'    <sys:String x:Key="{NS}_InfoDesc">{info}</sys:String>',
    ])
    xaml = ('<ResourceDictionary\n    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n'
            '    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n'
            '    xmlns:sys="clr-namespace:System;assembly=mscorlib">\n\n' + entries + "\n\n</ResourceDictionary>\n")
    (root / "Languages" / (locale + ".xaml")).write_text(xaml, encoding="utf-8")
    (root / "Languages" / (locale + ".json")).write_text(
        json.dumps({"Name": name_, "Description": desc_}, ensure_ascii=False, indent=2), encoding="utf-8")

slnx = Path(__file__).resolve().parent.parent / "src" / "jietuyao.slnx"
t = slnx.read_text(encoding="utf-8")
anchor = f'    <Project Path="Plugins/{NAME}/{NAME}.csproj" />'
if NAME not in t:
    pp = f'Plugins/STranslate.Plugin.Ocr.PpOnnx/STranslate.Plugin.Ocr.PpOnnx.csproj'
    t = t.replace(f'    <Project Path="{pp}" />',
                  f'    <Project Path="{pp}" />\n    <!-- jietuyao：Windows 系统本地 OCR（吸收自 SnipTrans） -->\n    <Project Path="Plugins/{NAME}/{NAME}.csproj" />')
    slnx.write_text(t, encoding="utf-8")
    print("slnx updated")
print("WindowsOcr plugin created")
