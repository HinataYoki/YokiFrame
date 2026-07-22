# TableKit

> 面向读者：使用 Luban 生成项目数据表代码的工具使用者和 Runtime 开发者
>
> 主要入口：Workbench TableKit 页面与生成后的 `YokiFrame.TableKit` 门面
>
> 运行边界：离线生成工具；未生成项目不携带 TableKit Runtime
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

TableKit 是 Luban 的 Workbench 生成入口。Workbench 读取 `luban.conf`、执行 Luban、保存项目草稿，并在 Luban 成功后直接生成项目自有 C# 门面和宿主程序集文件。未生成时包和用户项目都没有 TableKit Runtime 类型。

## 入口与当前状态

Workbench Luban 验证/生成页与跨宿主直接代码生成已实现；Kit Interaction Provider 尚未发布。

## 快速上手

1. 打开 Workbench 的 TableKit 页面，确认 `luban.conf`、Luban 工具路径、代码输出目录和数据输出目录。
2. 执行验证，检查实际 `topModule.manager`、代码目标、数据目标和数据扩展名。
3. 执行生成。代码输出字段表示 TableKit 根目录；Luban 生成代码写入 `<TableKitRoot>/Luban`，门面、加载契约、外部 helper 和用户扩展留在父目录。
4. Unity 生成可选 `<AssemblyName>.asmdef`；Godot .NET 生成 `<AssemblyName>.csproj` 并由主项目通过 `ProjectReference` 接入。

## 核心 API

生成目录中的 `ITableDataLoader.cs` 使用 `YokiFrame.TableKit` 命名空间，并与门面、Luban 代码编入同一个生成程序集：

| API | 说明 |
|---|---|
| `ITableDataLoader.Load(string resourcePathPattern, string tablesTypeName)` | 按路径模板同步读取并创建表管理器 |
| `ITableDataLoader.LoadAsync(string resourcePathPattern, string tablesTypeName)` | 按路径模板异步读取并创建表管理器 |

路径模板中的 `{0}` 由项目 Loader 替换为 Luban 传入的表名，例如 `config`、`item`。生成门面固定提供 `SetLoader`、无参数 `Init/Load`、路径模板 `Init/Load`、`Tables`、`Initialized` 和 `Clear`；启用异步加载时额外生成 `InitAsync/LoadAsync`。

```csharp
using YokiFrame.TableKit;

cfg.TableKit.SetLoader(new GeneratedTableLoader());
cfg.TableKit.Init();
cfg.Tables tables = cfg.TableKit.Tables;

// 仅在 Workbench 启用异步加载时生成。
await cfg.TableKit.InitAsync();
```

`GeneratedTableLoader` 是项目实现。门面只负责保存强类型 Luban manager、调用统一 Loader 和管理生命周期，不选择 ResKit Provider，也不假设资源一定是 `.bytes` 或 `.json`。

### 资源定位

Workbench 页面只保留一个“资源可寻址”开关和一个非可寻址时的“运行时地址模板”输入：

- 开启可寻址时，生成契约中的 `IsAddressable` 为 `true`，`RuntimePathPattern` 固定为 `"{0}"`。Loader 直接收到 Luban 的表名。
- 关闭可寻址且模板为空时，Workbench 根据数据输出目录推导模板。Unity `Resources` 输出推导为 `Resources` 相对路径，`StreamingAssets` 输出推导为 `streaming-assets://`；Godot 项目内输出推导为 `res://`。
- 输出目录无法可靠推导时，验证会提示用户开启可寻址或填写模板。用户填写的模板会原样规范化并追加 `{0}`（已有占位符时不重复追加），可用于项目外目录、Godot `user://` 或其它 C# 引擎资源系统。

Workbench 不探测 Addressables、YooAsset 或其它资源管理方案，也不生成 `LogicalAddress`、`TableDataLocation` 或第二套 Loader。运行时配置只存在于生成代码常量，不写入 Runtime Settings。

### 配置与生成

具体表管理器类型、代码目标和数据格式均来自当前 `luban.conf` 的 `topModule`、`manager`、`codeTarget` 和 `dataTarget`，不假设默认大小写或固定 `cfg.Tables`。`codeTarget` 与 `dataTarget` 是开放字符串，数据扩展名从 target 推导或读取 `fileExt`。

Workbench-only 配置保存到当前项目 `ProjectSettings/Packages/com.hinatayoki.yokiframe/tablekit-settings.json`，工具栏“保存”和 Workbench 正常关闭都会持久化完整草稿。配置包含 `IsAddressable` 和 `RuntimePathPattern`；自动推导值不写回，项目重新打开时会按最新输出目录重新计算。

“自定义编辑器数据路径”关闭时，“编辑器数据”始终由当前“数据输出”目录推断并实时同步；开启后才使用独立选择的目录。重新关闭自定义开关会立即恢复为当前数据输出目录。

验证预览只读取最多 32 个、单个不超过 512 KiB 的临时 JSON 文件；每张表最多物化前 200 条记录，复杂字段在列表中只显示摘要。超出预算或无法读取的文件会写入控制台诊断，完整临时输出仍保留在 `Temp/LubanValidate` 供定位。

主 Luban 目标固定写入 `<TableKitRoot>/Luban`，因此 Luban 清空目标目录不会删除父目录中的 `TableKit.cs`、`ITableDataLoader.cs`、`External/` 或用户代码。生成不经过 manifest 或 Unity postprocessor。

外部类型 helper 读取 `luban.conf` 的 `schemaFiles` XML，实际以 `builtin.xml` 中匹配当前 `target + codeTarget` 的 bean mapper 为准：`option name="type"` 决定目标类型，`option name="constructor"` 的类名和方法名决定 helper 文件与入口名称。该解析遵循 Luban [类型映射文档](https://www.datable.cn/docs/manual/typemapper)。

## 生命周期与错误边界

- 生成前先验证 `luban.conf`、目标类型、数据扩展名和项目根 containment；失败时不把半成品视为可用 Runtime
- 生成门面与宿主程序集属于可再生项目代码；业务扩展放在用户维护的文件中，不直接修改可再生输出
- TableKit 草稿只属于 Workbench Editor 配置，不进入 Runtime Settings

## 宿主与工具入口

Unity 使用 `<AssemblyName>.asmdef` 表达程序集边界，并引用 `Luban.Runtime`。Godot .NET 使用 `<AssemblyName>.csproj` 表达独立项目边界，继承主项目 TargetFramework；主项目排除 TableKit 源码 glob 后添加 `ProjectReference`，不能把 Unity `.asmdef` 当作 Godot 配置复用。

两端默认程序集名均为 `YokiFrame.TableKit`，不会生成或引用 `YokiFrame.TableKit.Contracts`。Godot 只生成 `.csproj`，Unity 只生成 `.asmdef`。

## 限制与相关资料

TableKit 页面是离线生成工具，不提供 Runtime Interaction。生成失败时应检查 Luban 退出码、实际 target 类型、数据扩展名、项目类型和路径 containment；不要直接修改自动生成的门面或程序集文件。
