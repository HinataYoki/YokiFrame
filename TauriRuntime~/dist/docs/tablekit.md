# TableKit 配置表

## 先判断环境

打开 TableKit 页面后先看 `环境与路径` 区块。

| 状态 | 说明 |
|---|---|
| Luban 可用 | 可以验证和生成配置表代码。 |
| Luban 不可用 | 可以编辑配置，但不能当成可生成环境。 |
| 输出目录为空 | 先设置代码输出目录和数据输出目录。 |
| 运行时路径模式为空 | 先设置表数据在 ResKit 中的读取路径。 |

## 页面区块

| 区块 | 用途 |
|---|---|
| 命令中心 | 还原默认、打开配置表、验证、生成。 |
| 环境与路径 | Luban 工作目录、`Luban.dll`、输出目录、运行时路径模式。 |
| 构建选项 | 独立程序集、`ExternalTypeUtil`、异步加载入口。 |
| 控制台 | 生成和验证输出。 |
| 数据预览 | 查看验证阶段生成的临时 JSON。 |

配置保存在 Tauri 前端 `localStorage`，并按当前 engine registry 的 `projectPath` 派生项目级 key，避免多个 Unity/Godot 项目共用同一份 TableKit 路径配置。

## 生成产物

| 产物 | 默认落点 | 说明 |
|---|---|---|
| Luban 生成代码 | `Assets/Scripts/TableKit/Luban` | 表入口由 `luban.conf` 决定。 |
| `TableKit.cs` | 代码输出目录 | 项目侧运行时入口。 |
| `ExternalTypeUtil.cs` | 代码输出目录 | 可选，处理外部类型转换。 |
| 表数据 | `Assets/Resources/Art/Table/` | `.bytes`、`.json` 或其它 data target。 |
| asmdef | 代码输出目录 | Unity 可选生成。 |

这些产物属于用户项目，不是 YokiFrame 包内 Runtime。

## 运行时加载

生成的 `TableKit.cs` 默认通过 ResKit 读取表数据：

```csharp
YokiFrame.ResKit.SetProvider(new YourResourceProvider());

TableKit.RuntimePathPattern = "Art/Table/{0}";
TableKit.Init();

var tables = TableKit.Tables;
```

需要绕过 ResKit 时，覆盖生成代码里的 loader：

```csharp
TableKit.SetBinaryLoader(fileName => LoadBytesFromProjectRuntime(fileName));
TableKit.SetJsonLoader(fileName => LoadTextFromProjectRuntime(fileName));
```

异步模式会生成 `InitAsync` / `ReloadAsync`。启用 `YOKIFRAME_UNITASK_SUPPORT` 时使用 UniTask，否则使用 `Task`。

## 工作台使用

TableKit 页面是配置表生成工作台，不是运行时 Kit 状态页。
Tauri 的 TableKit 页面只读取 engine registry 中的 Luban 可用性和项目路径，不通过运行时 `TableKit/*` 命令获取状态。
页面不会读取 `TableKit/state` snapshot，也不会把 TableKit 当成运行时诊断页轮询。
Tauri 后端通过 `dotnet Luban.dll` 执行生成和验证，输出日志会写入页面控制台。

| 在工作台里做什么 | 用途 |
|---|---|
| 检查环境 | 确认 Luban 是否可用、宏是否启用。 |
| 配置路径 | 设置 Luban 工作目录、代码输出目录和数据输出目录。 |
| 调整构建选项 | 设置独立程序集、外部类型工具、异步加载入口。 |
| 验证 | 先检查配置和输出路径，避免直接生成失败。 |
| 生成 | 输出 Luban 代码、`TableKit.cs` 和表数据。 |
| 查看控制台 | 读取生成日志和错误信息。 |
| 数据预览 | 查看验证阶段生成的临时 JSON。 |

如果页面显示 Luban 不可用，先看环境区块；如果运行时找不到表，先看生成产物和 `RuntimePathPattern`。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 页面显示 Luban 不可用 | 检查 Luban 包、宏和工作台环境区块。 |
| 生成后运行时报找不到表 | 检查数据输出目录和 `RuntimePathPattern`。 |
| Unity/Godot 路径不同 | 让 ResKit Provider 处理路径差异。 |
| 找不到 `TableKit` 类 | 它是生成到用户项目的代码，先执行生成。 |
| 以为它是运行时状态页 | TableKit 是生成工具页，不展示运行时 Kit 快照。 |
