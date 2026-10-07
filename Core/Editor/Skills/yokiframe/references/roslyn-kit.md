# RoslynKit：驱动宿主与内存编译

只有用户明确要求驱动宿主，或要在不退出 Play Mode 的情况下改变代码行为时，才读这一页。实现游戏能力直接写正式源码并落盘，不要从这里开始。

本页用 `yoki` 进播放、查改场景、调用已经在跑的服务。RoslynKit 没有 Workbench 页面，也不是游戏 Runtime。不退出 Play 就改行为或调参时，转 [livecode.md](livecode.md)。

不要为了读一次数而生成任务 `.cs`。运行中的数值以 CLI 返回为准。

## 先确认开关

执行类操作默认关闭。缺失、解析失败或不是 `true` 一律拒绝；`domain_state`、`engine_capabilities`、`script_status`、`object_list`、`object_describe` 和 `run_result` 仍可查询。改完立即生效，不用重启。

| 宿主 | 执行 | 受信任 C# |
|---|---|---|
| Unity | `editor-settings.json` 的 `settings` 里 `{"kit":"RoslynKit","key":"operations.enabled","value":"true"}` | 同一数组再加 `{"kit":"RoslynKit","key":"scripts.trustedCSharp","value":"true"}` |
| Godot | `project.godot` 的 `yokiframe/engine/operations_enabled = true` | `yokiframe/engine/trusted_csharp = true` |

文件位置：Unity 为 `ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json`。只在用户授权时改，不覆盖其他条目。

先看能不能做：

```bash
yoki command send --kit RoslynKit --action engine_capabilities
yoki command send --kit RoslynKit --action script_status
```

`script_status.installed` 为 false 时，按 [installer.md](installer.md) 准备编译器，不要临时下载或调用外部编译器。

## 调用已有服务

1. `domain_state` 确认当前是 editor、play 还是 runtime。
2. `object_list` / `object_describe` 取真实类型和方法。只查已初始化的 Architecture 服务，不创建架构，不执行 getter。
3. 用 `yoki script` 提交 C# 方法体。类型名必须来自上一步，下面的名字只说明形状。

```bash
yoki command send --kit RoslynKit --action object_list --payload '{"root":"service","target":"play","limit":25}'
yoki command send --kit RoslynKit --action object_describe --payload '{"target":"play","objectId":"<上一步的 ID>","limit":100}'
```

```powershell
@'
var model = engine.RequireService<Demo.StatsModel>();
int before = model.Read("Sentinel").Health;
await engine.WaitFrames(3, clock: "gameFrame");
test.Equal(model.Read("Sentinel").Health, before, "health unchanged");
engine.ConsoleLog("health=" + before);
'@ | yoki script --engine unity-editor --target play --confirm-execution --stdin --project <projectRoot>
```

- `--confirm-execution` 不打开项目开关。方法体不是完整类，不写 `using` 以外的入口属性；上限 128 KiB。含中文时用项目内 `--file`，不要靠管道传非 ASCII。
- 还没进 Play 就用 `--target editor` 和 `clock: "editorTick"`。`editorTick` 不是游戏帧。`play_control` 只表示接受，进播放后必须再看 `domain_state` 的 `isPlaying`。
- `RequireService<T>` 不触发懒初始化。多个架构有同一服务时传入架构类型全名。比较前后值时先保存标量。
- `test.Equal` 失败即失败，被 catch 也不会变成通过。`yoki script` 只在终态 `Passed` 时成功。结果不明就用 `run_result` 对账，不重新提交。
- Unity 截图：`await engine.Capture("game", ".yokiframe/automation/evidence/preview.png", autoNumber:true)`。要求正在运行且未暂停的 Game 视图。Godot 不能截图。
- 预算看 `script_status` 的剩余次数和字节。耗尽就停。删除脚本不退还，也不要连续重试。

## 宿主操作

`target` 只能是一个值：`editor`、`play` 或 `runtime`。`editor|play` 会报 `RoslynOperationInvalidPayload`。

| action | 风险 | 目标 | 作用 |
|---|---|---|---|
| `domain_state` | ReadOnly | 不限 | 模式、是否播放/编译、开关 |
| `engine_capabilities` | ReadOnly | 不限 | 这个宿主实际能做哪些 action |
| `play_control` | Dangerous | editor | `{"command":"enter"\|"exit"\|"pause"\|"resume"\|"step","confirmed":true}`。响应不是最终状态 |
| `scene_query` | ReadOnly | editor、play | `{"path":"Root/Child","depth":2,"includeInactive":true}`。深度最多 8，节点最多 2000 |
| `scene_mutate` | Dangerous | editor、play | `{"op":"create"\|"delete"\|"setActive"\|"setTransform"\|"save","path":"Root/Child"}` |
| `asset_ops` | UserAction | editor | `{"op":"refresh"\|"import"\|"find"\|"inspect","path":"Assets/..."}`。只在 Unity |

`scene_query` 也要执行开关。不要用 `inspect` 读业务值，它可能执行 getter。多步等待用 `yoki exec` 从 stdin 喂 NDJSON，不要写临时 `.ps1` / `.sh`，格式见 [cli-commands.md](cli-commands.md)。

## Godot

用 `--engine godot-editor --target editor` 或 `--engine godot-runtime --target runtime`。没有 play 目标。`gameFrame` 只给 runtime。

场景命令和 Unity 用同一份 payload，语义不同：

| 差异 | Unity | Godot |
|---|---|---|
| 创建 | `components` 类型名 | `nodeType`，缺省 `Node` |
| 删除 | 立即，可 Undo | `QueueFree`，`deferred=true` |
| 保存 | `scene_mutate save` | 只有 godot-editor；runtime 返回 `RoslynOperationUnavailable` |

Godot 的 C# 仍用 `yoki script`。`eval` 只接受 `language=gdscript`，不能当作 C# 回退。不支持 Patch、Export、Bind、Capture。

## 会话换了

`HostIdentityChanged`、`RoslynReloading` 或结果 `Unknown` 表示会话换代，不是失败。用 `command status --request-id <id>` 或 `run_result` 查证。不要重放 `yoki script` 或 `play_control`。

## 错误时

| 码 | 处理 |
|---|---|
| `RoslynOperationDisabled` | 打开上表里的执行开关 |
| `ScriptCompilerUnavailable` | 看 `script_status`，按安装页准备编译器 |
| `RoslynOperationInvalidPayload` | `target` 必须是单一值，payload 必须是 JSON 对象 |
| `RoslynOperationUnavailable` | 用 `engine_capabilities` 看当前 `hostTargets` |
| `ConfirmationRequired` | Dangerous 操作加 `confirmed:true` 或 `--confirm-execution` |

只读运行态用 `snapshot read` 或 `telemetry read`，不必开执行开关。改游戏规则就写业务代码。
