# ScreenGrab 选区物理坐标回传开发计划

> 本计划在 **ScreenGrab 仓库**（`D:\Others\gitR\ScreenGrab`）下执行。目的是从架构上消除 STranslate 图片翻译精简窗口的"概率性贴图偏移"。

## 背景与根因

### 现象
STranslate 图片翻译精简窗口截图后贴回屏幕时，**有概率**与原始截图选区位置偏移（非 100% 缩放下更明显，但 100% 下也会偶发）。

### 根因：坐标来源架构缺陷
对比 ScreenGrab 与 STranslate 当前实现，两者获取"截图选区物理坐标"的方式根本不同：

| | ScreenGrab（正确） | STranslate（当前，有缺陷） |
| --- | --- | --- |
| 截图时是否知道选区物理坐标 | **是**。`ScreenGrabView.RegionClickCanvas_MouseUp` 中用 `_selectBorder`（WPF 逻辑坐标）× DPI + `_captureTarget.PhysicalBounds.TopLeft`，当场算出选区绝对物理屏幕坐标 `correctedRegion` | **否**。`ScreenGrabber.CaptureAsync` 回调签名是 `Action<Bitmap>`，**只回传 bitmap，丢弃选区物理坐标** |
| 贴图坐标来源 | 截图瞬间记录的精确物理坐标 | 事后用 `ScreenshotSelectionResolver.Resolve` 反推：`GetCursorPos` + 4 个固定方位候选矩形像素匹配 |

### 为什么反推会"概率性偏移"（STranslate 侧 `ScreenshotSelectionResolver`）
1. **方位假设错误**：反推假设选区相对鼠标在 4 个固定方位（`cursor-bitmapSize` / `cursor` / `cursor.x-w,cursor.y` / `cursor.x,cursor.y-h`）。但截图是任意拖拽框选，鼠标在选区内任意位置，候选矩形基本都不在真实选区位置 → 像素匹配失败 → 返回 null。
2. **时间差竞态（概率性主因）**：`GetCursorPos` 在截图**完成后**才调用，用户截图后鼠标可能已移动 → 即使匹配上坐标也偏。这就是"有时偏有时不偏"的根源。
3. **回退必然偏移**：反推失败时回退到 `PlaceNearCursorScreen`，用 `cursor - bitmapSize` 贴窗，与真实选区无关。
4. **小截图 padding**：`GetRegionOfScreenAsBitmap` 对 <64px 截图做 `PadImage` 扩展，导致 `bitmap.Size` ≠ 真实选区尺寸，候选矩形尺寸错误。

### 结论
前两次 commit（`75b3a083`、`8546f528`）试图修补 STranslate 侧定位都未根治。按系统化调试原则，这是**架构性缺陷**：事后反推坐标不可能可靠。正确做法是让 ScreenGrab 在截图时就回传选区物理坐标，STranslate 删除整个反推逻辑。

## 目标
让 ScreenGrab 在截图完成回调中**同时回传选区的物理屏幕坐标**（虚拟屏幕物理像素），STranslate 升级后直接使用该精确坐标贴图，彻底删除 `ScreenshotSelectionResolver` 反推逻辑。

## 约束与兼容性
- ScreenGrab 是公共 NuGet 包（当前 `1.0.15`），改动必须**向后兼容**：不能破坏现有 `OnCaptured`/`CaptureAsync`/`CaptureDialog` 调用方。
- 多目标框架：`net8.0-windows;net481;net472`。
- `correctedRegion` 已在 `MouseUp` 算好（见下文），无需新增坐标计算逻辑，只需把它传出去。
- 取消截图（用户按 Esc 或选区过小）时不回调坐标，行为不变。

## 改动点总览
1. 新增回传类型 `ScreenCaptureResult`（bitmap + 选区物理坐标）。
2. `ScreenGrabView` 增加带坐标的回调重载，`MouseUp` 回传 `correctedRegion`。
3. `ScreenGrabber` 增加 `CaptureAsync`/`CaptureDialog`/`Capture` 的带坐标重载。
4. 升版本号、发布 NuGet。
5. （后续在 STranslate 仓库）升级包、改用新 API、删除 `ScreenshotSelectionResolver`。

---

## 步骤 1：新增回传类型

**文件**：`src/ScreenGrab/Models/ScreenCaptureResult.cs`（新建）

承载截图结果与选区物理坐标。`Region` 用 `System.Drawing.Rectangle`，与 ScreenGrab 内部 `correctedRegion` 一致（虚拟屏幕物理像素）。

```csharp
using System.Drawing;

namespace ScreenGrab.Models;

/// <summary>
/// 截图结果，包含截图位图与选区在虚拟屏幕中的物理像素坐标。
/// </summary>
public sealed class ScreenCaptureResult(Bitmap bitmap, Rectangle region)
{
    /// <summary>截图位图。</summary>
    public Bitmap Bitmap { get; } = bitmap;

    /// <summary>
    /// 截图选区在虚拟屏幕中的物理像素坐标。
    /// 多显示器场景下坐标可能为负值（选区在非主显示器左侧/上方）。
    /// </summary>
    public Rectangle Region { get; } = region;
}
```

## 步骤 2：ScreenGrabView 增加带坐标回调

**文件**：`src/ScreenGrab/ScreenGrabView.xaml.cs`

### 2.1 新增字段与构造函数重载
当前构造函数签名：`ScreenGrabView(Action<Bitmap>? action, bool isAuxiliary = false, ImageSource? preCapture = null)`，字段 `private readonly Action<Bitmap>? _onImageCaptured;`。

新增带坐标回调的字段与重载，**保留原构造函数**以向后兼容：

```csharp
private readonly Action<Bitmap>? _onImageCaptured;
private readonly Action<ScreenCaptureResult>? _onImageCapturedWithRegion;  // 新增

// 保留原构造函数
public ScreenGrabView(Action<Bitmap>? action, bool isAuxiliary = false, ImageSource? preCapture = null)
{
    InitializeComponent();
    _onImageCaptured = action;
    _isAuxiliary = isAuxiliary;
    _preCapture = preCapture;
}

// 新增：带选区坐标回调的构造函数
public ScreenGrabView(Action<ScreenCaptureResult>? action, bool isAuxiliary = false, ImageSource? preCapture = null)
{
    InitializeComponent();
    _onImageCapturedWithRegion = action;
    _isAuxiliary = isAuxiliary;
    _preCapture = preCapture;
}
```

> 注意：两个构造函数签名仅委托类型不同，C# 能正确重载分辨。

### 2.2 修改 MouseUp 回调
**位置**：`RegionClickCanvas_MouseUp` 末尾（当前第 541-544 行）：

```csharp
// 截图并回调
var bitmap = correctedRegion.GetRegionOfScreenAsBitmap();
CloseAllScreenGrabs();

// 新增：优先回传带坐标的结果，兼容旧回调
if (_onImageCapturedWithRegion is not null)
    _onImageCapturedWithRegion.Invoke(new ScreenCaptureResult(bitmap, correctedRegion));
else
    _onImageCaptured?.Invoke(bitmap);
```

`correctedRegion` 已在第 535 行算好（`regionScaled` + `_captureTarget.PhysicalBounds.TopLeft`），**无需新增任何坐标计算**，直接复用。

### 2.3 验证点
- 取消路径（选区过小 / Esc）走 `OnCancel?.Invoke()` 或 `CloseAllScreenGrabs()`，**不触发**任一回调，行为不变。
- `_captureTarget` 在 `SetCaptureTarget` 一定被设置，`correctedRegion` 依赖的 `_captureTarget.PhysicalBounds.TopLeft` 一定有值。

## 步骤 3：ScreenGrabber 增加带坐标重载

**文件**：`src/ScreenGrab/ScreenGrabber.cs`

为三个公开方法各增加一个带坐标的重载，**保留原方法**。以下以 `CaptureAsync` 为例（STranslate 实际使用的是它）：

```csharp
private static TaskCompletionSource<ScreenCaptureResult?>? _captureWithRegionTaskCompletionSource;

/// <summary>
/// 异步捕获截图，返回截图位图与选区物理坐标。
/// </summary>
public static Task<ScreenCaptureResult?> CaptureWithRegionAsync(bool isAuxiliary = false)
{
    if (IsCapturing)
        return Task.FromResult<ScreenCaptureResult?>(null);

    _captureWithRegionTaskCompletionSource = new TaskCompletionSource<ScreenCaptureResult?>();

    IsCapturing = true;

    var captureTargets = Screen.AllScreens.ToList().CreateCaptureTargets();
    var allScreenGrab = CreateScreenGrabViewsWithRegion(captureTargets, _ =>
        new ScreenGrabView(result =>
        {
            _captureWithRegionTaskCompletionSource?.TrySetResult(result);
        }, isAuxiliary)
        {
            OnGrabClose = () => { IsCapturing = false; },
            OnCancel = () => { _captureWithRegionTaskCompletionSource?.TrySetResult(null); }
        });

    ShowScreenGrabViews(captureTargets, allScreenGrab);
    return _captureWithRegionTaskCompletionSource.Task;
}

private static List<ScreenGrabView> CreateScreenGrabViewsWithRegion(
    IReadOnlyList<ScreenCaptureTarget> captureTargets,
    Func<ScreenCaptureTarget, ScreenGrabView> createView)
    => CreateScreenGrabViews(captureTargets, createView);  // 复用现有私有方法
```

对 `CaptureDialog` 增加同步重载 `CaptureWithRegionDialog`（用 `DispatcherFrame`，回传 `ScreenCaptureResult?`）；对 `Capture` 增加重载（设置 `OnCapturedWithRegion` 静态事件，或直接复用回调模式）。

> `Capture`（无返回值的事件式）当前用静态 `OnCaptured` 事件，使用频率低。若 STranslate 不需要，可只实现 `CaptureAsync`/`CaptureDialog` 的带坐标重载，`Capture` 暂不动以缩小改动面。**建议**：本次只实现 `CaptureWithRegionAsync`（STranslate 唯一用到的），`CaptureDialog`/`Capture` 视需要再加。

### 3.1 字段命名注意
新增的 `TaskCompletionSource<ScreenCaptureResult?>` 字段与现有 `_captureTaskCompletionSource`（`Bitmap?`）独立，避免泛型混淆。两个 TCS 不会同时使用（一次截图只走一条路径）。

## 步骤 4：单元测试

**文件**：`tests/ScreenGrab.Sample` 当前只是手动 Sample，无单元测试项目。

**建议**：本次改动逻辑简单（透传已算好的 `correctedRegion`），核心风险在坐标计算正确性——而坐标计算逻辑（`regionScaled` + `PhysicalBounds.TopLeft`）**本次未改动**，只是把它传出来。因此：

- **最低限度**：在 `ScreenGrab.Sample` 中加一个按钮，调用 `CaptureWithRegionAsync`，截图后弹窗显示 `result.Region`（X/Y/Width/Height），人工核对与选区一致。
- **可选**：若要自动化，新增 `tests/ScreenGrab.Tests` xUnit 项目，但坐标计算依赖真实窗口/DPI，难以脱离 UI 线程单测。**不强制**。

验证清单（人工，多显示器 + 多 DPI 场景）：
1. 主显示器 100% 缩放，框选任意区域 → `Region` 与选区一致。
2. 主显示器 150% 缩放，框选 → `Region`（物理像素）与选区一致。
3. 非主显示器（不同 DPI）框选 → `Region` 坐标可能为负，且数值正确。
4. 跨显示器拖拽选区（若支持）→ 不崩溃。
5. 选区过小（<2px）/按 Esc → 不回调，`CaptureWithRegionAsync` 返回 null。

## 步骤 5：版本与发布

**文件**：`src/ScreenGrab/ScreenGrab.csproj`

```xml
<Version>1.0.16</Version>
```

ScreenGrab 已配置 GitHub Action（`.github/workflows/dotnet.yml`）监听 `push tags: "*"`，任意 tag 推送即自动执行 `dotnet pack` + `dotnet nuget push`。**无需手动推送 NuGet，只需打 tag 推送：**

```bash
git tag 1.0.16
git push origin 1.0.16
```

> tag 命名沿用既有纯版本号格式（`1.0.15`、`1.0.14`…）。推送后在 GitHub Actions 页面确认构建成功并发布到 nuget.org。

## 步骤 6：验证 API 兼容性

发布前确认：
- 现有 `OnCaptured`、`CaptureAsync`、`CaptureDialog`、`Capture`、`ScreenGrabView(Action<Bitmap>, ...)` 签名与行为**完全不变**（旧调用方零改动）。
- 新增的 `ScreenCaptureResult`、`CaptureWithRegionAsync`、`ScreenGrabView(Action<ScreenCaptureResult>, ...)` 是纯增量。

---

## 后续（STranslate 仓库，待 ScreenGrab 1.0.16 发布后）

> 此部分供 STranslate 侧参考，不在 ScreenGrab 仓库执行。

1. `src/Directory.Packages.props` 把 `ScreenGrab` 升到 `1.0.16`。
2. `src/STranslate/Core/Screenshot.cs`：
   - `GetScreenshotCaptureAsync` 改用 `ScreenGrabber.CaptureWithRegionAsync`，直接拿 `result.Region` 作为 `physicalBounds`，删除 `ResolvePhysicalBounds`。
   - `ScreenshotCaptureResult` 构造改为 `new ScreenshotCaptureResult(result.Bitmap, result.Region)`。
3. 删除 `src/STranslate/Core/ScreenshotSelectionResolver.cs` 及其测试。
4. `ImageTranslateCompactWindow.PlaceForCapture` 现在总能拿到精确坐标，`PlaceNearCursorScreen`/`PlaceCenteredOnPrimaryScreen` 仅作为极端兜底（理论上不再触发）。
5. 多 DPI 多显示器实测贴图零偏移。

## 风险与回退
- **风险低**：ScreenGrab 侧是纯增量改动，坐标计算逻辑零修改，只透传已算好的值。
- **回退**：若新版有问题，STranslate 回退包版本到 `1.0.15` 即可，反推逻辑仍在（直到 STranslate 侧删除前）。
- **关键不变量**：`correctedRegion` 必须与实际 `GetRegionOfScreenAsBitmap(correctedRegion)` 截出的像素一一对应——这是 ScreenGrab 既有行为，本次不触碰。
