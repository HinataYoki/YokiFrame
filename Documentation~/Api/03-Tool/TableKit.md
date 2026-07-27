# TableKit 数据表

## 适用场景

TableKit 把 Luban 数据表生成流程接入 Workbench，并为运行时提供强类型表管理器。适合需要在 Unity、Godot .NET 或其它 C# 项目中读取配置表的项目。

## 使用前提

使用前准备：

- 可正常运行的 Luban 配置文件（通常为 `luban.conf`）和表数据。
- Workbench 中可用的 Luban 工具路径。
- 代码输出目录和数据输出目录。

TableKit 只负责验证、生成和加载，不提供运行时编辑表数据的功能。首次生成成功后，项目才会获得对应的 `TableKit` 门面和表管理器类型。

## 快速上手

1. 打开 Workbench 的 TableKit 页面，选择项目、Luban 配置和 Luban 工具。
2. 设置代码输出目录、数据输出目录以及数据格式。
3. 选择资源定位方式：
   - 开启“资源可寻址”时，运行时直接使用表名定位。
   - 关闭时填写运行时路径模板；模板必须包含 `{0}`，它会替换为表名。
4. 点击“验证”，确认配置、目标和路径均通过，并检查表预览。
5. 点击“生成”。生成成功后再编译项目。

自动生成的文件会在下次生成时覆盖。请把业务扩展放在自己的文件中，不要直接修改生成文件。

## 核心 API

### 初始化和访问

无参数入口使用 Workbench 保存的运行时路径：

```csharp
using YokiFrame;

TableKit.Init();
var tables = TableKit.Tables;

TableKit.Clear();
await TableKit.InitAsync();
var asyncTables = TableKit.Tables;
```

需要临时使用其它路径时，可以把包含 `{0}` 的模板传给 `Load` 或 `LoadAsync`：

```csharp
var tables = TableKit.Load("Tables/{0}");
var asyncTables = await TableKit.LoadAsync("Tables/{0}");
```

| API | 说明 |
|---|---|
| `Init()` / `InitAsync()` | 初始化一次表管理器；重复调用不会重复加载。 |
| `Load()` / `LoadAsync()` | 初始化并返回强类型表管理器。 |
| `Init(string pathPattern)` / `InitAsync(string pathPattern)` | 使用指定路径模板初始化。 |
| `Load(string pathPattern)` / `LoadAsync(string pathPattern)` | 使用指定路径模板初始化并返回表管理器。 |
| `Tables` | 获取已初始化的表管理器；未初始化时会抛出异常。 |
| `Initialized` | 判断表管理器是否已经初始化。 |
| `Clear()` | 清理当前运行时和编辑器缓存；下次访问时重新加载。 |

异步返回类型由项目是否安装 UniTask 决定：安装时为 `UniTask`，否则为 .NET `Task`。

### 自定义资源加载

通常不需要配置 Loader，默认实现会通过 ResKit 读取表数据。项目使用自有资源系统时，可以实现 `ITableDataLoader` 并注入：

```csharp
TableKit.SetLoader(new ProjectTableDataLoader());
await TableKit.InitAsync();
```

`ITableDataLoader` 提供同步和异步两种读取方法。路径模板中的 `{0}` 会替换为 Luban 表名；`TableDataResourceLoadMode.Asset` 读取宿主资源对象，`Raw` 读取原始字节或文本。跨引擎项目通常选择 `Raw`，Unity `TextAsset` 等资源对象可选择 `Asset`。

### 编辑器数据

在 Unity Editor 或 Godot Tools 中，生成的门面还提供独立的编辑器数据入口：

```csharp
#if UNITY_EDITOR
TableKit.SetEditorDataPath("Assets/Resources/Art/Table/");
TableKit.RefreshEditor();
var editorTables = TableKit.TablesEditor;
#endif
```

编辑器入口读取指定目录的数据，不会修改运行时的 `TableKit.Tables`。

## 资源路径

Workbench 会把最终路径模板写入项目运行时配置：

- 开启资源可寻址时，模板固定为 `{0}`，Loader 直接收到 Luban 表名。
- 关闭可寻址并留空模板时，Workbench 会根据输出目录推导路径：Unity 的 `Resources` 使用资源相对路径，`StreamingAssets` 使用 `streaming-assets://`，Godot 项目内目录使用 `res://`。
- 无法可靠推导时，验证会要求开启资源可寻址或填写模板。自定义模板可以指向项目外目录、Godot `user://` 或其它资源系统，但必须保留 `{0}` 占位符。

Workbench 不会替项目安装 Addressables、YooAsset 或其它资源方案；它只保存并传递路径模板。

## 生命周期与错误边界

- 生成前必须先验证；验证失败时不会写入可用的半成品。
- Luban 配置或数据格式变化后，应重新验证并生成，再编译项目。
- `TableKit.Clear()` 后已有表管理器引用不再代表当前缓存，业务应重新初始化并获取 `Tables`。
- 运行时路径模板为空、表文件不存在或 Loader 返回空结果时，初始化会失败并报告错误。

## 在工具中查看

Workbench 的 TableKit 页面提供配置校验、表预览和代码生成。生成结果属于项目代码，由项目自行提交和维护。

## 限制与相关资料

TableKit 是离线生成工具，不提供运行时表格编辑。生成失败时优先检查 Luban 配置、目标、数据扩展名、输出目录和运行时路径模板。
