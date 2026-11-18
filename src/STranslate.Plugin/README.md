Reference this package to develop a plugin for [STranslate](https://github.com/STranslate/STranslate).

## v1.0.15 - 2026-09-04

- `LangEnum` 新增维吾尔语（`Uyghur`）

## v1.0.14 - 2026-08-14

- `IAudioPlayer` 新增 `PlayAsync(AudioData, CancellationToken)`，支持显式播放 MP3、WAV 和裸 PCM
- `AudioData` 可通过 `PcmAudioFormat` 指定裸 PCM 的采样率、声道、位深与采样编码
- 现有 `byte[]` 和 URL 播放接口保持兼容，并支持自动识别 MP3/WAV

## v1.0.13 - 2026-07-01

- `Service` 新增 `IconPath` 属性，支持为服务实例设置自定义图标路径（为空时回退 `MetaData.IconPath`）

## v1.0.12 - 2026-06-25

- OCR 能力增强：`IOcrPlugin` 新增 `SupportBoxPoints()`（默认 `false`），声明是否返回图片像素坐标的文本框，图片翻译需要该能力
- `OcrRequest` 新增 `PixelWidth` / `PixelHeight` 参数，传递图片像素尺寸以便计算坐标
- `OcrResult` 新增 `Regions` 结构化区域（`OcrRegion` → `OcrParagraph` → `OcrContent`），每级携带 `BoxPoints` 坐标框
- `OcrResult.Text` 在未提供 `OcrContents` 时回退按区域/段落拼装纯文本，并按 CJK 与标点规则智能补空格
- 新增 `TextHelper.IsCjk(char)`，判断字符是否属于 CJK（中日韩）文字区域

## v1.0.11 - 2026-05-22

- LangEnum 添加乌兹别克语 感谢 @boxi-wangji #704

## v1.0.10 - 2026-03-30

- IHttpService 添加 StreamPostAsyncEnumerable 方法 感谢 @SwiftFloatFlow #672

## v1.0.9 - 2026-03-12

- 添加同步主题能力给插件 @SwiftFloatFlow

## v1.0.8 - 2026-01-13

- 生词本插件添加 SaveWithNoteAsync 方法，支持保存带笔记的生词（翻译结果作为笔记）

## v1.0.7 - 2026-01-06

- 添加 UrlHelper 类，简化 LLM URL 配置操作

## v1.0.5 - 2025-12-29

- 字典结果添加 Tag 字段

## v1.0.4 - 2025-12-12

- PluginContext 添加 ImageQuality
