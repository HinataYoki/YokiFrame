# SaveKit 存档

SaveKit 是跨引擎存档门面。业务代码统一调用 `YokiFrame.SaveKit` 静态入口，具体文件目录、序列化和引擎生命周期由 Unity、Godot 或项目自定义 Adapter 注入。迁移 1.x 行为时不要把 `Application.persistentDataPath`、`JsonUtility` 或 Godot API 写进 Tools 层。

## 核心类型

| 类型 | 作用 |
|------|------|
| `SaveKit` | 静态统一入口，负责存档、读档、删除、槽位扫描、版本迁移和自动保存 Tick。 |
| `ISaveStorage` | 存储后端接口。默认内存后端用于测试，Unity/Godot Adapter 注入文件后端。 |
| `ISaveSerializer` | 模块序列化接口。可由 Base `ISerializationProvider` 包装，也可由项目自定义。 |
| `ISaveEncryptor` | 可选加密接口。当前提供 XOR 和 AES 示例实现。 |
| `SaveData` | 存档数据容器，按模块类型保存 raw bytes。 |
| `SaveMeta` | 槽位元数据，包含 slot、version、displayName、创建和保存时间。 |
| `ISaveMigrator` | 版本迁移接口，支持从旧版本迁移到当前版本。 |

## 后端 getter

| 方法 | 说明 |
|------|------|
| `GetSerializer()` | 获取当前序列化器。 |
| `GetEncryptor()` | 获取当前加密器。 |
| `GetStorage()` | 获取当前存储后端。 |
| `GetCurrentVersion()` | 获取当前存档版本。 |
| `GetMaxSlots()` | 获取最大槽位数。 |

## 设置后端

Unity 项目通常由统一初始化入口自动安装文件存储和 Unity 序列化 Provider：

```csharp
using YokiFrame;

YokiFrameKit.Initialize(YokiFrameEngine.Unity);
```

Godot 项目由 `GodotBootstrap` 自动安装 `user://` 下的文件存储和 Godot 序列化 Provider。两边业务调用入口一致：

```csharp
using YokiFrame;

var data = SaveKit.CreateSaveData();
SaveKit.Save(0, data, "Chapter 1");
```

项目自定义后端时只替换接口，不创建 `UnitySaveKit2` 或 `GodotSaveKit2`：

```csharp
SaveKit.SetStorage(new ProjectSaveStorage());
SaveKit.SetSerializer(new ProjectSaveSerializer());
SaveKit.SetEncryptor(new AesSaveEncryptor(projectSaveSecret));
```

`AesSaveEncryptor` 不能使用默认构造函数；项目必须传入自己的密码，或传入 32 字节 key 与 16 字节 IV。

## 存档与读档

```csharp
var data = SaveKit.CreateSaveData();
data.SetModule(new PlayerSaveModule { Level = 3 });

SaveKit.Save(0, data, "Boss");

var loaded = SaveKit.Load(0);
var player = loaded.GetModule<PlayerSaveModule>();
```

异步 API 默认使用 `Task<T>` / `CancellationToken`，不绑定 Unity Coroutine；安装 UniTask 后 Unity Adapter 会自动启用 `YOKIFRAME_UNITASK_SUPPORT`，同一组 `SaveAsync()` / `LoadAsync()` 会直接切换为 `UniTask<T>`：

```csharp
using var cts = new CancellationTokenSource();
await SaveKit.SaveAsync(0, data, "Auto", cts.Token);
var loaded = await SaveKit.LoadAsync(0, cts.Token);
```

## 槽位和版本

```csharp
SaveKit.SetMaxSlots(8);
SaveKit.SetCurrentVersion(2);

var slots = SaveKit.GetAllSlots();
var exists = SaveKit.Exists(0);
var meta = SaveKit.GetMeta(0);

SaveKit.Delete(0);
```

版本迁移按相邻版本执行：

```csharp
SaveKit.RegisterMigrator(new PlayerLevelMigrator());
```

如果没有注册某个版本段的 migrator，SaveKit 会跳过该段并继续后续迁移。迁移器应保持纯 C#，不要直接依赖引擎对象。

## 自动保存

自动保存由宿主 Adapter Tick 驱动，不在 SaveKit 内部启动线程或协程：

```csharp
SaveKit.EnableAutoSave(0, data, 5f, () =>
{
    data.SetModule(CapturePlayer());
});

// Unity/Godot Adapter 每帧或固定节奏调用：
SaveKit.TickAutoSave(deltaSeconds);

SaveKit.DisableAutoSave();
```

`TickAutoSave` 返回 `true` 表示本次触发了保存。Unity/Godot Adapter 通过可选 tick delegate 接入，避免运行时程序集硬引用每个 Tools Kit。

### 自动保存状态

| 属性 | 说明 |
|------|------|
| `IsAutoSaveEnabled` | 自动保存是否启用。 |

## Architecture 集成

SaveKit 支持与 YokiFrame Architecture 集成，可以直接从架构中收集数据或恢复数据：

```csharp
// 从架构收集数据到存档
SaveKit.CollectFromArchitecture<GameArchitecture>(data);

// 从存档恢复数据到架构
SaveKit.ApplyToArchitecture<GameArchitecture>(data);
```

## 兼容 API

| 方法 | 说明 |
|------|------|
| `SetSavePath(path)` | 设置存档目录路径。 |
| `GetSavePath()` | 获取存档目录路径。 |
| `SetFileFormat(prefix, extension)` | 设置文件名前缀和扩展名。 |
| `GetFileFormat()` | 获取文件名前缀和扩展名。 |
| `Reset()` | 重置 SaveKit 状态。 |

## 命令桥

SaveKit 已接入文件命令桥。AI、Tauri 和脚本优先使用 engine-scoped v2 路径：

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "requestId": "save-001",
  "kit": "SaveKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

常用 action：

| action | payload | 说明 |
|--------|---------|------|
| `get_workbench_snapshot` | `{}` | 工作台、AI 和 snapshot publisher 共用的完整当前状态。 |
| `stats` | `{}` | 当前版本、最大槽位、槽位数量、后端类型和加密状态。 |
| `list_slots` | `{}` | 只读槽位元数据列表。 |
| `delete_slot` | `{ "slotId": 0 }` | 删除指定槽位。 |
| `disable_auto_save` | `{}` | 关闭自动保存。 |

命令桥不提供保存/读档 payload action。存档内容属于项目业务数据，不能通过 `.yokiframe` 文件桥随意暴露给 AI 或编辑器。

## Tauri 工作台

SaveKit 页面读取顺序为：

1. `read_telemetry("SaveKit", "state")`
2. `read_snapshot("SaveKit", "state")`
3. `send_command("SaveKit", "get_workbench_snapshot")`

Unity `KitStateSnapshotPublisher` 和 Godot `GodotKitStateSnapshotPublisher` 都通过可选 handler 发布 `SaveKit/state`。页面只在缺少 telemetry/snapshot、用户点击刷新、删除槽位或关闭自动保存时走命令桥，避免用高频命令轮询工作台。

## AI 查询建议

AI 默认先读：

```text
.yokiframe/engines/<engineId>/snapshots/SaveKit/state.json
```

snapshot 缺失、过期或需要显式维护动作时，再发送 `SaveKit/get_workbench_snapshot`、`SaveKit/list_slots`、`SaveKit/delete_slot` 或 `SaveKit/disable_auto_save`。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| Tauri 页面没有槽位 | 确认引擎在线，再检查 `SaveKit/state` snapshot；必要时发送 `get_workbench_snapshot`。 |
| Unity/Godot 路径不同 | 路径差异应在 Adapter 的 `ISaveStorage` 后端处理，业务仍使用 `SaveKit.Save()`。 |
| 加密状态不一致 | 检查启动时是否调用 `SetEncryptor`，旧存档需要保持同一解密策略。 |
| 自动保存不触发 | 确认宿主 Adapter 正在调用 `TickAutoSave(deltaSeconds)`。 |
