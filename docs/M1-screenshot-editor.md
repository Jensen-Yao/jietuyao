# jietuyao v2 — 里程碑 M1：截图编辑器 + 贴图

> jietuyao v2 的首个里程碑：将原 Python 版截图耀的核心看家功能移植为 C# WPF 原生实现。

## 新增能力

### 截图编辑器（替代 ScreenGrab 简易选框）

| 能力 | 说明 | 入口 |
|---|---|---|
| 多屏冻结 | 打开瞬间 BitBlt 冻结整个虚拟桌面，逐显示器全屏窗口呈现（PerMonitorV2 混合 DPI 正确） | `ScreenshotCapturer` |
| 智能选区 | 悬停高亮吸附窗口（DWM 扩展边界，过滤 UWP 挂起/最小化），单击即选中 | `ScreenshotWindowFinder` |
| 精确框选 | 拖拽 + 八向手柄调整 + 选区内拖动 + 方向键微调（Shift=10px）+ 双边框 + 尺寸标签 | `AnnotationCanvas` |
| 放大镜取色 | 6× 像素放大 + RGB/HEX 读数，Ctrl+点击复制颜色 | `AnnotationCanvas.RenderMagnifier` |
| 9 种标注工具 | 画笔 / 荧光笔 / 矩形 / 椭圆 / 箭头 / 文字 / 序号 / 橡皮 / 鼠标指针 | `ScreenshotAnnotations` |
| 智能编辑 | 悬停高亮图元 → 拖动平移 / 手柄缩放 | 选择工具（1） |
| 撤销重做 | 全部操作入栈（增/删/移动/缩放/清空） | Ctrl+Z / Ctrl+Y |
| 输出 | 复制（PNG+DIB V5 双格式透明度安全）/ 保存 PNG、JPG、BMP / 贴图 / OCR / 翻译 | `ScreenshotOutput` |
| 贴图 | 置顶钉图：拖动、滚轮缩放（光标锚点）、Ctrl+滚轮透明度、旋转/翻转、双击还原、OCR、翻译 | `PinImageWindow` |

### 快捷键（编辑器内）

| 键 | 动作 |
|---|---|
| `Alt + X` | 全局：打开截图编辑器（新增热键，可在设置-快捷键修改） |
| `1..9,0` | 切换工具（选择/画笔/荧光笔/矩形/椭圆/箭头/文字/序号/橡皮/指针） |
| `Enter` / `C` | 复制 |
| `S` / `Ctrl+S` | 保存 |
| `T` | 贴图 |
| `O` | OCR 识别 |
| `F` | 截图翻译 |
| `Del` | 删除悬停图元 / 清空标注 |
| `方向键` | 微调选区（Shift 加速） |
| `Esc` | 退出 |
| `Ctrl+点击` | 复制放大镜下颜色 |

### 与 底座项目 的融合

- **`IScreenshot` 接缝替换**：截图翻译(Alt+S)、OCR(Alt+Shift+S)、图片翻译(Alt+Shift+X)、二维码、静默 OCR 全部自动升级为 jietuyao 编辑器体验，原有流程零改动。
- **主窗口头部新增“截图”按钮**（相机图标），支持设置中显隐与拖拽排序。
- **贴图右键可直接路由到 底座项目 的 OCR 窗口 / 截图翻译流程**。
- **工具样式持久化**：每个工具的颜色/线宽/字号/填充存入 Settings.json。
- 9 种界面语言同步新增“截图”词条。

## 代码结构

```
src/底座项目/
  Core/Screenshot/            ← 编辑器引擎（无 UI 依赖，可单测）
    ScreenshotTypes.cs        类型/选项/结果
    ScreenshotCapturer.cs     多屏冻结捕获 + 位图互转
    ScreenshotWindowFinder.cs 智能选区窗口枚举
    ScreenshotAnnotations.cs  9 种标注图元（渲染/命中/几何变换）
    ScreenshotUndoHistory.cs  撤销重做栈
    ScreenshotEditorSession.cs 会话状态（选区/标注/导出）
    AnnotationCanvas.cs       交互画布（五层 DrawingVisual + 状态机）
    ScreenshotOutput.cs       剪贴板/文件输出
    ScreenshotEditorService.cs 编排 + IScreenshot 实现
    PinImageManager.cs        贴图管理
  Controls/ScreenshotEditorToolbar.cs  浮动工具栏
  Views/ScreenshotEditorWindow.xaml(.cs)  每显示器全屏编辑窗口
  Views/PinImageWindow.xaml.cs           贴图窗口
```

## 构建

```powershell
dotnet build src/底座项目.slnx -c Debug   # 需 .NET 10 SDK
dotnet test src/Tests/底座项目.Tests      # 554 项基线测试保持通过
```

## 后续里程碑（Backlog）

- M2：剪贴板历史管理（Win+V、分组、特殊粘贴）
- M3：GIF 录屏（逐帧标注/回放/鼠标特效）
- M4：滚动长截图（哈希拼接）
- M5：品牌重塑（jietuyao 名称/图标/仓库）、本地 PP-OCR 插件、PDF 导出
