# 插件市场与插件管理

## 模块职责
- 管理已安装插件（安装、拖拽导入、卸载、目录打开、官网跳转）。
- 提供插件市场视图（列表拉取、筛选、状态同步、下载/取消/升级）。
- 处理 CDN 源与下载代理策略，并落地升级后重启机制。

## 关键入口
- `STranslate/ViewModels/Pages/PluginViewModel.cs`
  - `ToggleMarketViewAsync()`：已安装/市场视图切换。
  - `LoadPluginsAsync()`：市场插件加载。
  - `DownloadPluginAsync()` / `CancelDownload()`：下载、取消、安装、升级。
  - `AddPluginAsync()` / `InstallPluginsAsync()` / `DeletePluginAsync()`：本地安装与卸载。
- `STranslate/Views/Pages/PluginPage.xaml`
  - 统一页面内包含已安装视图与市场视图模板。
  - 拖拽安装、状态图标、下载进度条绑定。
- `STranslate/Services/PluginInstance.cs`
  - `PluginService`：对 `PluginManager` 安装/升级/卸载能力的 ViewModel 侧封装。

## 核心流程
### 从入口到结果：首次进入市场并展示可操作列表
1. 点击“市场”触发 `ToggleMarketViewAsync()`。
2. 首次进入市场视图时延迟执行 `LoadPluginsAsync()`，避免设置页初始化阻塞。
3. `LoadPluginsAsync()` 执行：
   - 调用 `GetPluginsJsonUrl()` 获取插件 ID 列表。
   - 限流并发拉取每个插件 `plugin.json`（先 `main`，404 回退 `master`）。
   - 可选拉取 `Languages/zh-cn.json` 覆盖名称与描述。
   - 构建 `PluginMarketInfo` 并分批写入 UI 集合。
4. `UpdatePluginStatus()` 以 `PluginID` 对比本地插件，计算 `IsInstalled`、`CanUpgrade`、`InstalledVersion`。

### 从入口到结果：市场下载到安装/升级
1. `DownloadPluginAsync(plugin)` 创建取消令牌并显示进度。
2. 按 `Settings.PluginDownloadProxy` 选择下载 URL（GitHub 直连 / ghproxy 镜像 / 自定义）。
3. 通过 `IHttpService.DownloadFileAsync()` 下载 zip，重命名为 `.spkg`。
4. 调用 `_pluginService.InstallPlugin(spkgPath)`：
   - 新安装成功：立即生效并刷新状态。
   - 返回 `RequiredUpgrade`：弹窗确认升级，调用 `_pluginService.UpgradePlugin()`。
5. 升级成功后进入重启策略：
   - 立即重启：`UACHelper.Run(_settings.StartMode)` + `App.Current.Shutdown()`。
   - 稍后重启：标记 `plugin.IsPendingRestart = true`。

### 从入口到结果：本地导入与卸载
- 本地导入：文件选择或拖拽 `.spkg` 后走与市场同一安装流程。
- 卸载：`DeletePluginAsync()` 先确认，再调用 `_pluginService.UninstallPlugin()`，并提示重启使目录标记删除生效。

## 关键数据结构/配置
- `PluginMarketInfo`
  - 市场元信息：`PluginId/Name/Type/Version/DownloadUrl/IconUrl`。
  - 运行状态：`IsInstalled`、`CanUpgrade`、`IsDownloading`、`IsPendingRestart`。
  - `ActionStatus`：`Download/Installed/Upgrade/Downloading/PendingRestart`。
- 关键配置（`Settings`）
  - `PluginMarketCdnSource`
  - `CustomPluginMarketCdnUrl`
  - `PluginDownloadProxy`
  - `CustomDownloadProxyUrl`

## 关键文件
- `STranslate/ViewModels/Pages/PluginViewModel.cs`
- `STranslate/Views/Pages/PluginPage.xaml`
- `STranslate/Services/PluginInstance.cs`
- `STranslate/Core/PluginManager.cs`
- `STranslate/Core/HttpService.cs`

## 常见改动任务
- 扩展市场元数据字段：先改 `PluginMarketInfo`，再改加载解析与 XAML 绑定模板。
- 增加下载源/代理类型：修改 `Settings` 枚举、`PluginViewModel` URL 构建分支、设置页配置项。
- 优化列表加载卡顿：优先调整 `PluginLoadMaxConcurrency` 与 `PluginUiBatchSize`。
- 升级策略调整：统一改 `HandleInstallResultAsync()` 与 `PromptRestartAsync()`，避免出现“已升级但状态未刷新”。
