# 社区插件开发调试指南

## 文档目标
- 面向 clone 主程序源码后，在 `Plugins/ThirdPlugins` 中联调社区插件的开发者，说明从插件仓库准备到本地断点、打包导入、排障定位的推荐路径。
- 以当前运行时真实加载链路为准，避免只在源码仓库内可用、发布后失效的调试方式。
- 与 [插件 SDK 与开发范式](plugin-sdk-development.md) 分工：SDK 文档说明接口和模型，本指南说明开发调试流程。

## 前置约定
- clone `STranslate` 主程序源码，并在 `Plugins/ThirdPlugins` 目录下创建插件子目录。
- 推荐在 `Plugins/ThirdPlugins` 目录下 clone [STranslate 组织](https://github.com/orgs/STranslate/repositories)下同类型插件进行修改调试
- 插件目录重新初始化自己的 `.git`，提交和推送在插件仓库内完成。
- 社区插件即使放在主仓库 `Plugins/ThirdPlugins` 下调试，也优先引用 NuGet 包 `STranslate.Plugin`
- `Plugins/ThirdPlugins` 是本地调试工作区，主仓库通过 `.gitignore` 忽略该目录
- 不要直接实现 `IPlugin`，按能力选择（了解即可）：
  - 文本翻译：继承 `TranslatePluginBase`。
  - 大模型翻译：继承 `LlmTranslatePluginBase`。
  - 词典：继承 `DictionaryPluginBase`。
  - OCR/TTS/生词本：实现 `IOcrPlugin`、`ITtsPlugin`、`IVocabularyPlugin`。
- `PluginID` 必须长期稳定且全局唯一；升级只提升 `Version`，不要重新生成 `PluginID`。
- `Version` 应使用 `System.Version` 可解析格式，例如 `1.0.0` 或 `1.0.1`。安装升级比较依赖 `Version.TryParse()`，不建议在 `plugin.json` 中使用 `1.0.0-beta` 这类版本号。

## 推荐调试工作区
### 目录布局
以下仅以 `STranslate.Plugin.Translate.DeepLX` 为范例，开发者需要将文中加粗项替换为自己的插件参数。

实际联调目录建议保持为：

```text
<STranslate源码根目录>\
    src\
      STranslate\
      STranslate.Plugin\
      Plugins\
        ThirdPlugins\                               # 自己手动创建该目录 <与 csproj 里DEBUG OutputPath 层级有关>
          STranslate.Plugin.Translate.DeepLX\       # 独立插件 git 仓库
            .git\
            STranslate.Plugin.Translate.DeepLX\
              Main.cs
              Settings.cs
              plugin.json
              icon.png
              View\
              ViewModel\
              Languages\
              STranslate.Plugin.Translate.DeepLX.csproj
```

准备一个社区插件时，推荐从对应插件仓库 clone 到 `Plugins/ThirdPlugins`：

```powershell
cd <STranslate源码根目录>
New-Item -ItemType Directory -Force .\Plugins\ThirdPlugins
git clone https://github.com/STranslate/STranslate.Plugin.Translate.DeepLX.git .\Plugins\ThirdPlugins\STranslate.Plugin.Translate.DeepLX
```

需要替换为自己插件参数的内容：
- **`https://github.com/STranslate/STranslate.Plugin.Translate.DeepLX.git`**：改为自己的插件仓库地址。
- **`STranslate.Plugin.Translate.DeepLX`**：改为自己的插件仓库目录名。

如果插件源码来自压缩包或复制目录，则进入插件目录后重新建立插件自己的 git 仓库，并按插件仓库远端提交；不要把 `ThirdPlugins` 下的插件内容提交到主程序仓库：

```powershell
cd .\Plugins\ThirdPlugins\STranslate.Plugin.Translate.DeepLX
git init
git remote add origin https://github.com/STranslate/STranslate.Plugin.Translate.DeepLX.git
```

需要替换为自己插件参数的内容：
- **`STranslate.Plugin.Translate.DeepLX`**：改为自己的插件仓库目录名。
- **`https://github.com/STranslate/STranslate.Plugin.Translate.DeepLX.git`**：改为自己的插件仓库地址。

### `plugin.json`
`plugin.json` 必须位于插件输出目录根部，且 `ExecuteFileName` 必须指向同一目录下真实存在的插件 dll。以下是 `DeepLX` 范例：

```json
{
  "PluginID": "new-guid-str",
  "Name": "DeepLX",
  "Description": "DeepLX plugin for stranslate",
  "Author": "your-github-username",
  "Version": "1.0.0",
  "Website": "https://github.com/STranslate/STranslate.Plugin.Translate.DeepLX",
  "ExecuteFileName": "STranslate.Plugin.Translate.DeepLX.dll",
  "IconPath": "icon.png"
}
```

需要替换为自己插件参数的内容：
- **`PluginID`**：**改为自己的稳定唯一插件 ID。** ★★★不要从现有的插件项目里复制，每个插件的ID都需要唯一★★★
- **`Name`** / **`Description`** / **`Website`**：改为自己的插件展示信息。
- **`Author`**：改为自己的 GitHub 用户名。
- **`Version`**：改为自己的插件版本，并保持 `System.Version` 可解析格式。
- **`ExecuteFileName`**：改为自己的插件 dll 文件名。

### `csproj` 调试改法
`ThirdPlugins` 下的社区插件会受到主仓库上层 `Directory.Packages.props` 影响，所以插件项目应显式关闭集中包版本管理，并保留自己的 `PackageReference` 版本。以下是 `DeepLX` 项目文件中的实际配置参考，不要遗漏其中的 Debug / Release 输出和内容复制项；`STranslate.Plugin` 推荐使用 [NuGet 上的最新版本](https://www.nuget.org/packages/STranslate.Plugin)，截至 2026-08-14 核对为 `1.0.14`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <LangVersion>preview</LangVersion>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <EnableWindowsTargeting>true</EnableWindowsTargeting>
        <RepositoryUrl>https://github.com/STranslate/STranslate.Plugin.Translate.DeepLX</RepositoryUrl>
        <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>

        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
        <DebugSymbols>true</DebugSymbols>
        <DebugType>portable</DebugType>
        <Optimize>false</Optimize>
        <!--// 方便调试 //-->
        <OutputPath>..\..\..\..\.artifacts\Debug\Plugins\STranslate.Plugin.Translate.DeepLX\</OutputPath>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
        <DebugType>none</DebugType>
        <Optimize>true</Optimize>
        <OutputPath>..\.artifacts\</OutputPath>
        <DefineConstants>TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
        <!--// 编译后打包为插件 //-->
        <EnableAutoPackage>true</EnableAutoPackage>
    </PropertyGroup>

    <ItemGroup>
        <Content Include="Languages\*.*">
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
        <PackageReference Include="STranslate.Plugin" Version="1.0.14" />
    </ItemGroup>

</Project>
```

需要替换为自己插件参数的内容：
- **`RepositoryUrl`**：改为自己的插件仓库地址。
- **Debug `OutputPath` 末尾的 `STranslate.Plugin.Translate.DeepLX`**：改为自己的插件输出目录名，通常与插件项目/程序集名一致。
- **Release `OutputPath`**：可沿用插件仓库内 `.artifacts`，如需调整则改为自己的发布输出目录。
- **`PackageReference` 列表和版本号**：改为自己插件真实依赖；`STranslate.Plugin` 推荐先使用 NuGet 最新版本。

这些配置的作用：
- `ManagePackageVersionsCentrally=false`：让插件仓库使用自己的 NuGet 版本声明，避免被主程序仓库的集中包版本覆盖。
- `PackageReference Include="STranslate.Plugin"`：推荐使用 NuGet 最新版本；如果插件必须兼容旧宿主，再单独锁定较旧版本。
- `AppendTargetFrameworkToOutputPath=false` / `AppendRuntimeIdentifierToOutputPath=false`：让 `plugin.json`、插件 dll、图标、语言文件直接落在插件目录根部，符合宿主扫描预期。
- Debug `OutputPath`：把插件输出直接放进源码版宿主的 `Plugins` 目录，启动 Debug 宿主即可加载。
- Release `OutputPath` + `EnableAutoPackage=true`：把发布产物留在插件仓库自己的 `.artifacts` 中，并自动生成 `.spkg`。

## 本地断点调试
### 主仓库源码联调
这是实际最常用的调试路径。

1. 使用 Visual Studio 打开主程序源码的 `STranslate.slnx`。
2. 在解决方案资源管理器中右键解决方案，选择“添加” -> “现有项目”。
3. 选择 `Plugins\ThirdPlugins\STranslate.Plugin.Translate.DeepLX\STranslate.Plugin.Translate.DeepLX\STranslate.Plugin.Translate.DeepLX.csproj`。
4. 将 `STranslate` 设为启动项目，配置选择 `Debug`，直接按 `F5` 调试。
5. 在插件项目中给 `Init()`、`GetSettingUI()`、`TranslateAsync()` / `RecognizeAsync()` / `PlayAudioAsync()` 等入口打断点。

需要替换为自己插件参数的内容：
- **`Plugins\ThirdPlugins\STranslate.Plugin.Translate.DeepLX\STranslate.Plugin.Translate.DeepLX\STranslate.Plugin.Translate.DeepLX.csproj`**：改为自己的插件项目路径。

宿主 Debug 程序目录是 `.artifacts\Debug`，插件 Debug 输出目录正好是 `.artifacts\Debug\Plugins\<插件名>`，因此 `PluginManager.LoadPlugins()` 能按正常运行时路径加载该插件。

## 打包与安装验证
### 生成 `.spkg`
`STranslate.Plugin` SDK 包提供 `PackageAsSpkg` 目标。`DeepLX` 的 Release 配置已设置 `EnableAutoPackage=true`，因此在 Visual Studio 中将插件项目切到 `Release` 后生成项目，即可自动生成插件包。

在 `DeepLX` 这类目录结构中，生成路径通常为：

```text
Plugins\ThirdPlugins\STranslate.Plugin.Translate.DeepLX\.artifacts\plugins\STranslate.Plugin.Translate.DeepLX.spkg
```

需要替换为自己插件参数的内容：
- **`STranslate.Plugin.Translate.DeepLX`**：改为自己的插件仓库目录名和插件包名。

### 安装验证清单
- `.spkg` 本质是 zip，包根目录必须直接包含 `plugin.json`，不要再套一层项目文件夹。
- 包根目录必须包含 `ExecuteFileName` 指定的 dll、依赖 dll、`icon.png` 和必要的 `Languages/*`。
- 在 STranslate 的插件页使用“本地导入”或拖拽 `.spkg` 安装；这会走与市场下载相同的安装逻辑。
- 新安装成功后可立即创建服务并测试；升级已有插件时会写入升级标记，按提示重启后才替换旧目录。
- 卸载和升级会通过 `NeedDelete.txt` / `_NeedUpgrade` 标记在下次启动清理目录，不要手动删除正在运行中的插件 dll。

## 运行时数据与日志位置
### 日志
插件应使用 `Context.Logger` 记录诊断信息。宿主会把日志写到：

```text
便携模式：<STranslate.exe 所在目录>\PortableConfig\Logs\<当前版本>\*.log
漫游模式：%APPDATA%\STranslate\Logs\<当前版本>\*.log
```

日志模板包含 `SourceContext`，社区插件通常会显示为插件程序集名，便于从主程序日志中筛出插件记录。

### 配置与缓存
插件配置由 `context.LoadSettingStorage<T>()` / `context.SaveSettingStorage<T>()` 管理：

```text
便携模式：<STranslate.exe 所在目录>\PortableConfig\Settings\Plugins\<AssemblyName>_<PluginID>\<serviceId>.json
漫游模式：%APPDATA%\STranslate\Settings\Plugins\<AssemblyName>_<PluginID>\<serviceId>.json
```

插件缓存目录由 `context.MetaData.PluginCacheDirectoryPath` 提供：

```text
便携模式：<STranslate.exe 所在目录>\PortableConfig\Cache\Plugins\<AssemblyName>_<PluginID>
漫游模式：%APPDATA%\STranslate\Cache\Plugins\<AssemblyName>_<PluginID>
```

配置模型变更时优先在插件 `Init()` 内兼容旧配置并保存，不要要求宿主为社区插件硬编码迁移逻辑。

## 调试关键入口
- `Init(IPluginContext context)`
  - 读取配置，保存 `context` 引用，初始化只和插件生命周期相关的资源。
  - 不要在这里发起耗时网络请求；首次打开设置页或首次执行能力时再做验证更容易定位问题。
- `GetSettingUI()`
  - 返回配置面板控件。
  - 设置变更后调用 `context.SaveSettingStorage<T>()`。
  - 新窗口或对话框需要跟随主题时调用 `context.ApplyTheme(window)`。
- `TranslateAsync()` / `RecognizeAsync()` / `PlayAudioAsync()` / `SaveAsync()`
  - 将 `CancellationToken` 继续传给 `Context.HttpService`、音频播放或外部异步操作。
  - 播放 MP3、WAV 或裸 PCM 时，优先构造 `AudioData` 并显式声明格式；裸 PCM 必须同时提供 `PcmAudioFormat`，不要依赖宿主猜测采样参数。
  - 对可预期的服务端失败返回 `Fail(...)` 或写入结果错误信息；对意外异常记录日志后再决定是否抛出。
  - 不要把本次请求文本、响应正文或临时结果保存在插件字段中；运行时服务会复用同一个插件实例，字段更适合保存只读配置、客户端状态或可控缓存。

## 常见故障排查
| 现象 | 优先检查 |
| --- | --- |
| 插件列表中看不到插件 | `plugin.json` 是否在目录根部；`ExecuteFileName` 是否存在；插件是否实现了目标能力接口；日志中是否有 `Plugin loading error`。 |
| 安装 `.spkg` 提示结构无效 | 包根目录是否直接包含 `plugin.json`；是否把整个项目文件夹压进了包里；`PluginID` 是否为空。 |
| 升级被提示版本过旧 | `Version` 是否可被 `Version.TryParse()` 解析；新版本是否大于已安装版本。 |
| 断点不命中 | 运行中的 `STranslate.exe` 是否加载了你刚构建的目录；dll 旁边是否有 pdb；是否存在同 `PluginID` 的更高版本插件被优先加载。 |
| 设置保存后重启丢失 | 是否在设置 VM 中调用了 `SaveSettingStorage<T>()`；是否调试了另一个数据目录（便携/漫游目录不一致）。 |
| ESC 或关闭窗口无法取消请求 | 是否把 `CancellationToken` 传给所有 HTTP、流式读取、下载和音频操作。 |
| 图片翻译无法选中 OCR 区域 | OCR 插件是否 override `SupportBoxPoints()` 并返回 `true`；OCR 结果是否填充图片像素坐标 `OcrContent.BoxPoints` 或结构化 `Regions` 中的行框；仅返回纯文本无法支持框选和标注。 |
| 日志里有 `ReflectionTypeLoadException` | 依赖 dll 是否随插件输出；依赖版本是否与宿主可加载版本兼容；Release 打包是否遗漏内容文件。 |

## 发布前检查
- 使用 Release 打包并通过本地导入安装一次，不只验证 Debug 目录直连。
- 清理本机旧插件目录后再测一次首次安装，避免被本地旧配置掩盖问题。
- 至少覆盖一种成功路径和一种失败路径，确认错误能在 UI 或日志中被理解。
- 测试取消：翻译、OCR、TTS、下载、流式响应都应能响应宿主传入的取消令牌。
- 核对 `plugin.json` 的 `PluginID`、`Version`、`Website`、`ExecuteFileName`、`IconPath`，这些字段会影响安装、升级、展示和排障。
- 若插件依赖远程服务，避免在 README 或示例配置中提交真实密钥。

### 自动发布
按照本文档前述方式从 STranslate 组织仓库 clone 并修改的插件，其项目已内置 GitHub Actions 发布工作流，无需手动本地打包。代码修改完成后，直接创建并推送以 `v` 开头的 Tag 即可触发自动打包并发布：

```powershell
git tag v1.0.0
git push origin v1.0.0
```

- Tag 命名**必须**以 `v` 开头，例如 `v1.0.0`、`v1.2.3`。
- 推送后 Actions 会自动执行 Release 构建、生成 `.spkg` 并发布到 GitHub Releases。

## 插件收录

社区插件发布到插件市场前，请先阅读 [STranslate-doc](https://github.com/STranslate/STranslate-doc) 仓库中的发布规范与收录说明，确保插件信息、版本策略及元数据符合官方市场要求。
