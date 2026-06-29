# SaveKit 存档

## 配置后端

Unity / Godot 通常由统一初始化安装默认后端：

```csharp
using YokiFrame;

YokiFrameKit.Initialize(YokiFrameEngine.Unity);
```

项目自定义：

```csharp
SaveKit.SetStorage(new ProjectSaveStorage());
SaveKit.SetSerializer(new ProjectSaveSerializer());
SaveKit.SetEncryptor(new AesSaveEncryptor(projectSaveSecret));
```

不要把 Unity `Application.persistentDataPath` 或 Godot API 写进 Tool 层；路径差异放 `ISaveStorage`。

## 保存和读取

```csharp
var data = SaveKit.CreateSaveData();
data.SetModule(new PlayerSaveModule { Level = 3 });

SaveKit.Save(0, data, "Boss");

var loaded = SaveKit.Load(0);
var player = loaded.GetModule<PlayerSaveModule>();
```

异步 API 默认返回 `Task<T>`；启用 `YOKIFRAME_UNITASK_SUPPORT` 后返回 `UniTask<T>`。

```csharp
await SaveKit.SaveAsync(0, data, "Auto", token);
var loaded = await SaveKit.LoadAsync(0, token);
```

## 槽位和版本

```csharp
SaveKit.SetMaxSlots(8);
SaveKit.SetCurrentVersion(2);

var slots = SaveKit.GetAllSlots();
bool exists = SaveKit.Exists(0);
SaveMeta meta = SaveKit.GetMeta(0);

SaveKit.Delete(0);
```

版本迁移：

```csharp
SaveKit.RegisterMigrator(new PlayerLevelMigrator());
```

迁移器保持纯 C#，不要依赖引擎对象。

## 自动保存

```csharp
SaveKit.EnableAutoSave(0, data, 5f, () =>
{
    data.SetModule(CapturePlayer());
});

SaveKit.TickAutoSave(deltaSeconds);
SaveKit.DisableAutoSave();
```

自动保存由宿主 Adapter tick 驱动，SaveKit 不启动线程或协程。

## 工作台诊断

SaveKit 页面用于查看后端、版本、最大槽位、槽位列表、自动保存状态和加密状态。

| 在工作台里看什么 | 用途 |
|---|---|
| Backend / Storage | 确认存档写到哪个后端。 |
| Version | 判断读档失败是否可能来自版本迁移。 |
| Slots | 查看槽位是否存在、时间戳和元数据是否正确。 |
| Auto Save | 确认自动保存是否开启、间隔是否正常。 |
| Encryption | 判断当前是否启用加密。 |

工作台只展示槽位元数据，不展示完整存档内容。删除槽位、关闭自动保存属于维护操作，只在明确需要时执行。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 没有槽位 | 确认后端配置、保存路径和 `SaveKit.Save()` 是否成功。 |
| 自动保存不触发 | 确认宿主正在调用 `TickAutoSave(deltaSeconds)`。 |
| 加密后旧档读不了 | 保持同一解密策略，或写迁移流程。 |
| 删除槽位误操作 | `delete_slot` 是变更命令，只在明确确认后执行。 |
