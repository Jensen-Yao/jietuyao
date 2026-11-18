# 插件 SDK 与开发范式

## 模块职责
- 定义插件开发接口、基础模型与运行时上下文能力。
- 说明插件从 `plugin.json` 到运行时服务实例的生命周期。
- 提供官方插件实现的共性范式，指导新插件快速落地。

## 关键入口
- `STranslate.Plugin/IPlugin.cs`
  - 所有插件共同入口：`Init(IPluginContext)`、`GetSettingUI()`、`Dispose()`。
- `STranslate.Plugin/IPluginContext.cs`
  - 插件可用能力：`HttpService`、`Logger`、`AudioPlayer`、`Snackbar`、`Notification`、配置存储、主题应用。
- `STranslate.Plugin/IHttpService.cs`
  - 插件侧 HTTP 能力：普通 GET/POST、表单、下载、流式 POST、代理测试。
- `STranslate.Plugin/IAudioPlayer.cs`、`AudioData.cs`
  - 插件侧音频播放能力：兼容旧 `byte[]` / URL 调用，并支持显式描述 MP3、WAV 和裸 PCM。
- `STranslate.Plugin/ITranslatePlugin.cs`
  - 翻译/词典接口与基类：`TranslatePluginBase`、`LlmTranslatePluginBase`、`DictionaryPluginBase`。
- `STranslate.Plugin/IOcrPlugin.cs`、`ITtsPlugin.cs`、`IVocabularyPlugin.cs`
  - OCR/TTS/生词本插件接口。
- `STranslate.Plugin/PluginMetaData.cs`、`Service.cs`
  - 元数据模型与运行时服务模型。
- 官方插件样例
  - `Plugins/*/Main.cs`
  - `Plugins/*/plugin.json`

## 核心流程
### 从入口到结果：插件被加载并可调用
1. 插件目录包含 `plugin.json`、执行 dll、图标资源。
2. `PluginManager` 读取 `plugin.json`，加载程序集并定位实现 `IPlugin` 的类型。
3. `ServiceManager` 基于 `PluginMetaData` 创建 `Service`：绑定 `Plugin` 实例与 `PluginContext`。
4. `Service.Initialize()` 调用 `Plugin.Init(Context)`，插件读取自身配置并准备可执行状态。
5. UI 层在设置页请求 `GetSettingUI()`，插件返回自身配置面板控件。

### 从入口到结果：插件配置读写
1. 插件 `Init` 内调用 `context.LoadSettingStorage<T>()` 读取配置。
2. 修改配置后调用 `context.SaveSettingStorage<T>()` 持久化。
3. 服务销毁时由 `Service.Dispose()` 调用 `Context.Dispose()` 与 `Plugin.Dispose()` 释放资源。

## 关键数据结构/配置
### `plugin.json` 规范
- 必填字段（与 `PluginMetaData` 对应）：
  - `PluginID`：插件唯一 ID（升级与去重依据）。
  - `Name`
  - `Description`
  - `Author`
  - `Version`
  - `Website`
  - `ExecuteFileName`
  - `IconPath`

### SDK 核心模型
- `PluginMetaData`：插件静态元信息 + 运行时路径与类型。
- `Service`：插件实例容器，含 `ServiceID`、`DisplayName`、`Options`。
- `AudioData`：待播放的音频字节与格式；裸 PCM 通过 `PcmAudioFormat` 指定采样率、声道、位深和编码。
- `TranslateRequest` / `TranslateResult`、`DictionaryResult`、`OcrRequest` / `OcrResult`、`VocabularyResult`：能力结果模型。
- OCR 模型：
  - `OcrRequest.PixelWidth` / `PixelHeight` 由宿主在截图 OCR、OCR 窗口和图片翻译中传入真实图片尺寸，旧插件可忽略。
  - `OcrResult.OcrContents` 是兼容旧插件的扁平文本块列表。
  - `OcrResult.Regions` 可返回结构化分段，层级为 `OcrRegion -> OcrParagraph -> OcrContent`。
  - `OcrContent.BoxPoints`、`OcrRegion.BoxPoints`、`OcrParagraph.BoxPoints` 均使用图片像素坐标；宿主不再接收归一化坐标单位声明。
- OCR 坐标能力：
  - `IOcrPlugin.SupportBoxPoints()` 默认返回 `false`，普通 OCR 不要求插件支持文本坐标框。
  - 图片翻译 OCR 服务必须 override `SupportBoxPoints()` 并返回 `true`，否则不会出现在图片翻译 OCR 选择列表。
  - 服务商能返回段落/区域结构时直接填充 `OcrResult.Regions`；`Auto` / `Provider` 模式会按是否存在有效 `Regions` 判断结构化分段。
  - 图片翻译专用链路、`Auto` / `Provider` / `Smart` 分段策略和结构化分段影响见 [flow-image-translation.md](flow-image-translation.md)。
- `LangEnum`：语言枚举，当前包含 `Uzbek`、`Uyghur`；新增语言时需要同步主程序语言检测、内置插件语言映射、本地化文本与双向文本布局规则。

### 图片翻译 OCR 插件要求
- 普通 OCR 插件仍只需要实现 `IOcrPlugin`；想进入图片翻译 OCR 下拉列表时，必须实现 `SupportBoxPoints() => true`。
- 如果返回结构化分段：
  - `OcrResult.Regions` 应按服务商真实区域、段落、行填充。
  - `OcrParagraph.Lines` 内的每个 `OcrContent` 应带文本和坐标。
  - `OcrParagraph.BoxPoints` 可直接返回服务商段落框；不返回时宿主会用行框求外接框。
  - `OcrRegion.BoxPoints` 可返回区域框；不返回不影响图片翻译。
- 如果只返回扁平 `OcrContents`：
  - 每个 `OcrContent` 仍必须有坐标框，否则图片翻译无法稳定覆盖和选中。
  - 宿主会使用本地 `Smart` 分段推断段落、表格和网格项。
- 坐标必须对应传入图片的像素空间；如服务商返回归一化坐标，插件需要用 `OcrRequest.PixelWidth` / `PixelHeight` 换算后再写入 `BoxPoints`。
- 插件侧不要把整张表格、整列列表或整页正文合成单个 `OcrContent`；这会让宿主无法恢复准确翻译粒度。

### HTTP 与流式接口
- 普通请求：使用 `GetAsync()`、`PostAsync()`、`PostFormAsync()`，通过 `Options` 传入请求头、查询参数、超时与内容类型。
- 下载：使用 `DownloadFileAsync()`，传入 `IProgress<DownloadProgress>` 获取下载进度。
- 流式请求：
  - `StreamPostAsync(url, content, onDataReceived, options, cancellationToken)` 适合回调式消费。
  - `StreamPostAsyncEnumerable(url, content, options, cancellationToken)` 适合 `await foreach` 消费。
  - 两者均有带 `serviceName` 的重载，可复用指定服务 HTTP 客户端配置。
- 取消：插件实现必须把 `CancellationToken` 继续传给 HTTP 接口；流式取消会以取消状态向上传播。

### 音频播放接口
- 新插件优先调用 `AudioPlayer.PlayAsync(AudioData, cancellationToken)` 并显式声明 `AudioFormat`。
- MP3 和 WAV 的采样参数由容器携带；裸 PCM 必须传入 `PcmAudioFormat`，宿主不会猜测采样率、声道或位深。
- `AudioFormat.Auto` 只识别 MP3/WAV，主要用于兼容旧 `byte[]` 和 URL 播放接口；AAC、FLAC、Ogg、Opus 当前不属于保证支持范围。
- Gemini 等返回裸 PCM 的服务可直接传入 `AudioFormat.Pcm`，例如单声道、24 kHz、16-bit little-endian PCM。

### 接口与基类选择建议
- 文本翻译：优先继承 `TranslatePluginBase`。
- 大模型翻译：优先继承 `LlmTranslatePluginBase`（内置 Prompt 选择机制）。
- 词典类：继承 `DictionaryPluginBase`。
- OCR/TTS/生词本：分别实现 `IOcrPlugin`、`ITtsPlugin`、`IVocabularyPlugin`。
- OCR 插件如果希望参与图片翻译，override `SupportBoxPoints()` 返回 `true` 并为 OCR 内容返回图片像素坐标 `BoxPoints`。

### 官方内置插件维护要点
- 通用登记要求：新增官方内置或预装插件时，必须将其 `plugin.json` 中的 `PluginID` 添加到 `STranslate/Core/Constant.cs` 的 `PrePluginIDs`；该列表用于判断插件是否安装到预装目录，普通社区插件无需登记。
- Microsoft 内置翻译：
  - `Settings.RequestMode` 支持 `Default` 与 `EdgeToken` 两种请求方案。
  - `EdgeToken` 模式会缓存授权 Token，并在过期前刷新。
- Google 内置翻译：
  - `Settings.RequestMode` 支持 `CustomApi` 与 `Direct` 两种请求方案。
  - `CustomApi` 使用可配置的 Deno 兼容中转地址；`Direct` 由客户端直接访问 Google 翻译网站接口。
- Transmart 内置翻译：
  - 腾讯 Transmart 官方服务已停止，插件保留仅用于兼容旧配置。
  - 设置页只展示停用说明，不再提供连通性验证入口。
- 金山词霸内置词典：
  - 当前解析网页 `__NEXT_DATA__`，需同时处理 Next.js HTML 与直接 JSON 两类响应。
  - 无结果或跳转默认页时应返回 `DictionaryResultType.NoResult`，避免错误词条污染历史。

## 关键文件
- `STranslate.Plugin/IPlugin.cs`
- `STranslate.Plugin/IPluginContext.cs`
- `STranslate.Plugin/IHttpService.cs`
- `STranslate.Plugin/IAudioPlayer.cs`
- `STranslate.Plugin/AudioData.cs`
- `STranslate.Plugin/ITranslatePlugin.cs`
- `STranslate.Plugin/IOcrPlugin.cs`
- `STranslate.Plugin/ITtsPlugin.cs`
- `STranslate.Plugin/IVocabularyPlugin.cs`
- `STranslate.Plugin/PluginMetaData.cs`
- `STranslate.Plugin/Service.cs`
- `Plugins/STranslate.Plugin.Translate.OpenAI/Main.cs`
- `Plugins/STranslate.Plugin.Ocr.OpenAI/Main.cs`
- `Plugins/STranslate.Plugin.Ocr.OcrSpace/Main.cs`
- `Plugins/STranslate.Plugin.Tts.MicrosoftEdge/Main.cs`
- `Plugins/STranslate.Plugin.Vocabulary.Eudict/Main.cs`

## 常见改动任务
- 新建插件：
  1. 新建项目与 `Main.cs`。
  2. 实现目标接口或基类。
  3. 提供 `plugin.json` 与图标。
  4. 在 `Init` 中加载配置并在设置 UI 中可编辑。
  5. 如果属于官方内置或预装插件，将 `plugin.json` 中的 `PluginID` 添加到 `STranslate/Core/Constant.cs` 的 `PrePluginIDs`。
- 增加插件能力参数：先扩展插件 `Settings` 模型，再在 `GetSettingUI()` 对应 VM 中读写并调用 `SaveSettingStorage`。
- 处理长任务取消：所有 `TranslateAsync` / `RecognizeAsync` / `SaveAsync` 应尊重 `CancellationToken`。
- 兼容升级：保持 `PluginID` 稳定，升级仅提升 `Version`，避免被识别为新插件。
- 接入大模型或 SSE 服务：优先使用 `StreamPostAsyncEnumerable()`，按行解析服务返回，避免在插件里重复实现 `HttpClient` 流读取。
- 新增 SDK 语言枚举：同时更新 `LangEnum`、主程序本地化、语言检测映射、官方插件语言映射与 `STranslate.Plugin/README.md` 版本说明。
