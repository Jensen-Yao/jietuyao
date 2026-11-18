#!/usr/bin/env python3
"""生成 PP-OCR 本地 ONNX 插件项目并注册到 slnx"""
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "src" / "Plugins"
WECHAT = ROOT / "STranslate.Plugin.Ocr.WeChatBuiltIn"
NAME = "STranslate.Plugin.Ocr.PpOnnx"
root = ROOT / NAME
(root / "View").mkdir(parents=True, exist_ok=True)
(root / "ViewModel").mkdir(parents=True, exist_ok=True)
(root / "Languages").mkdir(parents=True, exist_ok=True)

csproj = """<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
        <!-- PaddleOCR ONNX 本地引擎：完全离线 -->
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
        <PackageReference Include="Microsoft.ML.OnnxRuntime" />
    </ItemGroup>

</Project>
""" % (NAME, NAME)

plugin_json = json.dumps({
    "PluginID": "a1d4e9c2f8b3465789d0e1f2a3b4c5d6",
    "Name": "PP-OCR (Local)",
    "Description": "Local offline PaddleOCR (ONNX) plugin for jietuyao",
    "Author": "Jensen-Yao",
    "Version": "1.0.0",
    "Website": "https://github.com/Jensen-Yao/jietuyao",
    "ExecuteFileName": NAME + ".dll",
    "IconPath": "icon.png",
}, indent=2, ensure_ascii=False)

settings = """namespace STranslate.Plugin.Ocr.PpOnnx;

public class Settings
{
    /// <summary>检测模型路径（如 PP-OCRv6_det_small.onnx）</summary>
    public string DetModelPath { get; set; } = string.Empty;

    /// <summary>识别模型路径（如 PP-OCRv6_rec_small.onnx，字典内嵌 metadata）</summary>
    public string RecModelPath { get; set; } = string.Empty;

    /// <summary>推理线程数</summary>
    public int Threads { get; set; } = 2;
}
"""

vm = """using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.PpOnnx.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        DetModelPath = Settings.DetModelPath;
        RecModelPath = Settings.RecModelPath;
        Threads = Settings.Threads;
        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DetModelPath): Settings.DetModelPath = DetModelPath; break;
            case nameof(RecModelPath): Settings.RecModelPath = RecModelPath; break;
            case nameof(Threads): Settings.Threads = Math.Clamp(Threads, 1, 8); break;
            default: return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string DetModelPath { get; set; }
    [ObservableProperty] public partial string RecModelPath { get; set; }
    [ObservableProperty] public partial int Threads { get; set; }
}
"""

view = """<UserControl
    x:Class="STranslate.Plugin.Ocr.PpOnnx.View.SettingsView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
    xmlns:vm="clr-namespace:STranslate.Plugin.Ocr.PpOnnx.ViewModel"
    d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <ikw:SimpleStackPanel Spacing="12">
        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_PpOnnx_DetModel}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Document_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox MinWidth="360" Text="{Binding DetModelPath, UpdateSourceTrigger=PropertyChanged}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_PpOnnx_RecModel}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Document_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox MinWidth="360" Text="{Binding RecModelPath, UpdateSourceTrigger=PropertyChanged}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_PpOnnx_Threads}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.PerformanceMetrics_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox MinWidth="80" Text="{Binding Threads, UpdateSourceTrigger=PropertyChanged}" />
        </ui:SettingsCard>

        <ui:SettingsCard Description="PaddleOCR det/rec ONNX 模型" Header="{DynamicResource STranslate_Plugin_Ocr_PpOnnx_Official}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.WebAsset_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <ui:HyperlinkButton Content="https://github.com/PaddlePaddle/PaddleOCR" NavigateUri="https://github.com/PaddlePaddle/PaddleOCR" />
        </ui:SettingsCard>

    </ikw:SimpleStackPanel>
</UserControl>
"""

view_cs = """namespace STranslate.Plugin.Ocr.PpOnnx.View;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();
}
"""

(root / (NAME + ".csproj")).write_text(csproj, encoding="utf-8")
(root / "plugin.json").write_text(plugin_json + "\n", encoding="utf-8")
(root / "Settings.cs").write_text(settings, encoding="utf-8")
(root / "ViewModel" / "SettingsViewModel.cs").write_text(vm, encoding="utf-8")
(root / "View" / "SettingsView.xaml").write_text(view, encoding="utf-8")
(root / "View" / "SettingsView.xaml.cs").write_text(view_cs, encoding="utf-8")
shutil.copy(WECHAT / "icon.png", root / "icon.png")

LOCALES = {
    "zh-cn": ("PP-OCR 本地识别", "完全离线的本地 OCR（PaddleOCR ONNX）",
              ("检测模型路径", "识别模型路径", "推理线程数", "模型来源")),
    "en": ("PP-OCR (Local)", "Fully offline local OCR (PaddleOCR ONNX)",
           ("Detection model path", "Recognition model path", "Inference threads", "Model source")),
}
NS = "STranslate_Plugin_Ocr_PpOnnx"
for locale in ["en", "zh-cn", "zh-tw", "ja", "ko", "ru", "tr", "uk", "fa"]:
    name_, desc_, (det, rec, thr, off) = LOCALES.get(locale, LOCALES["en"])
    entries = "\n".join([
        f'    <sys:String x:Key="{NS}_DetModel">{det}</sys:String>',
        f'    <sys:String x:Key="{NS}_RecModel">{rec}</sys:String>',
        f'    <sys:String x:Key="{NS}_Threads">{thr}</sys:String>',
        f'    <sys:String x:Key="{NS}_Official">{off}</sys:String>',
    ])
    xaml = ('<ResourceDictionary\n    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n'
            '    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n'
            '    xmlns:sys="clr-namespace:System;assembly=mscorlib">\n\n' + entries + "\n\n</ResourceDictionary>\n")
    (root / "Languages" / (locale + ".xaml")).write_text(xaml, encoding="utf-8")
    (root / "Languages" / (locale + ".json")).write_text(
        json.dumps({"Name": name_, "Description": desc_}, ensure_ascii=False, indent=2), encoding="utf-8")

# 注册 slnx
slnx = Path(__file__).resolve().parent.parent / "src" / "STranslate.slnx"
t = slnx.read_text(encoding="utf-8")
anchor = '    <Project Path="Plugins/STranslate.Plugin.Ocr.WeChatBuiltIn/STranslate.Plugin.Ocr.WeChatBuiltIn.csproj" />'
if "Ocr.PpOnnx" not in t:
    t = t.replace(anchor, anchor + "\n    <!-- jietuyao v2：本地 PP-OCR -->\n"
                   f'    <Project Path="Plugins/{NAME}/{NAME}.csproj" />')
    slnx.write_text(t, encoding="utf-8")
    print("slnx updated")
print("PpOnnx plugin created")
