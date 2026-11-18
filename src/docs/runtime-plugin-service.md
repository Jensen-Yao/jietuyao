# 插件与服务运行时

## 模块职责
- 负责插件目录扫描、元数据加载、安装/升级/卸载。
- 将插件元数据实例化为可运行的 `Service`，并与 `ServiceSettings` 持久化绑定。
- 提供插件运行上下文 `PluginContext`，承载 HTTP、日志、通知、配置存储等能力。

## 关键入口
- `STranslate/Core/PluginManager.cs`
  - `LoadPlugins()`：扫描目录、去重、加载程序集与插件类型。
  - `InstallPlugin()` / `UpgradePlugin()` / `UninstallPlugin()`：包安装与目录标记策略。
- `STranslate/Core/ServiceManager.cs`
  - `LoadServices()`：根据 `ServiceSettings` + 插件配置文件恢复服务实例。
  - `AddService()` / `RemoveService()`：服务实例新增删除。
- `STranslate/Services/BaseService.cs`
  - 统一管理服务集合、服务属性变更落盘、单启用约束、顺序变更。
- `STranslate/ViewModels/Pages/TranslateViewModel.cs`
  - 翻译服务配置页命令，如启用项前移排序。
- `STranslate/Core/PluginContext.cs`
  - 插件上下文能力注入与插件配置读写。
- `STranslate.Plugin/Service.cs`
  - `Initialize()` 调用 `Plugin.Init(Context)`，`Dispose()` 释放上下文与插件。

## 核心流程
### 从入口到结果：启动时插件发现到可实例化元数据
1. `PluginManager.LoadPlugins()` 遍历 `DataLocation.PluginDirectories`（预装 + 用户目录）。
2. 扫描中先处理标记目录：
   - 命中 `NeedDelete`：直接清理目录。
   - 命中 `NeedUpgrade`：去除后缀并覆盖原目录。
3. 读取每个目录的 `plugin.json`，反序列化为 `PluginMetaData`，校验执行文件存在。
4. 按 `PluginID` 分组，保留版本最新项，重复项仅记录日志不加载。
5. `PluginAssemblyLoader` 加载程序集，反射定位实现 `IPlugin` 的类型，写回 `PluginMetaData.PluginType`。
6. 计算并填充插件专属目录：
   - `PluginSettingsDirectoryPath`
   - `PluginCacheDirectoryPath`

### 从入口到结果：服务实例化与持久化恢复
1. `ServiceManager.LoadServices()` 先清理被标记删除的插件配置/缓存目录。
2. 对每个插件目录读取 `*.json`（文件名即 `SvcID`），与 `ServiceSettings` 的四类 `ServiceData` 做 join。
3. 对 join 命中的记录创建 `Service`：
   - 克隆 `PluginMetaData`
   - 绑定 `ServiceID`、`DisplayName`、`IsEnabled`
   - 翻译/词典服务恢复 `TranslationOptions`（执行模式、自动回译）
   - 创建 `PluginContext(metaDataClone, serviceID)`
   - 通过 `CreatePluginService()` 反射实例化插件
4. `service.Initialize()` 调用插件 `Init(context)`，进入可用状态。
5. 缺失服务（配置有但文件不存在）会从 `ServiceSettings` 自动移除并落盘；失效的 `ReplaceSvcID` / `ImageTranslateSvcID` / `ImageTranslateOcrSvcID` 也会自动重置。

### 从入口到结果：插件服务配置写入
1. 插件在 `Init` 中调用 `context.LoadSettingStorage<T>()`。
2. `PluginStorage<T>` 使用 `PluginSettingsDirectoryPath/{serviceId}.json` 作为存储位置。
3. 插件调用 `context.SaveSettingStorage<T>()` 写盘；服务销毁时 `PluginContext.Dispose()` 会删除当前服务配置并尝试清理空目录。

### 从入口到结果：服务顺序与启用项整理
1. 服务启用状态、显示名、执行模式与顺序由 `ServiceSettings` 持久化。
2. 拖拽或命令调整服务集合顺序时，`BaseService` 监听集合变化并写回配置。
3. 翻译服务配置页提供 `ReorderEnabledServicesCommand`，快捷键 `Ctrl + Shift + R`：
   - 已启用服务移动到前方。
   - 未启用服务移动到后方。
   - 启用组和未启用组内部相对顺序保持不变。

### 服务重复策略
- 翻译、OCR、TTS 和生词本服务执行“重复”时都会生成独立的 `ServiceID`，名称为原名称加 `_New`，并插入原服务后一位。
- 重复操作会复制插件配置 JSON、执行模式、自动回译和自定义图标；插件初始化必须在配置复制后执行。
- 新旧服务使用各自的配置文件和图标文件，完成重复后可以独立修改。替换翻译、图片翻译、图片翻译 OCR 等全局角色仍保持指向原服务。
- OCR、TTS 和生词本只允许启用一个服务，因此其副本默认禁用；文本翻译副本保持原服务的启用状态。

### 安装、升级、卸载策略
- 安装：`.spkg` 解压到临时目录，校验 `plugin.json`，按插件 ID 决定目标目录（预装目录或用户目录），加载成功后加入运行集合。
- 升级：旧目录写 `NeedDelete` 标记，新目录以 `NeedUpgrade` 后缀落位，等待重启生效。
- 卸载：插件目录、插件设置目录、插件缓存目录均仅写删除标记，重启时清理。

## 关键数据结构/配置
- `PluginMetaData`：插件标识、执行文件、图标、类型、目录、设置/缓存路径。
- `Service`：运行时服务实例（`ServiceID`、`Plugin`、`Context`、`Options`）。
- `ServiceData`：可持久化的服务配置项（启用状态、名称、翻译选项）。
- `PluginInstallResult` / `PluginLoadResult`：安装/加载结果模型。
- 特殊服务 ID：`ReplaceSvcID`、`ImageTranslateSvcID`、`ImageTranslateOcrSvcID`；对应服务缺失时启动加载会自动重置。

## 关键文件
- `STranslate/Core/PluginManager.cs`
- `STranslate/Core/ServiceManager.cs`
- `STranslate/Services/BaseService.cs`
- `STranslate/ViewModels/Pages/TranslateViewModel.cs`
- `STranslate/Core/PluginContext.cs`
- `STranslate/Services/PluginInstance.cs`
- `STranslate.Plugin/PluginMetaData.cs`
- `STranslate.Plugin/Service.cs`

## 常见改动任务
- 新增插件类型（例如新能力接口）：同步修改 `BaseService.OnPluginMetaDatasCollectionChanged` 与对应服务模块加载逻辑。
- 调整安装策略：优先改 `PluginManager.InstallPlugin` 与 `MoveToPluginPath`，不要破坏 `NeedUpgrade` / `NeedDelete` 标记语义。
- 服务配置丢失排查：先检查 `DataLocation.PluginSettingsDirectory` 的 `SvcID.json` 与 `ServiceSettings` 四类列表是否一致。
- 插件配置迁移：优先在插件 `Init` 中做版本迁移，再 `SaveSettingStorage`，避免在主程序层硬编码插件私有结构。
- 调整服务排序工具：优先复用 `ObservableCollection.Move()`，让现有集合监听和持久化逻辑生效。
