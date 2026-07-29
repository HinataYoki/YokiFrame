# LogKit 日志

## 适用场景

LogKit 是跨宿主日志门面。它负责等级过滤、开关和向 `IEngineLogger` 转发；Unity/Godot 的控制台实现由宿主接入。需要查看历史或调试覆盖层时，可使用 Workbench 或宿主自己的调试工具。

## 使用前提

LogKit 可直接用于 Unity 与 Godot .NET Runtime。控制台输出由当前宿主提供；Workbench 可以查看会话历史和配置，但不会替代宿主日志窗口。

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

在 Unity 中，适配层（`UnityEngineLogger`）的转发方法标记了 `[HideInCallstack]`，使 Unity 原生调用堆栈中这些帧不带文件行号后缀，Console 详情区仍显示完整帧列表。Core 禁止引用引擎无法使用该 attribute，因此 Editor 适配层额外注册了 `[OnOpenAsset(-1)]` 回调：双击 Console 条目时，回调读取当前选中条目的堆栈文本，跳过所有 `YokiFrame.LogKit:` 包装帧后，重定向到首个业务调用帧；2022.3 ～ Unity 6 均兼容（不同版本的回调签名通过条件编译隔离）。Core 不再自行捕获 `System.Diagnostics.StackTrace`，热路径无 GC 开销，普通日志的 `LogKitEntry.StackTrace` 为空，异常日志仍保留异常自身堆栈。

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
| `IEngineLoggerWithStackTrace` | `Log(LogLevel, string, object, string)` | 仅工具 logger 可选实现的扩展点，用于接收异常堆栈；内置 Unity/Godot 后端不实现，调用点由宿主原生堆栈呈现。 |
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
| `ResetToDefaults()` | 恢复全部设置默认值。 |

日志设置还可以控制是否保存历史、调试覆盖层、保留条数和文件大小。不同宿主的可用项以当前设置页面为准；编辑器设置与运行时设置分开保存。

## 生命周期与错误边界

- 未安装 logger 时，已注册 Adapter 工厂的首次写日志会创建宿主后端；未注册工厂时保持空后端，也可以显式 `SetLogger`。
- 日志过滤应在调用点尽早生效；高频路径不要把复杂字符串拼接放在可能被过滤的参数中。
- Unity Console 的调用点定位由 `[HideInCallstack]` 交给宿主原生堆栈完成，不产生额外运行时开销；自定义 logger 若要复现该行为，需自行标记转发方法。
- 开启调试覆盖层后，宿主会创建跨场景日志视图；关闭时不保留额外 UI 或历史缓存。
- `Reset()` 会清理静态状态，不应在业务模块仍持有 logger 时随意调用。

## 在工具中查看

Workbench 可以查看日志设置、会话历史和文件尾部。修改设置前请确认目标项目和 engine。

## 限制与相关资料

- 文件写入和加密是否可用以当前宿主设置为准。
- 需要批量读取日志设置时，使用当前项目的 `yoki log` 命令。
