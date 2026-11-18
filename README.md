# jietuyao 截图耀 v2

> Windows 截图 · 贴图 · GIF 录屏 · 长截图 · 剪贴板历史 · 翻译 / OCR / TTS / AI 一体化工具
> C# WPF / .NET 10 原生实现，快、稳、美。v1（Python 版截图耀）的全量功能血统 + 全面进化：**功能只多不少，更快更好用。**

## 功能总览

| 模块 | 能力 |
|---|---|
| 📸 截图编辑器 | 多屏冻结、智能窗口选区、放大镜取色、10 种标注工具（画笔/荧光笔/矩形/椭圆/箭头/文字/序号/橡皮/指针/马赛克）、撤销重做、图元移动/缩放/旋转、圆角截图、描边/阴影、锁定纵横比、刷新背景重截 |
| 📌 贴图 | 置顶钉图、滚轮缩放、透明度、旋转/翻转、缩略图模式、再次编辑、OCR 文字层（点击复制/拖框多选） |
| 🎬 GIF 录屏 | 区域录制（5-24fps）、暂停继续、录制中标注（时间线分段）、鼠标点击烟花/滚轮箭头特效、回放预览与起止帧裁剪 |
| 📜 长截图 | 滚轮自动连拍（冷却/顶部忽略可调）、行哈希最优重叠拼接 |
| 📋 剪贴板历史 | SQLite 存储、搜索/类型筛选、分组管理、13 种特殊粘贴变换（SQL IN/JSON 转义/加行号…）、双击自动粘贴、CSV 导入导出 |
| 🌐 翻译/OCR/TTS | 谷歌/微软/DeepL/百度/Azure/Amazon/智谱/OpenAI 兼容任意 LLM（GLM·DeepSeek·Ollama·自建）等翻译；本地 PP-OCR 离线识别 + 多家在线 OCR；Edge TTS；生词本 |
| 🔌 插件体系 | 插件市场、多服务实例、自定义提示词（翻译/润色/总结）、WebDAV 备份、自动更新 |

## 快捷键（默认，可改）

`Alt+X` 截图 · `Ctrl+Alt+V` 剪贴板历史 · `Alt+S` 截图翻译 · `Alt+D` 划词翻译 · `Alt+Shift+X` 图片翻译 · `Alt+Shift+S` OCR

## 构建

```powershell
# 需 .NET 10 SDK / Windows
dotnet build src/jietuyao.slnx -c Release
dotnet test  src/Tests    # 576 项测试（引擎真实执行 + 性能基准）
```

- 本地 OCR：PaddleOCR 导出的 `PP-OCRv6_det_small.onnx / PP-OCRv6_rec_small.onnx` 放到 exe 同级 `models/` 即开箱即用
- 连接 AI：设置 → 翻译 → 启用 OpenAI 插件 → 填任意兼容端点

## 文档

- [功能总台账](docs/feature-parity.md) — v1→v2 逐项对照与验证证据
- [截图编辑器设计](docs/M1-screenshot-editor.md)

## 协议

[MIT](LICENSE)
