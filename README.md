<div align="center">

<a href="https://jensen-yao.github.io/jietuyao/">
<img src="./images/logo.png" width="118" alt="jietuyao" style="border-radius:24px;border:1px solid #E5E1D8;background:#F0EEE6;padding:10px" /></a>

# <span style="font-family:Georgia,serif;font-weight:500">jietuyao</span> <sub style="font-size:15px;color:#D97757">截图耀</sub>

**截图 · 贴图 · GIF · 长截图 · 剪贴板 · 翻译 / OCR / AI** —— 一体化桌面工具

`C# WPF` · `.NET 10` · `Windows`

<a href="https://jensen-yao.github.io/jietuyao/"><img src="https://img.shields.io/badge/官方网站-jensen--yao.github.io%2Fjietuyao-D97757?style=flat-square" alt="Website" /></a>&nbsp;
<a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10-191919?style=flat-square" alt=".NET 10" /></a>&nbsp;
<a href="#-工程质量"><img src="https://img.shields.io/badge/tests-576%20passing-D97757?style=flat-square" alt="tests" /></a>&nbsp;
<a href="#-插件生态"><img src="https://img.shields.io/badge/plugins-24-191919?style=flat-square" alt="plugins" /></a>&nbsp;
<a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-D97757?style=flat-square" alt="MIT" /></a>

<br /><br />

<table style="border:none">
<tr style="border:none">
<td align="center" style="border:none;padding:6px 22px"><div style="font-family:Georgia,serif;font-size:34px;color:#D97757">576</div><div style="font-size:12px;color:#6E6A63">自动化测试</div></td>
<td align="center" style="border:none;padding:6px 22px"><div style="font-family:Georgia,serif;font-size:34px;color:#D97757">24</div><div style="font-size:12px;color:#6E6A63">内置插件</div></td>
<td align="center" style="border:none;padding:6px 22px"><div style="font-family:Georgia,serif;font-size:34px;color:#D97757">10</div><div style="font-size:12px;color:#6E6A63">标注工具</div></td>
<td align="center" style="border:none;padding:6px 22px"><div style="font-family:Georgia,serif;font-size:34px;color:#D97757">6</div><div style="font-size:12px;color:#6E6A63">核心模块</div></td>
</tr>
</table>

</div>

---

> ### 一次按键，进入完整工作流
> **`Alt+X` 框住屏幕 → 标注 → `Enter`。**
> 或者 `T` 贴在桌面上 · `L` 拼成滚动长图 · `G` 录成带鼠标特效的 GIF · `O` 离线识别 · `F` 送进大模型。

## 📸 六件套

| | 模块 | 能力 |
|:---:|---|---|
| 📸 | **截图编辑器** | 多屏冻结 · 智能窗口吸附 · 放大镜取色 · **10 种标注工具** · 图元移动/缩放/旋转 · 圆角截图 · 描边/阴影 · 锁定比例 · 刷新背景 |
| 📌 | **贴图** | 置顶钉图 · 滚轮缩放 · 透明度 · 缩略图 · 再次编辑 · **OCR 文字层**（点选/拖框复制） |
| 🎬 | **GIF 录屏** | 5–24fps · 暂停续录 · **录制中手绘标注**（时间线分段生效） · **鼠标点击烟花/滚轮箭头特效** · 回放裁剪 |
| 📜 | **长截图** | 滚轮自动连拍 · 行哈希最优重叠拼接 · 方向切换 · 顶部忽略 |
| 📋 | **剪贴板历史** | SQLite 持久化 · 搜索/分组 · **13 种特殊粘贴**（SQL IN / JSON / 加行号…） · 自动粘贴 · CSV 导入导出 |
| 🌐 | **翻译 · OCR · AI** | 谷歌 · 微软 · DeepL · 百度 · Azure · Amazon · 智谱 GLM · **任意 OpenAI 兼容端点**（DeepSeek / Ollama / 自建） · **本地 PP-OCR 完全离线** · Edge TTS |

## ⌨️ 快捷键

| 全局 | | 编辑器内 | |
|---|---|---|---|
| <kbd>Alt</kbd>+<kbd>X</kbd> | 截图 | <kbd>1</kbd>–<kbd>0</kbd> | 切换 10 种工具 |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>V</kbd> | 剪贴板历史 | <kbd>Enter</kbd> / <kbd>C</kbd> | 复制 |
| <kbd>Alt</kbd>+<kbd>D</kbd> | 划词翻译 | <kbd>T</kbd> · <kbd>S</kbd> | 贴图 · 保存 |
| <kbd>Alt</kbd>+<kbd>S</kbd> | 截图翻译 | <kbd>L</kbd> · <kbd>G</kbd> | 长截图 · GIF |
| <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>X</kbd> | 图片翻译 | <kbd>O</kbd> · <kbd>F</kbd> | OCR · 翻译 |
| <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> | OCR | <kbd>Ctrl</kbd>+<kbd>Z</kbd>/<kbd>Y</kbd> | 撤销 · 重做 |

全部可在 **设置 → 快捷键** 修改 · 冲突检测 · 全屏应用自动忽略。

## 🚀 快速开始

```bash
git clone https://github.com/Jensen-Yao/jietuyao
dotnet build src/jietuyao.slnx -c Release     # 需 .NET 10 SDK
src/.artifacts/Release/jietuyao.exe           # 按 Alt+X 开始
```

<details>
<summary><b>可选配置</b> &nbsp;·&nbsp; 离线 OCR / 连接 AI / 插件市场</summary>

- **本地离线 OCR**：PaddleOCR 的 `PP-OCRv6_det_small.onnx` + `PP-OCRv6_rec_small.onnx` 放到 exe 同级 `models/`，设置 → OCR 启用 *PP-OCR (Local)*
- **连接 AI**：设置 → 翻译 → 启用 *OpenAI* → 填任意兼容端点（GLM `https://open.bigmodel.cn/` · DeepSeek · Ollama `http://localhost:11434/` · 自建 vLLM）+ Key，可自定义提示词（翻译/润色/总结）
- **插件市场**：设置 → 插件，在线安装与升级

</details>

## 🧩 插件生态

**24 个内置插件**：12 翻译 · 8 OCR · TTS · 生词本。多实例并存、独立配置、市场在线扩展。

## ✅ 工程质量

- **576 项测试全绿** —— 含引擎**真实执行**（拼接/GIF/PDF/WEBP/CSV 端到端）与 **PP-OCR 真模型推理**
- 性能基准固化进测试：取色 150ns · JPEG 0.5ms/帧 · 1080p 冻结转源 42ms
- `Release` 冷启动 ~3.4s / ~195MB（前代 Python 实现同机 90s 未完成加载）

## 📖 文档

- [功能迁移总台账](docs/feature-parity.md) —— v1 → v2 逐项对照与验证证据
- [截图编辑器设计](docs/M1-screenshot-editor.md)

---

<div align="center">

**jietuyao 截图耀** · [官网](https://jensen-yao.github.io/jietuyao/) · [GitHub](https://github.com/Jensen-Yao/jietuyao) · MIT

<sub>截图不是功能，是呼吸。</sub>

</div>
