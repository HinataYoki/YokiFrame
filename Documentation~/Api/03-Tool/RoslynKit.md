# RoslynKit 宿主操作

## 适用场景

RoslynKit 用来操作**已经打开**的 Unity 或 Godot 编辑器：进入或退出播放、查看和修改当前场景、调用已经在运行的服务、截一张游戏画面。

它也用来在**不退出播放**的情况下试一段临时行为或改一个数值。试完要留下的功能，写成普通 C# 源码。

不要用 RoslynKit 实现新的游戏功能。服务、状态、资源和 UI 仍写在对应 Kit 里。

## 使用前提

- 只在 Unity 编辑器，或 Godot .NET 的编辑器 / 运行工具里可用。打进玩家包后不能用。
- 没有 Workbench 页面。命令通过项目 `.yokiframe/runtime/` 里的 `yoki` 发送。
- 会改编辑器状态的操作默认关闭。查询状态不需要打开开关。
- 提交任意 C# 前必须由用户明确允许。这不是沙箱，代码和编辑器进程权限相同。

Unity 的开关在 `ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json` 的 `settings` 数组中：

```json
{ "kit": "RoslynKit", "key": "operations.enabled", "value": "true" }
{ "kit": "RoslynKit", "key": "scripts.trustedCSharp", "value": "true" }
```

第二项只在要执行 C# 时需要。Godot 改 `project.godot`：

```ini
yokiframe/engine/operations_enabled = true
yokiframe/engine/trusted_csharp = true
```

改完立即生效，不用重启。只添加缺失项，不要覆盖文件里的其他设置。

## 快速上手

先看当前编辑器能不能做、是不是在播放：

```bash
yoki command send --kit RoslynKit --action engine_capabilities
yoki command send --kit RoslynKit --action domain_state
```

进入播放。返回只表示请求已接受，要再查一次状态：

```bash
yoki command send --kit RoslynKit --action play_control --payload "{\"command\":\"enter\",\"confirmed\":true}"
yoki command send --kit RoslynKit --action domain_state
```

`domain_state` 里的 `isPlaying` 为 true 后，再查场景或调用服务。`target` 只能写一个：Unity 用 `editor` 或 `play`，Godot 用 `editor` 或 `runtime`。

查看当前场景：

```bash
yoki command send --kit RoslynKit --action scene_query --payload "{\"depth\":2}"
```

调用一个已经在运行的服务。类型和方法必须来自前一条查询，不能猜名字：

```bash
yoki command send --kit RoslynKit --action object_list --payload "{\"root\":\"service\",\"target\":\"play\",\"limit\":25}"
```

```powershell
@'
var model = engine.RequireService<Demo.StatsModel>();
int before = model.Read("Sentinel").Health;
await engine.WaitFrames(3, clock: "gameFrame");
test.Equal(model.Read("Sentinel").Health, before, "health unchanged");
'@ | yoki script --engine unity-editor --target play --confirm-execution --stdin --project <项目根>
```

还在编辑状态时，把 `--target` 写成 `editor`，等待用 `editorTick`。`editorTick` 不是游戏帧。含中文的脚本用项目内文件交给 `--file`，不要从管道传入。

## 常用操作

| 要做的事 | 命令 |
|---|---|
| 看模式和播放状态 | `domain_state` |
| 看这个编辑器实际支持哪些操作 | `engine_capabilities` |
| 进播放、退出、暂停、单步 | `play_control`，并带 `"confirmed":true` |
| 看场景层级 | `scene_query` |
| 创建、删除、显隐或移动一个对象 | `scene_mutate` |
| 在 Unity 里刷新或查找资产 | `asset_ops` |
| 查看已初始化服务的公开方法 | `object_list`，再用 `object_describe` |
| 执行一段 C# 并等待结果 | `yoki script` |

`scene_query` 默认深度 2，最多 8 层、2000 个节点。超出时结果带 `truncated`，不是完整场景。

对象目录只列出已经初始化的服务，不会顺便创建它们，也不会执行属性的 getter。

## 不退出播放时试行为

只有确实不能退出播放时才用 LiveCode。在同一次 `yoki script` 里把临时行为挂到场景对象上：

```csharp
await engine.LiveCode.Attach("walker", target, "Walker", members);
engine.LiveCode.SetField("walker", "Speed", 1.5f);
```

`members` 是类里面的字段和方法，不是完整类，也不能写 `using`。私有字段要调参时，提供 public 或 `[SerializeField]` 字段。

试完要保留的行为，写成普通 MonoBehaviour 或 Godot 脚本。LiveCode 不是最终代码。

Godot 可以挂行为、批量修改和恢复，不能打补丁、不能导出成正式脚本，也不能截图。

## 限制

- `play_control` 的 `isPlaying` 是操作前的状态。进入播放后可能发生脚本重载，结果未知时先查询，不要再发一次。
- 编译器不可用时，`script_status` 的 `installed` 为 false。Unity 安装包里已经带了编译器；Godot 安装不包含它，需要按安装说明准备后才能执行 C#。
- 内存程序集不会卸载。预算用完就停止新建脚本或临时行为，不要连续重试。
- 玩家包、IL2CPP 和 Godot 导出包没有这些命令。

## 在工具中查看

RoslynKit 没有 Workbench 页面。状态用上面的 `yoki` 命令读取。只想看框架心跳和诊断时，用 Workbench 的框架页，不必打开执行开关。
