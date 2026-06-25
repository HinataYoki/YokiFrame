# TableKit 配置表编辑器

TableKit 是 Luban 配置表工作流的编辑器封装，不是 YokiFrame 框架 Runtime Kit。Tauri 的 TableKit 页面是编辑器入口，Unity 和 Godot 只负责报告 Luban 环境是否存在。

安装 Luban 后，Unity 和 Godot 适配层会自动维护 `YOKIFRAME_LUBAN_SUPPORT`。只有该宏存在时，TableKit 相关编辑器代码才参与编译；没有 Luban 时，Tauri 页面仍可编辑配置，但不会把它当成可生成环境。

## Tauri 编辑器

| 区块 | 作用 |
|---|---|
| 命令中心 | 设置 `Target` 和 `Code`，提供还原默认、打开配置表、生成配置表入口。 |
| 环境与路径配置 | 配置 Luban 工作目录、`Luban.dll`、数据输出、代码输出、编辑器数据路径、运行时路径模式和额外输出目标。 |
| 构建选项 | 设置是否生成独立程序集、`ExternalTypeUtil` 和异步加载入口。 |
| 控制台 | 显示 Luban 生成与验证输出，支持复制和清空。 |
| 数据预览与配置表信息 | 读取验证阶段生成的临时 JSON，用树形视图和表清单检查本次打包的数据结构。 |

主要配置项：

| 字段 | 默认值 | 说明 |
|---|---|---|
| Luban 工作目录 | `Luban/MiniTemplate` | 包含 `Datas`、`Defines`、`luban.conf` 的目录。 |
| Luban.dll 路径 | `Luban/Tools/Luban/Luban.dll` | Luban 代码生成工具 DLL。 |
| 导出目标 | `client` | 可选 `client`、`server`、`all`。 |
| 代码目标 | `cs-bin` | 可选 `cs-bin`、`cs-simple-json`、`cs-newtonsoft-json`。 |
| 数据目标 | `bin` | 可选 `bin`、`json`、`lua`。 |
| 数据输出目录 | `Assets/Resources/Art/Table/` | Luban data target 输出。 |
| 代码输出目录 | `Assets/Scripts/TableKit/` | Luban 代码和 `TableKit.cs` 的项目侧落点。 |
| 运行时路径模式 | `Art/Table/{0}` | `{0}` 为表文件名占位符。 |
| 编辑器数据路径 | 默认跟随数据输出目录 | `TablesEditor` 读取表数据的位置。 |

配置会保存在浏览器 localStorage，键为 `yokiframe.tablekit.generator.v1`。它属于 Tauri 前端偏好，不写入 YokiFrame Runtime，也不通过 `.yokiframe` 命令桥同步。

Tauri 后端通过 `dotnet Luban.dll` 执行生成和验证。打开配置表会优先打开 Luban 工作目录下的 `Datas`，不存在时回退到 Luban 工作目录；验证配置会清理并生成 `Temp/LubanValidate` 临时 JSON，再把表清单和结构预览回传给前端；生成配置表会按 Target 分批调用 Luban，复制额外输出目标，并在项目侧代码输出目录生成 `TableKit.cs`、可选 `ExternalTypeUtil.cs` 和 Unity asmdef。整个流程是编辑器工具能力，不能假装成 `TableKit` Runtime command。

## 生成产物

| 产物 | 落点 | 说明 |
|---|---|---|
| Luban 生成代码 | `代码输出目录/Luban` | 表入口类型由 `luban.conf` 的 `topModule` 和 `manager` 决定，例如 `Cfg.Tables` 或自定义命名空间。 |
| `TableKit.cs` | 代码输出目录 | 项目侧静态入口，和 Luban 代码一起编译。 |
| `ExternalTypeUtil.cs` | 代码输出目录 | 可选生成，处理 Luban vector 到引擎类型的转换。 |
| 表数据 | 数据输出目录 | `.bytes`、`.json` 或其它 Luban data target 输出。 |
| 可选 asmdef | 代码输出目录 | Unity 下默认引用 `Luban.Runtime`。Godot 没有 Unity asmdef 语义，应让生成代码参与项目 `.csproj` / `Directory.Build.props` 编译。 |

这些产物都属于用户项目，通常在 `Assets/Scripts/TableKit/` 或用户配置的代码输出目录中。YokiFrame 包内不提供 TableKit Runtime 代码。

开启异步加载模式后，生成的 `TableKit.cs` 会包含 `InitAsync` / `ReloadAsync` 和异步 loader 注入入口。`YOKIFRAME_UNITASK_SUPPORT` 存在时这些入口使用 UniTask；没有该宏时回退为 `System.Threading.Tasks.Task`。未开启异步加载模式时不会生成异步入口，也不会引用 UniTask 或 Task 异步依赖。

## 运行时 API

生成的 `TableKit.cs` 提供以下运行时 API：

| 属性/方法 | 说明 |
|-----------|------|
| `Initialized` | 是否已初始化。 |
| `Tables` | 获取生成的 Tables 实例。 |
| `RuntimePathPattern` | 设置运行时资源路径模式。 |
| `Init()` | 同步初始化。 |
| `InitAsync(token)` | 异步初始化（需开启异步加载模式）。 |
| `Reload(onComplete)` | 同步重载配置表。 |
| `ReloadAsync(token)` | 异步重载配置表。 |
| `Clear()` | 清空所有数据。 |
| `SetBinaryLoader(loader)` | 设置同步二进制加载器。 |
| `SetJsonLoader(loader)` | 设置同步 JSON 加载器。 |
| `SetAsyncBinaryLoader(loader)` | 设置异步二进制加载器。 |
| `SetTableFileNames(names)` | 覆盖表文件名列表。 |

## 运行时加载

生成的 `TableKit.cs` 默认不再区分 Unity / Godot。运行时数据读取统一委托给 `YokiFrame.ResKit.LoadRaw()` / `LoadRawText()`，Unity `Resources`、YooAsset、Godot `FileAccess` 或项目自定义资源系统的差异都应放在 ResKit Provider 中处理：

```csharp
YokiFrame.ResKit.SetProvider(new YourResourceProvider());

TableKit.RuntimePathPattern = "Art/Table/{0}";
TableKit.Init();

var tables = TableKit.Tables;
```

如果项目确实需要临时绕过 ResKit，也可以覆盖生成代码中的 loader：

```csharp
TableKit.SetBinaryLoader(fileName => LoadBytesFromProjectRuntime(fileName));
TableKit.SetJsonLoader(fileName => LoadTextFromProjectRuntime(fileName));
```

## Luban 环境

Tauri 的 TableKit 页面只读取 engine registry：

```text
.yokiframe/engines/<engineId>/engine.json
```

关注字段：

```json
{
  "optionalDependencies": {
    "luban": {
      "available": true,
      "define": "YOKIFRAME_LUBAN_SUPPORT"
    }
  }
}
```

页面不会读取 `TableKit/state` snapshot，也不会发送 `TableKit/get_workbench_snapshot`、`TableKit/stats` 或 `TableKit/list_tables`。

## AI 诊断入口

AI 排查 TableKit 环境时只需要确认两件事：

1. `engine.json` 里 `optionalDependencies.luban.available` 是否为 `true`。
2. 项目 `Assets/Scripts/TableKit/` 或用户配置的代码输出目录中是否存在 Luban 生成代码和 `TableKit.cs`。

不要通过 `.yokiframe` 发送 TableKit 命令。配置表内容、缓存和 loader 状态属于项目运行时代码，不由 YokiFrame 文件桥诊断。
