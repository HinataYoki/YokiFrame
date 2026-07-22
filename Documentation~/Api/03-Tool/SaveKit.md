# SaveKit 存档

> 面向读者：需要持久化玩家进度、Global 设置或可迁移本地数据的 Runtime 开发者
>
> 主要入口：`SaveKit`、`SaveTarget`、`SaveData`
>
> 运行边界：跨宿主 Runtime；Workbench 只浏览文件元信息，不读取 payload
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

SaveKit 是跨宿主的纯 C# 存档 Tool Kit，用于运行时持久化玩家进度与槽位、与槽位无关的 Global 设置，以及需要版本迁移、可替换 Serializer/Storage 或认证加密的本地存档。

SaveKit 负责目标寻址、模块化数据容器、文件容器校验和后端组合。它不负责云同步、跨设备冲突合并、账号系统或防作弊，也不会把真实存档 payload 写入 Workbench、CLI 或诊断协议。

存档语义必须通过 `SaveTarget` 表达：玩家可选存档用 `Slot`，设置与其它独立文档用 `Global`。不要用负数或特殊槽位编号模拟 Global。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现：Slot/Global、JSON、Task/UniTask 异步 API、自动保存、Architecture 集成；源码 `Tools/SaveKit` |
| 程序集 | `YokiFrame.SaveKit`（无引擎引用）；Adapter / Nino 见下文 |
| Interaction | 已实现：`SaveKit/state`、`stats`、`get_workbench_snapshot`，全部只读 |
| Workbench | 已实现：存档目录/扩展名配置、文件元信息与 Runtime 后端/自动保存/容器头摘要 |
| 状态入口 | Editor/Tools 内部版本只驱动低频 FileBridge Snapshot 增量写入；SaveKit 不发布 Shared Memory Telemetry，纯观察不创建默认后端 |

## 快速上手

### 定义模块并保存槽位

模块是 `SaveData` 中的强类型数据单元。注册时可传显式模块 ID；省略时使用 `typeof(T).FullName`：

```csharp
using YokiFrame;

public sealed class PlayerSaveModule
{
    public int Level;
    public int Experience;
}

var data = SaveKit.CreateSaveData();
data.RegisterModule(new PlayerSaveModule
{
    Level = 3,
    Experience = 120
}, "game.player");

SaveKit.Save(SaveTarget.Slot(0), data, "Chapter 1");
```

`Save` 返回 `true` 表示 Storage 已接受完整容器字节。已有目标保留创建时间并更新最近保存时间；非空 `displayName` 会更新显示名称。

### 读取并处理失败

```csharp
SaveLoadResult result = SaveKit.TryLoad(SaveTarget.Slot(0));
if (!result.Succeeded)
{
    LogKit.Warning("Load failed: " + result.Status + ", " + result.Error);
    return;
}

PlayerSaveModule player = result.Data.GetModule<PlayerSaveModule>("game.player");
```

`Load` 是简化入口，失败返回 `null`。需要区分“没有存档”和“存档坏了”时始终用 `TryLoad`。

### Global 文档

```csharp
public sealed class GameSettings
{
    public string Language;
    public float MasterVolume;
}

var settings = SaveKit.CreateSaveData();
settings.RegisterModule(new GameSettings
{
    Language = "zh-CN",
    MasterVolume = 0.8f
});

SaveKit.Save(SaveTarget.Global("settings"), settings);
GameSettings loaded = SaveKit.Load(SaveTarget.Global("settings"))
    ?.GetModule<GameSettings>();
```

Global key 只能包含字母、数字、`-`、`_` 和 `.`，长度 1～64。

### 异步与自动保存

```csharp
await SaveKit.SaveAsync(SaveTarget.Slot(0), data, "Checkpoint", cancellationToken);
SaveData loaded = await SaveKit.LoadAsync(SaveTarget.Slot(0), cancellationToken);

SaveKit.EnableAutoSave(SaveTarget.Slot(0), data, 5f, () =>
{
    data.RegisterModule(CaptureCurrentPlayer());
});
// 在宿主 Update / Process 或业务时钟中调用
SaveKit.TickAutoSave(deltaSeconds);
```

异步 API 当前复用同步 Storage/Serializer，不保证切到线程池。自动保存只绑定一个 Slot；再次启用会替换旧绑定。

## 核心 API

### `SaveTarget` 寻址

| API | 说明 |
|---|---|
| `SaveTarget.Slot(int slotId)` | 创建非负数字槽位。无预设槽位上限；负槽位在入口拒绝。 |
| `SaveTarget.Global(string key)` | 创建命名 Global 文档。key 限 1～64 个安全字符（字母、数字、`-` `_` `.`）；非法 key 抛参数异常。 |
| `Kind` | 返回 `SaveTargetKind`（`Slot` 或 `Global`）。 |
| `SlotId` | 槽位编号；Global 目标返回 `-1`。 |
| `GlobalKey` | Global 名称；Slot 目标返回 `null`。 |
| `Name` | 槽位的十进制名称或 Global key，用于元数据与排序。 |
| `IsSlot` / `IsGlobal` | 判断目标类型。 |
| `Equals` / `==` / `!=` | 按类型、槽位编号和 Global key 比较。 |
| `ToString()` | 形如 `Slot(0)` 或 `Global(settings)`。 |

`GetAllSlots()` 与 `GetAllGlobals()` 以 Storage 中实际存在且头部可解析的目标为准，不以配置上限为准。

### `SaveData` 模块容器

`SaveData` 同时支持刚注册的模块对象引用，以及加载后尚未解码的原始 payload。首次 `GetModule<T>()` 才解码并缓存。

| API | 说明 |
|---|---|
| `ModuleCount` | 当前模块数量；对象引用与原始 payload 按稳定 ID 去重。 |
| `SetSerializer(ISaveSerializer)` | 设置 `GetModule<T>()` 解码用序列化器；`null` 抛 `ArgumentNullException`。 |
| `GetSerializer()` | 读取容器当前序列化器。 |
| `RegisterModule<T>(T data, string moduleId = null)` | 注册或替换模块。ID 为空时用 `typeof(T).FullName`；显式 ID 须非空且不超过 256 字符。 |
| `RemoveModule<T>(string moduleId = null)` | 删除指定模块；实际删除返回 `true`。 |
| `GetModule<T>(string moduleId = null)` | 获取模块；不存在返回 `null`，原始 payload 在首次读取时解码。 |
| `HasModule<T>(string moduleId = null)` | 判断指定模块是否存在。 |
| `Clear()` | 删除容器中全部模块。 |

注意：

- `Save` 总是使用当前 `SaveKit.GetSerializer()` 生成容器 payload。
- 正常路径用 `CreateSaveData()` 创建容器；切换后端后应重新创建或明确设置容器序列化器。
- 同一模块 ID 重注册会替换旧模块；读取自定义 ID 时传相同 ID。
- 不要用 `string.GetHashCode()` 当存档身份。

### `SaveKit` 配置

| API | 说明 |
|---|---|
| `SetSerializer` / `GetSerializer` | 模块序列化器；设为 `null` 抛 `ArgumentNullException`。 |
| `SetEncryptor` / `GetEncryptor` | payload 加密器；`null` 表示关闭加密。 |
| `SetStorage` / `GetStorage` | 容器存储后端；设为 `null` 抛 `ArgumentNullException`。 |
| `RegisterDefaultBackendFactory(...)` | 注册宿主惰性默认工厂；首次业务调用才实例化。 |
| `CreateSaveData()` | 创建并绑定当前 Serializer 的 `SaveData`。 |
| `Reset()` | 停用自动保存并清除当前后端。不删除外部文件根目录内容；下次业务调用重回默认工厂，无工厂则 raw + memory。 |

切换 Serializer 不会迁移已有目标。保存其它 Serializer 写出的目标会抛 `InvalidOperationException`；`TryLoad` 用容器头 `SerializerId` 与当前后端比较。

### 保存、读取与删除

| API | 说明 |
|---|---|
| `Save(SaveTarget, SaveData, string displayName = null)` | 序列化、可选加密并写入完整容器。同步；成功返回 `true`；Storage 失败抛异常，不伪装成功。 |
| `Save(int slotId, ...)` | Slot 保存便捷重载，语义同 `Save(SaveTarget.Slot(...), ...)`。 |
| `TryLoad(SaveTarget)` / `TryLoad(int)` | 结构化读取，返回 `SaveLoadResult`。需要区分失败原因时的推荐主路径。 |
| `Load(SaveTarget)` / `Load(int)` | 简化读取；成功返回 `SaveData`，失败返回 `null`（丢失失败分类）。 |
| `Exists(SaveTarget)` / `Exists(int)` | 是否有可解析的容器头。**不等价**于 payload 已成功解码。 |
| `Delete(SaveTarget)` / `Delete(int)` | 删除目标；实际删除返回 `true`。 |
| `GetMeta` / `GetAllSlots` / `GetAllGlobals` | 列表 UI 元数据，只读头部。缺失或无效头部返回默认 `SaveMeta`。 |

`Save` 流程：序列化模块表 → 可选加密 payload → 生成头部 → 拼接完整容器 → Storage 写入。

### `SaveLoadResult` 与错误状态

| 状态 | 含义 |
|---|---|
| `Success` | `Data` 可用 |
| `Missing` | Storage 中不存在目标 |
| `Invalid` | 头部、模块表、payload 或加密认证失败 |
| `SerializerMismatch` | 头部 `SerializerId` 与当前后端不一致 |
| `MigrationFailed` | Serializer 迁移链失败 |
| `Unsupported` | 当前 Serializer 不支持该 payload |

| 属性 | 说明 |
|---|---|
| `Status` | `SaveLoadStatus` 状态 |
| `Succeeded` | 等价于 `Status == Success` |
| `Data` | 成功时的 `SaveData`，失败为 `null` |
| `Meta` | 能解析头部时的 `SaveMeta` |
| `Error` | 失败诊断消息，成功为 `null` |

容器读取严格拒绝截断、重复模块 ID、超长字段、payload 长度不匹配和 trailing bytes。

### 异步 API

定义 `YOKIFRAME_UNITASK_SUPPORT` 时异步返回 `UniTask`，否则返回 `Task`；参数与行为一致。

| API | 说明 |
|---|---|
| `SaveAsync(...)` | 异步签名保存，返回 `bool`。提供 Target / Slot 与有无 `displayName` 的重载。 |
| `LoadAsync(...)` | 异步读取；失败返回 `null`。 |
| `TryLoadAsync(...)` | 异步结构化读取，返回 `SaveLoadResult`。 |

当前实现在同步 IO 前后检查取消令牌，不强制把文件 IO 放到线程池。若同步写入完成后才收到取消，调用报告取消，但已提交文件不回滚。取消、参数错误和 Storage 异常仍按异步约定抛出。

### 自动保存

不启动线程、协程或引擎计时器；由宿主或业务时钟驱动。

| API | 说明 |
|---|---|
| `EnableAutoSave(SaveTarget, SaveData, float, Action onBeforeSave = null)` | 绑定自动保存。**仅 Slot**；间隔须为正有限数；Global 抛参数异常。再次启用会替换旧绑定。 |
| `EnableAutoSave(int, ...)` | Slot 便捷重载。 |
| `DisableAutoSave()` | 停用并清空自动保存状态。 |
| `IsAutoSaveEnabled` | 当前是否启用。 |
| `GetAutoSaveTarget()` | 当前目标；未启用时为默认值。 |
| `GetAutoSaveIntervalSeconds()` / `GetAutoSaveElapsedSeconds()` | 当前间隔与已累计时间。 |
| `TickAutoSave(float deltaSeconds)` | 推进计时。`deltaSeconds` 须为非负有限数；触发并成功保存返回 `true`；未触发保存不会触发 Interaction Snapshot 重写。 |

达到间隔后先调用 `onBeforeSave`，再执行同步 `Save`；回调抛异常则不进入保存。一次 tick 跨过多间隔时只保存一次，剩余时间按间隔取模。

### Architecture 集成

```csharp
var data = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(data);
SaveKit.Save(SaveTarget.Slot(0), data);

var result = SaveKit.TryLoad(SaveTarget.Slot(0));
if (result.Succeeded)
    SaveKit.ApplyToArchitecture<GameArchitecture>(result.Data);
```

`CollectFromArchitecture<T>` 遍历已注册服务中的 `IModel`，按具体运行时类型计算模块 ID。`ApplyToArchitecture<T>` 只覆盖**已存在**的同 ID 模型（`DeserializeOverwrite`），不会自动创建或注册服务。调用前须完成 Architecture 初始化与模型注册；不改变 Slot/Global、Serializer、迁移和加密规则。

### `SaveMeta` 与容器格式

`SaveMeta` 只描述头部，不含真实模块 payload。

| 成员 / API | 说明 |
|---|---|
| `HeaderVersion` / `ContainerVersion` | 当前均为 1。 |
| `Target` / `DisplayName` / `SerializerId` | 目标、显示名、模块 Serializer 稳定 ID。 |
| `CreatedTimestamp` / `LastSavedTimestamp` | Unix 秒级时间戳。 |
| `SaveMeta.Create(...)` | 创建带当前 UTC Unix 秒时间戳的元数据。 |
| `UpdateSaveTime()` | 只更新最近保存时间。 |
| `SerializeHeader(int payloadLength)` | 严格生成头部字节；payload 长度最大 64 MiB。 |
| `TryDeserializeHeader(...)` | 支持完整 `byte[]` 或仅头部 `Stream + containerLength`；均校验 magic、版本、长度、目标和完整容器长度，trailing bytes 返回 `false`。 |
| `GetCreatedDateTime()` / `GetLastSavedDateTime()` | Unix 时间转本地 `DateTime`。 |

容器 = 头部 + payload。payload 为按稳定 ID 排序写出的模块表。模块数上限 10000；单模块 ID 编码最大 1024 字节；单模块 payload 最大 64 MiB。

### Storage 契约与内置实现

| API | 说明 |
|---|---|
| `ISaveStorage.Exists` / `Write` / `Read` / `Delete` | 操作完整容器字节，不是裸模块 payload。自定义后端自定并发与原子策略。 |
| `ISaveMetadataStorage.TryReadMetadata` | 可选的只读头部契约。实现后 Interaction 可读取安全元数据而不读取 payload；内置 Memory/File Storage 已实现。 |
| `GetTargets(SaveTargetKind)` | 枚举指定类型目标快照。 |
| `Clear(SaveTargetKind)` | 清空指定类型全部目标。 |

**`MemorySaveStorage`**：适合单元测试与临时隔离。读写都会复制 `byte[]`；进程结束或替换后端后数据丢失。

**`FileSaveStorage`**：

```csharp
var storage = new FileSaveStorage("D:/Game/Saves");
// 或自定义扩展名：new FileSaveStorage(rootPath, ".save")
SaveKit.SetStorage(storage);
```

公开属性 `RootPath`（规范化绝对根目录）与 `FileExtension`（默认 `.yoki`）。文件布局：

```text
<RootPath>/
├── slots/save_0.yoki
└── global/settings.yoki
```

写入先写同目录临时文件并 `Flush(true)`，再 Replace/Move 提交，避免目标文件停留在半写状态。调用方仍应避免对同一目标并行写入，除非自定义后端明确支持。

### Serializer、迁移与加密

| 契约 / 类型 | 说明 |
|---|---|
| `ISaveSerializer` | `SerializerId`、`Serialize`/`Deserialize`、`ValidatePayload`、`DeserializeOverwrite`。JSON 实现另支持 `IModuleIdAwareSaveSerializer`，以便按显式 moduleId 找迁移器。 |
| Core `raw` | `SerializerId` 为 `raw`。只复制 `byte[]`，不能直接强类型编解码。 |
| `JsonSaveSerializer` | `SerializerId` 为 `json`。模块 JSON 前 4 字节为 schema 版本；按 moduleId 逐步迁移到 `CurrentSchemaVersion`；缺步 / null / 版本倒退失败。 |
| `IJsonSaveCodec` | Unity：`UnityJsonSaveCodec`（JsonUtility）；Godot：`GodotJsonSaveCodec`（System.Text.Json，字段 + 大小写不敏感）。 |
| `IJsonSaveMigrator` | 要求 `ToVersion == FromVersion + 1`；只处理 UTF-8 JSON；确定性、不依赖引擎对象。 |
| `ISaveEncryptor` | `EncryptorId`、`Encrypt`/`Decrypt`；认证失败应抛异常。 |
| `AesCbcHmacSaveEncryptor` | PBKDF2-SHA256 + AES-CBC + HMAC-SHA256。密码非空；错误/篡改 → `TryLoad` 的 `Invalid`。**不是防作弊**。 |

迁移成功只表示当前读取可用。要把旧存档物理升级，应读取各模块并重新注册/保存；仅 `TryLoad` 后立刻 `Save` 仍可能保留未解码原始 payload。

切换加密或密码前：用旧配置读出 → 设新配置 → 重新保存。开启加密后不能直接读未加密文件，关闭后也不能直接读加密文件。项目必须负责密钥存放与轮换，不能把固定密码暴露在客户端可逆配置中。

## 生命周期与错误边界

1. Unity/Godot 完成 Adapter 默认后端安装后再创建 `SaveData` 与读写；自定义宿主先设 Serializer、Storage 与可选 Encryptor。
2. `CreateSaveData()` 绑定创建时的当前 Serializer；同一批 typed 容器未处理完前不要无规划地切换全局 Serializer。
3. `Save` / `Delete` / 配置 / 自动保存状态是同步操作；`Async` 仅是签名与取消检查，不代表后台线程执行。
4. `Exists` 只代表可解析头部；列表 UI 用 `GetMeta` / `GetAllSlots` / `GetAllGlobals` 后，对选中目标仍建议 `TryLoad`。
5. `Reset()` 丢弃当前内存后端引用并恢复 Core 默认，不删除外部 `FileSaveStorage` 根目录文件。
6. 更换根目录、扩展名、Serializer 或 Encryptor 是配置切换，不是数据迁移；迁移由项目显式编排。
7. File Storage 单次提交具备临时文件原子替换；应用崩溃后的业务级回滚不由 SaveKit 代劳。

## 宿主与工具入口

SaveKit Provider 位于 `Tools/SaveKit/Editor`，Unity Editor Adapter 仅负责加载时注册。`SaveKit/state` 与两个 command 都只发布当前已存在后端的安全事实；SaveKit 只走 FileBridge Snapshot，不发布 Shared Memory Telemetry。纯观察**不会**调用 `EnsureBackend()`，因而不会创建默认 Storage 或 Serializer。

- `state`：后端是否已配置、Storage 类型、Serializer/Encryptor 标识、自动保存摘要，以及最多 32 个 Slot 和 32 个 Global 的已验证容器头。
- `stats`：不含列表的后端、自动保存和容器数量摘要。
- `get_workbench_snapshot`：返回完整有界 `state`；两项命令均为 `ReadOnly`。
- 状态只通过 `ISaveMetadataStorage` 读取头部；未知 Storage 仍保留数量但不读取、解析或传输模块 payload，不提供远程 Save、Load、Delete、Clear、后端切换或通用迁移 action。
- Avalonia 页面继续配置目录/扩展名并扫描物理文件元信息；Runtime 区域通过 Application 强类型 read model 显示当前后端、自动保存和容器头覆盖率。

- Unity 配置：`Assets/Settings/Resources/YokiFrame/runtime-settings.json` 的 `SaveKit/storagePath` 与 `SaveKit/fileExtension`
- Godot 配置：`project.godot` 的 `yokiframe/runtime/save_kit/storage_path` 与 `yokiframe/runtime/save_kit/file_extension`

通用存档迁移工具仍未实现；不要根据只读 Interaction 推断其可以修改文件或后端。内部 Snapshot 版本只在配置、保存、删除和自动保存状态变化时推进，普通计时 Tick 不会重写 FileBridge state；该版本不是 Runtime 公共 API，也不会创建 Telemetry。

### 宿主 Adapter 与可选 Integration

| 程序集/项目 | 内容 | 宿主边界 |
|---|---|---|
| `YokiFrame.SaveKit` | 门面、容器、Storage/Serializer/Encryptor 契约、JSON 迁移、AES | 无引擎引用 |
| `YokiFrame.SaveKit.Unity` | `UnityJsonSaveCodec`、默认后端安装器 | Unity 2022.3+；API 仅在 Adapter |
| `YokiFrame.SaveKit.Godot` | `GodotJsonSaveCodec`、由 Installer 薄 Bootstrap 显式加载的惰性默认后端安装器 | Godot .NET |
| `YokiFrame.SaveKit.Nino` | 可选 `NinoSaveSerializer` | `UNITY_2022_3_OR_NEWER && YOKIFRAME_NINO_SUPPORT` |

| 配置 | Core 默认 | Unity/Godot Adapter 安装后 |
|---|---|---|
| Serializer | `RawBytesSaveSerializer`（`raw`） | `JsonSaveSerializer`（`json`） |
| Storage | `MemorySaveStorage` | `FileSaveStorage` |
| Encryptor | 无 | 无，需项目显式配置 |
| 文件根 | — | Unity：`Application.persistentDataPath/YokiFrame/Saves`；Godot：`OS.GetUserDataDir()/YokiFrame/Saves` |

Adapter 只注册惰性默认工厂；首次业务调用才创建 JSON + 文件后端。仅引用 Core 且无工厂时回退 raw + memory。

Nino 显式切换示例：

```csharp
#if UNITY_2022_3_OR_NEWER && YOKIFRAME_NINO_SUPPORT
SaveKit.SetSerializer(new NinoSaveSerializer());
#endif
```

`SerializerId` 为 `nino`。版本与迁移由 Nino 负责；SaveKit 不会自动把 JSON 存档转成 Nino。

## 限制与相关资料

- 用 `SaveTarget.Slot` / `Global` 表达语义，禁止魔法槽位号模拟 Global。
- 需要失败分类时用 `TryLoad`，不要依赖 `Load` / `Exists` 推断 payload 健康。
- 切换 Serializer/Encryptor/根目录不会自动迁移文件；必须读出再按新配置写回。
- 加密不是防作弊；不要在客户端放可逆固定密钥当安全方案。
- 自动保存只服务单个 Slot，且必须由宿主 `TickAutoSave`。
- 不提供：云同步、冲突合并、账号、防作弊、Workbench/CLI 读 payload、远程存档修改或通用迁移工具。

Core 测试覆盖目标验证、容器截断/trailing bytes、模块 ID、Memory/File Storage、JSON 迁移、加密认证、自动保存和 Architecture 集成。Unity/Nino 测试需在对应依赖与宏开启时执行。

## 维护

| 项 | 值 |
|---|---|
| 源码根 | `Tools/SaveKit`（Runtime / Adapters / Integrations / Tests） |
| Workbench | `YokiFrameWorkbench~` 中 SaveKit Application 与 Avalonia 页面 |
| 状态入口 | `SaveKit/state` + `stats` / `get_workbench_snapshot`；只发布安全摘要 |
| 改 API 时同步 | 本文、`kit-index.md`、`yokiframe` Skill；Workbench 设置字段变更时同步 Workbench Skill |
