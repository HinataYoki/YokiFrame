# LogKit

> 面向读者：需要统一业务日志与宿主日志后端的 Runtime 开发者
>
> 主要入口：`LogKit`
>
> 运行边界：跨宿主 Runtime；历史和 Workbench 状态只在 Editor/Tools 编译
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

LogKit 是跨宿主日志门面。它负责等级过滤、开关和向 `IEngineLogger` 转发；Unity/Godot 的控制台实现由宿主 Adapter 注入。历史、文件预览和 Workbench 状态属于 Editor/Tools 观察能力，不是 Player Runtime 的日志 API；Player 调试覆盖层则由各自 Runtime Adapter 按设置创建。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现，位于 `Core/Runtime/LogKit` |
| 程序集 | Core Runtime 编入 `YokiFrame`；logger 契约位于 `Core/Runtime/Interfaces` |
| Interaction | 已实现，Provider 位于 `Core/Editor/LogKit` |
| Workbench | 已实现，支持配置、内存历史和按需文件尾读 |
| Player 调试覆盖层 | 已实现：Unity IMGUI；Godot `CanvasLayer + Control` |
| 状态入口 | `LogKit/state` |

## 快速上手

```csharp
using YokiFrame;

LogKit.Info("Player ready");
LogKit.Warning("Profile fallback used");
LogKit.Error("Profile load failed");

try
{
    LoadProfile();
}
catch (System.Exception exception)
{
    LogKit.Exception(exception);
}
```

宿主 Adapter 会注册默认 logger 工厂，首次写日志时惰性安装 logger。业务代码不要直接用 Unity `Debug` 或 Godot `GD` 作为框架日志后端。

## 核心 API

### `LogKit`

| API | 说明 |
|---|---|
| `Enabled` | 总开关；关闭后日志不会转发。 |
| `MinimumLevel` | 最低等级；低于该等级的消息在格式化前丢弃。 |
| `HasLogger` | 是否已注入宿主 logger；读取不会主动创建后端。 |
| `RegisterDefaultLoggerFactory(Func<IEngineLogger>)` | 注册宿主默认 logger 工厂，首次写日志时创建。 |
| `SetLogger(IEngineLogger logger)` | 注入或替换 logger；传入 `null` 清空。 |
| `GetLogger()` / `ClearLogger()` | 获取或清除原始 logger。 |
| `Reset()` | 恢复默认开关、等级、logger，并在工具构建中清空历史。 |
| `Debug` / `Log` / `Info` / `Warning` / `Error` | 写入对应等级消息，签名为 `(object message, object context = null)`。`Log` 是 Info 别名。 |
| `Exception(Exception exception, object context = null)` | 写异常并保留异常类型、消息和堆栈。 |
| `DebugLog(string)` / `DebugWarning(string)` / `DebugError(string)` | 开发检查入口，只在 Editor、Checks 或 Godot Tools 构建中生效。 |

`context` 仅作为宿主透明对象传递，Core 不访问宿主专属成员。`LogLevel` 包含 `Debug`、`Info`、`Warning`、`Error`；非法等级会回退到 `Debug`。

### Logger 契约和数据类型

| 类型 | 公开成员 | 说明 |
|---|---|---|
| `IEngineLogger` | `Log(LogLevel level, string message, object context = null)` | 宿主控制台的最小 Runtime 契约。 |
| `IEngineLoggerWithStackTrace` | `Log(LogLevel, string, object, string)` | 仅工具 logger 可选实现，用于接收过滤后的调用点堆栈。 |
| `LogKitStats` | `LoggerName`、`HasLogger`、`Enabled`、`MinimumLevel`、`HistoryCount`、`DroppedCount` | 工具统计快照。 |
| `LogKitEntry` | `Level`、`Message`、`Context`、`ExceptionType`、`ExceptionMessage`、`StackTrace`、`TimestampUtc` | 工具历史条目。 |

### Runtime Settings

Unity Runtime 权威配置文件为：

```text
Assets/Settings/Resources/YokiFrame/runtime-settings.json
```

`LogKitSettings` 的公共入口如下：

| API | 说明 |
|---|---|
| `KIT_NAME`、`*_KEY`、`DEFAULT_*` | Kit 名称、配置 key 和默认值常量。 |
| `Enabled` / `MinimumLevel` | 读取当前 `KitSettings` 中的基础配置。 |
| `ApplyBaseRuntimeSettings()` | 将配置同步到 `LogKit`。 |
| `RuntimeSettingsApplied` | Runtime 设置同步完成后的宿主通知；仅 Adapter 用于刷新自身实现。 |
| `GetBool` / `GetInt` / `GetString` | 读取带默认值的配置。 |
| `BuildJson()` / `AppendJson(StringBuilder)` | Editor/Tools 构建完整扁平设置对象。 |
| `ApplyPayload(string)` | Editor/Tools 原子应用完整 payload；缺失、未知或类型错误字段抛 `ArgumentException`。 |
| `ResetToDefaults()` | Editor/Tools 恢复全部设置默认值。 |

当前配置还包含 `saveLogInPlayer`、`enableIMGUIInPlayer`、`enableEncryption`、队列、保留天数、文件大小、目录和文件名等字段。`enableIMGUIInPlayer` 已在 Unity/Godot Player 生效：Unity 使用 IMGUI，Godot 使用原生 `CanvasLayer + Control`；`imguiMaxLogCount` 限制两端保留的日志条数。`fileWriter` 与 `encryption` 仍未实现，仍以 capability 为准。Editor 配置保存到项目 `ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json`，不要把 Editor 字段写进 Runtime JSON。

## 宿主与工具入口

工具构建额外提供 `DiagnosticVersion`、`LoggerName`、`GetHistory(List<LogKitEntry>)`、`ClearHistory()` 和 `GetStats()`。内存历史最多 128 条，超出后丢弃最旧记录。`LogKitHostEnvironment.Configure(...)` 只供宿主设置文件位置和 capability，普通业务不应调用。

Unity Editor 与 Godot Tools 会在工具宿主进入可用状态时配置该环境，因此 Workbench 可以在尚未写入第一条日志时读取状态并应用设置；默认 logger 仍只在首次真实写日志时创建。

Workbench 通过 `LogKit/state` 读取设置、能力、有界内存历史和文件元数据；文件正文只在用户显式操作时按需读取有限尾部。CLI action：

```powershell
yoki command send --engine <engineId> --kit LogKit --action get_workbench_snapshot --project <projectRoot>
yoki command send --engine <engineId> --kit LogKit --action read_log_file --payload '{"kind":"editor"}' --project <projectRoot>
```

还支持 `set_settings`、`reset_settings` 和 `clear_history`。其中 `set_settings` 需要完整顶层设置对象，CLI 改变的是当前 Runtime 会话；Workbench 保存项目配置和应用当前 Runtime 配置是两个独立动作。

## 生命周期与错误边界

- 未安装 logger 时，已注册 Adapter 工厂的首次写日志会创建宿主后端；未注册工厂时保持空后端，也可以显式 `SetLogger`。
- 日志过滤应在调用点尽早生效；高频路径不要把复杂字符串拼接放在可能被过滤的参数中。
- 开启 `enableIMGUIInPlayer` 后，Unity Adapter 创建跨场景 IMGUI 覆盖层，Godot Adapter 由 Bootstrap 挂载原生 Control 覆盖层；关闭时两端不保留 UI 或日志缓存。
- Player 不编译工具历史、Provider、文件预览和 Workbench 诊断对象。
- `Reset()` 会清理静态状态，不应在业务模块仍持有 logger 时随意调用。

## 限制与相关资料

- `fileWriter` 与可信加密当前不提供；是否可用以宿主 capability 为准，不能根据配置字段推断
- Workbench 项目配置保存与当前 Runtime `set_settings` 是两个独立操作
- 需要操作日志文件、Runtime command 或诊断 evidence 时使用 [Workbench、CLI 与 Installer](../../Guides/Tooling.md)
