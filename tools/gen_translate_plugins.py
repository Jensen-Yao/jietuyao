#!/usr/bin/env python3
"""生成 Azure / Amazon 翻译插件项目骨架（jietuyao v2 承诺补齐 v1 引擎）"""
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "src" / "Plugins"
BAIDU = ROOT / "STranslate.Plugin.Translate.Baidu"
LOCALES = ["en", "zh-cn", "zh-tw", "ja", "ko", "ru", "tr", "uk", "fa"]


def csproj(name: str) -> str:
    return f'''<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
        <DebugSymbols>true</DebugSymbols>
        <DebugType>portable</DebugType>
        <Optimize>false</Optimize>
        <OutputPath>..\\..\\.artifacts\\Debug\\Plugins\\{name}\\</OutputPath>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
        <DebugType>none</DebugType>
        <Optimize>true</Optimize>
        <OutputPath>..\\..\\.artifacts\\Release\\Plugins\\{name}\\</OutputPath>
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
'''


def plugin_json(pid: str, name: str, display: str) -> str:
    return f'''{{
  "PluginID": "{pid}",
  "Name": "{display}",
  "Description": "{display} plugin for jietuyao (ported from jietuyao v1)",
  "Author": "Jensen-Yao",
  "Version": "1.0.0",
  "Website": "https://github.com/Jensen-Yao/jietuyao",
  "ExecuteFileName": "{name}.dll",
  "IconPath": "icon.png"
}}
'''


AZURE_MAIN = r'''
using STranslate.Plugin.Translate.Azure.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.Azure;

public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    private const string DefaultUrl = "https://api.cognitive.microsofttranslator.com/translate";

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "",
        LangEnum.ChineseSimplified => "zh-Hans",
        LangEnum.ChineseTraditional => "zh-Hant",
        LangEnum.Cantonese => "zh-Hant",
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn-MN",
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => "km",
        LangEnum.NorwegianBokmal => "nb",
        LangEnum.NorwegianNynorsk => "nn",
        LangEnum.Persian => "fa",
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => "ug",
        _ => ""
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => GetSourceLanguage(langEnum);

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        var sourceStr = GetSourceLanguage(request.SourceLang);
        var targetStr = GetTargetLanguage(request.TargetLang);
        if (targetStr is null || targetStr.Length == 0)
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        var baseUrl = string.IsNullOrWhiteSpace(Settings.Endpoint)
            ? DefaultUrl
            : Settings.Endpoint.TrimEnd('/') + "/translate";

        var queryParams = new Dictionary<string, string>
        {
            ["api-version"] = "3.0",
            ["to"] = targetStr,
        };
        if (!string.IsNullOrEmpty(sourceStr))
            queryParams["from"] = sourceStr;

        var options = new Options
        {
            QueryParams = queryParams,
            Headers = new Dictionary<string, string>
            {
                ["Ocp-Apim-Subscription-Key"] = Settings.ApiKey,
            },
        };
        if (!string.IsNullOrWhiteSpace(Settings.Region))
            options.Headers!["Ocp-Apim-Subscription-Region"] = Settings.Region;

        var payload = new[] { new { Text = request.Text } };
        var response = await Context.HttpService.PostAsync(baseUrl, payload, options, cancellationToken);

        var parsed = JsonNode.Parse(response);
        if (parsed is not JsonArray array || array.Count == 0)
        {
            result.Fail($"Invalid response\nRaw:{response}");
            return;
        }

        var error = array[0]?["error"];
        if (error != null)
        {
            result.Fail($"{error["code"]}: {error["message"]}");
            return;
        }

        var data = string.Join(Environment.NewLine,
            array[0]?["translations"]?.OfType<JsonObject>()
                .Select(t => t["text"]?.ToString())
                .Where(t => !string.IsNullOrEmpty(t)) ?? []);
        if (data.Length == 0)
        {
            result.Fail($"No result\nRaw:{response}");
            return;
        }

        var detected = array[0]?["detectedLanguage"]?["language"]?.ToString();
        result.Success(data, detected);
    }
}
'''.lstrip()

AZURE_SETTINGS = '''
namespace STranslate.Plugin.Translate.Azure;

public class Settings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    /// <summary>自定义端点（如 VNET/区域网关），留空使用公共端点</summary>
    public string Endpoint { get; set; } = string.Empty;
}
'''.lstrip()

AZURE_VM = '''
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Azure.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        ApiKey = Settings.ApiKey;
        Region = Settings.Region;
        Endpoint = Settings.Endpoint;
        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ApiKey): Settings.ApiKey = ApiKey; break;
            case nameof(Region): Settings.Region = Region; break;
            case nameof(Endpoint): Settings.Endpoint = Endpoint; break;
            default: return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string ApiKey { get; set; }
    [ObservableProperty] public partial string Region { get; set; }
    [ObservableProperty] public partial string Endpoint { get; set; }
}
'''.lstrip()

AZURE_VIEW = '''<UserControl
    x:Class="STranslate.Plugin.Translate.Azure.View.SettingsView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:plugin="clr-namespace:STranslate.Plugin;assembly=STranslate.Plugin"
    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
    xmlns:vm="clr-namespace:STranslate.Plugin.Translate.Azure.ViewModel"
    d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <ikw:SimpleStackPanel Spacing="12">
        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Azure_ApiKey}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Key_24_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <PasswordBox
                MinWidth="300"
                plugin:PasswordBoxAssistant.Attach="True"
                plugin:PasswordBoxAssistant.Password="{Binding ApiKey}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Azure_Region}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.GlobeLocation_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox
                MinWidth="300"
                Text="{Binding Region, UpdateSourceTrigger=PropertyChanged}"
                placeholder:TextBoxHelper.PlaceholderText="global" xmlns:placeholder="http://schemas.inkore.net/lib/ui/wpf" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Azure_Endpoint}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Globe_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox
                MinWidth="300"
                Text="{Binding Endpoint, UpdateSourceTrigger=PropertyChanged}"
                placeholder2:TextBoxHelper.PlaceholderText="https://api.cognitive.microsofttranslator.com" xmlns:placeholder2="http://schemas.inkore.net/lib/ui/wpf" />
        </ui:SettingsCard>

        <ui:SettingsCard Description="Azure Portal → Translator" Header="{DynamicResource STranslate_Plugin_Translate_Azure_Official}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.WebAsset_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <ui:HyperlinkButton Content="https://portal.azure.com" NavigateUri="https://portal.azure.com" />
        </ui:SettingsCard>

    </ikw:SimpleStackPanel>
</UserControl>
'''

VIEW_CS = '''
namespace {ns}.View;

public partial class SettingsView
{{
    public SettingsView() => InitializeComponent();
}}
'''

AMAZON_MAIN = r'''
using STranslate.Plugin.Translate.Amazon.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.Amazon;

public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    private const string Target = "AWSShineFrontendService_20170701.TranslateText";
    private const string ContentType = "application/x-amz-json-1.1";

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => null, // Amazon 必须显式源语言，Auto 时用 en 检测由用户保证
        LangEnum.ChineseSimplified => "zh",
        LangEnum.ChineseTraditional => "zh-TW",
        LangEnum.Cantonese => "zh",
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn",
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => "km",
        LangEnum.NorwegianBokmal => "nb",
        LangEnum.NorwegianNynorsk => "nn",
        LangEnum.Persian => "fa",
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => null,
        _ => null
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => GetSourceLanguage(langEnum) is null && langEnum == LangEnum.Auto
        ? null
        : GetSourceLanguage(langEnum);

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        var sourceStr = GetSourceLanguage(request.SourceLang) ?? "auto";
        var targetStr = GetTargetLanguage(request.TargetLang);
        if (targetStr is null or "auto")
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.AccessKeyId) || string.IsNullOrWhiteSpace(Settings.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(Settings.Region))
        {
            result.Fail("Amazon Translate credentials are not configured");
            return;
        }

        var host = $"translate.{Settings.Region}.amazonaws.com";
        var url = $"https://{host}/";
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        // 预序列化载荷（签名与请求体必须字节一致）
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            Text = request.Text,
            SourceLanguageCode = sourceStr == "auto" ? DetectHeuristic(request.Text) : sourceStr,
            TargetLanguageCode = targetStr,
        });

        var headers = new Dictionary<string, string>
        {
            ["content-type"] = ContentType,
            ["x-amz-date"] = amzDate,
            ["x-amz-target"] = Target,
        };
        if (!string.IsNullOrWhiteSpace(Settings.SessionToken))
            headers["x-amz-security-token"] = Settings.SessionToken;

        var signedHeaders = string.Join(";", headers.Keys.OrderBy(k => k));
        var canonicalHeaders = string.Concat(headers.Keys.OrderBy(k => k)
            .Select(k => $"{k}:{headers[k].Trim()}\n"));
        var payloadHash = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var canonicalRequest = string.Join("\n", "POST", "/", "", canonicalHeaders, signedHeaders, payloadHash);
        var scope = $"{dateStamp}/{Settings.Region}/translate/aws4_request";
        var stringToSign = string.Join("\n",
            "AWS4-HMAC-SHA256", amzDate, scope, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var kDate = Hmac(Encoding.UTF8.GetBytes("AWS4" + Settings.SecretAccessKey), dateStamp);
        var kRegion = Hmac(kDate, Settings.Region);
        var kService = Hmac(kRegion, "translate");
        var kSigning = Hmac(kService, "aws4_request");
        var signature = Hex(Hmac(kSigning, stringToSign));

        headers["Authorization"] =
            $"AWS4-HMAC-SHA256 Credential={Settings.AccessKeyId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";
        headers["host"] = host;

        var options = new Options { Headers = headers, ContentType = ContentType };
        var response = await Context.HttpService.PostAsync(url, payload, options, cancellationToken);

        var parsed = JsonNode.Parse(response);
        var translated = parsed?["TranslatedText"]?.ToString();
        if (string.IsNullOrEmpty(translated))
        {
            var message = parsed?["message"]?.ToString() ?? parsed?["__type"]?.ToString();
            result.Fail(string.IsNullOrEmpty(message) ? $"Invalid response\nRaw:{response}" : message);
            return;
        }

        var detected = parsed?["SourceLanguageCode"]?.ToString();
        result.Success(translated, detected);
    }

    /// <summary>Auto 源语言的轻量启发检测（CJK 判定）</summary>
    private static string DetectHeuristic(string text)
    {
        if (text.Length == 0) return "en";
        var cjkCount = text.Count(char.IsBetweenHelper);
        return cjkCount * 2 >= text.Length ? "zh" : "en";
    }

    private static byte[] Hmac(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}

file static class CharExtensions
{
    public static bool IsBetweenHelper(this char c) => c >= 0x4E00 && c <= 0x9FFF;
}
'''.lstrip()

AMAZON_SETTINGS = '''
namespace STranslate.Plugin.Translate.Amazon;

public class Settings
{
    public string Region { get; set; } = "us-west-2";

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string SessionToken { get; set; } = string.Empty;
}
'''.lstrip()

AMAZON_VM = '''
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Amazon.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;
        Region = Settings.Region;
        AccessKeyId = Settings.AccessKeyId;
        SecretAccessKey = Settings.SecretAccessKey;
        SessionToken = Settings.SessionToken;
        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Region): Settings.Region = Region; break;
            case nameof(AccessKeyId): Settings.AccessKeyId = AccessKeyId; break;
            case nameof(SecretAccessKey): Settings.SecretAccessKey = SecretAccessKey; break;
            case nameof(SessionToken): Settings.SessionToken = SessionToken; break;
            default: return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string Region { get; set; }
    [ObservableProperty] public partial string AccessKeyId { get; set; }
    [ObservableProperty] public partial string SecretAccessKey { get; set; }
    [ObservableProperty] public partial string SessionToken { get; set; }
}
'''.lstrip()

AMAZON_VIEW = '''<UserControl
    x:Class="STranslate.Plugin.Translate.Amazon.View.SettingsView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:plugin="clr-namespace:STranslate.Plugin;assembly=STranslate.Plugin"
    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
    xmlns:vm="clr-namespace:STranslate.Plugin.Translate.Amazon.ViewModel"
    d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <ikw:SimpleStackPanel Spacing="12">
        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Amazon_Region}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.GlobeLocation_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox
                MinWidth="300"
                Text="{Binding Region, UpdateSourceTrigger=PropertyChanged}"
                placeholder:TextBoxHelper.PlaceholderText="us-west-2" xmlns:placeholder="http://schemas.inkore.net/lib/ui/wpf" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Amazon_AccessKeyId}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Key_24_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <TextBox
                MinWidth="300"
                Text="{Binding AccessKeyId, UpdateSourceTrigger=PropertyChanged}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Amazon_SecretAccessKey}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.PersonKey_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <PasswordBox
                MinWidth="300"
                plugin:PasswordBoxAssistant.Attach="True"
                plugin:PasswordBoxAssistant.Password="{Binding SecretAccessKey}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Translate_Amazon_SessionToken}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.LockClosedKey_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <PasswordBox
                MinWidth="300"
                plugin:PasswordBoxAssistant.Attach="True"
                plugin:PasswordBoxAssistant.Password="{Binding SessionToken}" />
        </ui:SettingsCard>

        <ui:SettingsCard Description="AWS Console → IAM / Translate" Header="{DynamicResource STranslate_Plugin_Translate_Amazon_Official}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.WebAsset_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <ui:HyperlinkButton Content="https://console.aws.amazon.com/translate" NavigateUri="https://console.aws.amazon.com/translate" />
        </ui:SettingsCard>

    </ikw:SimpleStackPanel>
</UserControl>
'''


def lang_xaml(ns: str, keys: dict[str, str]) -> str:
    entries = "\n".join(f'    <sys:String x:Key="{ns}_{k}">{v}</sys:String>' for k, v in keys.items())
    return f'''<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

{entries}

</ResourceDictionary>
'''


def lang_json(name: str, desc: str) -> str:
    import json
    return json.dumps({"Name": name, "Description": desc}, ensure_ascii=False, indent=2)


def build(name: str, pid: str, display: str, main: str, settings: str, vm: str, view: str,
          zh_keys: dict, en_keys: dict, zh_name: str):
    root = ROOT / name
    (root / "View").mkdir(parents=True, exist_ok=True)
    (root / "ViewModel").mkdir(parents=True, exist_ok=True)
    (root / "Languages").mkdir(parents=True, exist_ok=True)

    (root / f"{name}.csproj").write_text(csproj(name), encoding="utf-8")
    (root / "plugin.json").write_text(plugin_json(pid, name, display), encoding="utf-8")
    (root / "Main.cs").write_text(main, encoding="utf-8")
    (root / "Settings.cs").write_text(settings, encoding="utf-8")
    (root / "ViewModel" / "SettingsViewModel.cs").write_text(vm, encoding="utf-8")
    (root / "View" / "SettingsView.xaml").write_text(view, encoding="utf-8")
    ns = name
    (root / "View" / "SettingsView.xaml.cs").write_text(
        VIEW_CS.format(ns=ns), encoding="utf-8")
    shutil.copy(BAIDU / "icon.png", root / "icon.png")

    for locale in LOCALES:
        keys = zh_keys if locale.startswith("zh") else en_keys
        (root / "Languages" / f"{locale}.xaml").write_text(
            lang_xaml(ns.replace("STranslate.Plugin.", "STranslate_Plugin_").replace(".", "_"), keys), encoding="utf-8")
        (root / "Languages" / f"{locale}.json").write_text(
            lang_json(display if locale == "en" else zh_name if locale.startswith("zh") else display,
                      f"{display} plugin for jietuyao"), encoding="utf-8")
    print(f"built {name}")


AZURE_NS = "STranslate_Plugin_Translate_Azure"
build(
    "STranslate.Plugin.Translate.Azure",
    "7c2f9a41d8b64c0e9a1f5d3b2e8a4701",
    "Azure",
    AZURE_MAIN, AZURE_SETTINGS, AZURE_VM, AZURE_VIEW,
    {"ApiKey": "API 密钥", "Region": "区域(Region)", "Endpoint": "自定义端点(可选)", "Official": "官方网站"},
    {"ApiKey": "API Key", "Region": "Region", "Endpoint": "Custom Endpoint (optional)", "Official": "Official Website"},
    "Azure 翻译")

AMAZON_NS = "STranslate_Plugin_Translate_Amazon"
build(
    "STranslate.Plugin.Translate.Amazon",
    "3e8a5b7c1d9f4062ac7b5e0f8d2c6149",
    "Amazon",
    AMAZON_MAIN, AMAZON_SETTINGS, AMAZON_VM, AMAZON_VIEW,
    {"Region": "区域(Region)", "AccessKeyId": "Access Key ID", "SecretAccessKey": "Secret Access Key",
     "SessionToken": "会话令牌(可选)", "Official": "官方网站"},
    {"Region": "Region", "AccessKeyId": "Access Key ID", "SecretAccessKey": "Secret Access Key",
     "SessionToken": "Session Token (optional)", "Official": "Official Website"},
    "亚马逊翻译")
