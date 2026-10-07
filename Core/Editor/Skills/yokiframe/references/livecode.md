# LiveCode：行为原型、零编译调参与显式恢复

> **实现范围**：Unity Play 行为及 Godot 4.7 .NET/Tools editor/runtime 行为、批量、调参、恢复已验证。Patch 与 Export/Bind 仅 Unity；Godot 不支持 Capture。Player/IL2CPP 不支持。宿主侧契约见包根 `Documentation~/Guides/Engine-LiveCode-Contract.md`；Godot 差异见本页末节。
>
> 需要「先让用户在运行态里调手感，满意后再落盘成 Mono」时用这一页。只想读状态、截图、跑断言，用 [roslyn-kit.md](roslyn-kit.md) 的 `yoki script`，不要起 LiveCode。

## 给 Agent 的硬边界

Play Mode 里改行为、调数值、截图和断言，默认走内存编译，**不要**为了试一次就改 `Assets` 下的 `.cs`。下面这些是当前实现做不到的，撞上就停，不要换一种写法再试。

**硬限制，不能靠重试绕过：**

- 不能把方法插进已经存在的类。`Attach` 是新的临时行为，`Patch` 只是包住旧方法。要改 `Player.cs` 的源码，这是正式编译，先告诉用户。
- 内存程序集不卸载。`Remove` 只停用，`loadedAssemblies` 不减。同 ID 重挂也再占一次。预算见 `script_status`，耗尽后停止新 Attach / 新脚本，不要连续重试。
- 没有协程、`enabled`、运行时 `AddComponent<原型>`、Inspector 编辑。碰撞/触发回调和 `Delay` 已支持，签名见下文；需要协程或正式组件生命周期时写真实代码。
- 两个原型不能互引类型，也不能 `SendMessage`。只能 `CallLive` / `Invoke`，参数类型必须完全一致。
- 标量、枚举、向量、颜色、对象引用可调。数组、List、嵌套对象要按当前字段契约走；属性和 `SerializeReference` 仍然改不了。
- Patch 打中该方法的所有调用，不限单个对象；异常不会自动撤销补丁。
- 取消和超时停不了不加检查的死循环。失败不回滚 `Awake` 已经造成的加分、生成物体等副作用。
- Player、IL2CPP、手机包没有 LiveCode。Godot 没有 Patch、Export、Bind、Capture。

**必须编译进真实代码时，先告诉用户，再改文件：**

- `Export` / `CommitExport` / `Reexport` 会写 `Assets/.../*.cs` 并 `AssetDatabase.Refresh()`，随后 Unity 编译，Play 现场会丢。
- `Bind` 只在 Edit Mode，会给场景加组件并保存该场景。
- 改已有业务 `.cs`、asmdef、包依赖，都会触发 Unity 资产编译。这不属于本功能的零编译路径。
- 第一次安装 Roslyn / Harmony 包本身也要编译一次。装完之后的 `yoki script` 和 `Attach` 才不触发 Unity 编译。

## 什么时候用

| 场景 | 用什么 |
|---|---|
| 高频改数值 | `live_set_fields` 或文件绑定，零编译；不必每次提交脚本 |
| 读状态、截图、断言 | 同次提交内复用 ReadField；`yoki script` 缓存未命中才加载新程序集，Godot Capture 不支持 |
| 在 Play 中新增/覆盖行为，边跑边调参 | LiveCode `Attach` + `SetField` |
| 拦截已有 C# 方法 | LiveCode `Patch`（需 HarmonyX bundle） |
| 把调好的行为变成永久 MonoBehaviour | 批量 `ExportMany` → `live_export_commit` → 编译 → `Bind`；后续 `Reexport` |
| 只是想改业务逻辑 | 写业务代码。LiveCode 是原型/调参通道，不是交付形态 |

## API（`yoki script` 方法体内）

| 调用 | 语义 |
|---|---|
| `RoslynKit/LiveCode.Patch` | 用完整 static patch 类拦截已有方法；`replace` 是 bool prefix |
| `engine.LiveCode.Attach(id, target, className, members)` | 把成员源码编译成临时行为挂到 **Play 场景对象**上 |
| `await engine.LiveCode.AttachMany(requests)` | 1..64 个 `LiveAttachmentRequest(id,target,className,members)`，整批预检/准备后激活，返回逐项结果 |
| `engine.LiveCode.ReadField(id, name)` / `SetField(id, name, value)` | 读写**已声明的 public / SerializeField** 字段 |
| `engine.LiveCode.Invoke(id, method, args...)` | 按 ID 调用原型声明的 public 实例方法，不要求引用另一个原型类型 |
| `await engine.LiveCode.Export(id, "Assets/.../X.cs")` | 单项暂存并安排提交；返回 exportId，后续导入可重载 |
| `await engine.LiveCode.ExportMany(requests, batchId = null)` | 暂存 1..64 个 `LiveExportRequest(id,path)`；最多 16 份脚本，不写 Assets；返回 Prepared 批次 |
| `await engine.LiveCode.Reexport(previousExportId, members, batchId = null)` | 同类/同路径暂存新版本，EditMode 无 handle 也可用；不直接提交 |
| `engine.LiveCode.CommitExport(batchId)` / `RoslynKit/live_export_commit` | 显式提交批次，统一请求导入；CLI payload 为 batchId/target/confirmed:true |
| `RoslynKit/live_export_status` | 只读，传 batchId 或 exportId 二选一；Committed 不等于 compiled |
| `engine.LiveCode.Bind(exportId, target = null)` | EditMode 一个版本绑定多个目标；多目标版本必须显式传 target；重复绑定不重置现有字段 |
| `engine.LiveCode.List()` / `Remove(id)` | 诊断与逐个移除 |
| `RoslynKit/live_status` / `RoslynKit/live_remove` | 诊断 handle / 按 `{"id":"..."}` 移除，执行开关关闭时仍可用 |
| `RoslynKit/live_snapshot` | `{"target":"play"}`，不加载程序集，写受控快照文件，返回 snapshotId/path/complete/逐项错误 |
| `engine.LiveCode.Snapshot()` / `ReadSnapshot(snapshotId)` | 保存并返回记录 / 读取记录；Snapshot 返回对象，不是 JSON 字符串 |
| `await engine.LiveCode.Restore(snapshotId, sourceProvider, resolver)` | 核对源码 hash、对象、预算，恢复所有字段后才激活；resolver 可省略，但临时引用必需 |

- `Export` 的签名是 **两个参数**：`Export(id, outputPath)`。不要再传 className/source，会得到 `CS1501`。
- 每次提交仍需 `--confirm-execution`，并依赖 [roslyn-kit.md](roslyn-kit.md) 的两个开关。

## 成员源码怎么写（最容易踩的地方）

`Attach` 的 `members` 是**类成员**，不是完整类。宿主会把它放进生成的类型里，因此：

- **不要写 `using`**：用了会得到 `CS1529` / `CS1001`。全部写全限定名（`UnityEngine.Vector3`、`System.Collections.Generic.List`）。
- **不要再包装一层完整同名 `class`**；类成员方法内部仍可使用正常 C# 局部函数。
- 生成作用域里**没有 `engine`**：`engine.ConsoleLog` 会 `CS0103`。直接日志可用 `UnityEngine.Debug.Log`，需要运行报告关联时由提交脚本读字段后 ConsoleLog。
- 普通成员方法可以重载；同一作用域的局部函数不能同名。跨原型 Invoke 若多个重载匹配则拒绝，使用唯一方法名或预编译共享接口。
- 生命周期回调只支持非泛型、无参、返回 `void`：`Awake`、`OnEnable`、`Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnDisable`、`OnDestroy`。
- 碰撞与触发器回调同样只按方法名转发，签名固定：`OnCollisionEnter/Stay/Exit(UnityEngine.Collision)`、`OnTriggerEnter/Stay/Exit(UnityEngine.Collider)`、`OnControllerColliderHit(UnityEngine.ControllerColliderHit)`。**宿主必须在编译期实现这些消息，Unity 才会回调**；原型没声明对应方法时委托为空，直接返回。签名写错会在 `Attach` 阶段被拒绝，不会变成运行时静默不触发。目标对象仍需自己具备 Collider / Rigidbody —— 原型不会替它添加物理组件。
- facade 提供 gameObject、transform、GetComponent/GetComponents、GetComponent(s)InChildren/Parent、Instantiate、Destroy；复数查询支持 includeInactive 和 List 输出，不必自己递归层级。
- 延迟调用用 `Delay(float seconds, Action callback)`，返回句柄可用 `CancelDelay(handle)` 取消；宿主停止时统一停止，回调异常按原型故障处理（与 `Update` 同一套）。它不是 MonoBehaviour 的 `Invoke` 重载，语义等价。
- `enabled` / 运行时 `AddComponent<原型>` / Inspector 编辑 / UnityEvent 目标仍然**没有**，因为原型不是 `Component`。
- 每个 id 单独编译成一个程序集：**两个原型之间不能互相引用类型**（`CS0246`）。

### 原型之间怎么互相调用

facade 是普通类（`YokiFrame.UnityLiveBehaviour`），位于 Editor-only 程序集 `YokiFrame.Unity.LiveCode.Facade`。内存编译只引用这个外观和运行时程序集，不引用 `YokiFrame.Unity.Editor`。**Unity 原生 SendMessage / SendMessageUpwards / BroadcastMessage 不转发到原型**。使用显式按 ID 调用：

```csharp
// 原型方法内；目标方法须声明为 public。
CallLive("TargetPrototype", "OnHit", damage);
// 外部受信任脚本内。
engine.LiveCode.Invoke("TargetPrototype", "OnHit", damage);
```

每次按 ID 找当前版本，重挂自动转向新实例，不缓存旧 Instance/MethodInfo。
同对象可挂不同 ID 的 host，Id/Instance 只读；不要按 GetComponents 的第一个猜目标。
仅支持原型声明的 public 实例方法，参数精确匹配、可选参数显式传齐，
歧义重载/private/开放泛型/ref/out/pointer 被拒。Task 返回需调用方转换并 await。
CallLive 是原型专用 API，导出前须改为正式组件的共享接口或事件通信。

### 字段可读性

`ReadField` / `SetField` 只接受**已声明的 public 或 `[SerializeField]`** 字段，私有状态会报
`Only declared public or SerializeField fields can be tuned: <name>`。

所以调参用的私有状态（当前散布、后坐力累积、冷却时间）要**镜像到 public 字段**，在 `Update` 末尾同步一次：

```csharp
public float Spread;   // 只读镜像，供调参脚本读
private float mSpread;
private void Update() { /* ... */ Spread = mSpread; }
```

调参脚本读镜像、只 `SetField` 真正可调的参数，不要让镜像和真实状态双向写。

### 重挂（reattach）会让 `Awake` 重跑

复用同一 id 会迁移支持的字段并**重新执行 `Awake`**。如果 `Awake` 里记录了"当前位置=巡逻中心"，那么目标半路被重挂时会把漂移中的位置记成新中心，**靶子会一路漂出场外**。把中心做成显式字段：

```csharp
public UnityEngine.Vector3 MoveCenter;
public bool MoveCenterSet;
private void Awake()
{
    if (!MoveCenterSet) { MoveCenter = transform.position; MoveCenterSet = true; }
}
```

反复重挂期间也要注意：`Update` 里 `FireNow()` 这类**测试入口**要和真实输入路径分开，否则会互相污染统计。

## Export / Bind 的真实边界

- 导出生成包含 using、完整类声明和 MonoBehaviour 基类的包装源码，**不是只写成员片段**。
  首次需要 Play 中的活动 handle。ExportMany 只在 `.yokiframe/engine/live-exports/` 暂存，
  收到批次后单独 live_export_commit，再等 live_export_status 的 Committed 和每项
  sourceMatches/compiled 均 true。最后退出 Play，等 EditMode 空闲，再提交 Bind。
- 多实例同 className、源码和 outputPath 会合并为一份 `.cs`，保留每个目标自身字段/引用。
  不需要 TargetPatrol01/02/03 三个类；outputPath 仍须 `Assets/.../<className>.cs`。
  额外目标默认值独立，已由本工具绑定的组件保留当前值。不要循环 await Export 做批量。
- 不要把完整同名 class 包装交给 Attach。成员依赖 CallLive、临时 host 或其他原型类时须先改为正式组件通信；不能将原型 API 当成 MonoBehaviour 自带成员。
- 新 Export 不覆盖已有文件；修改已导出的代码用 Reexport(oldExportId,newMembers)，
  比较旧源码/meta 后原位替换、保留 .meta 与旧版本，无需删组件/删脚本/重回 Play。
  这也是 Prepared 批次，仍须显式提交和等编译。字段类型/重命名不做自动无损迁移。
- Bind 要求 EditMode 空闲、源码 hash 和编译版本匹配、目标场景干净且已加载。
  新账本核对组件 GlobalObjectId；Unity 删除后新建可复用该 ID，不能保证检测所有替换。
  旧 bound=true 记录先 Bind 旧 ID/原目标建立账本再 Reexport；旧格式只有类型/目标保证，
  binding=true 的中断旧记录不自动认领。旧 manifest 不改写。
- 批次中断为 Queued/Committing 或响应未知时先查状态，不能自动重放；Failed/Partial 先
  核对文件。提交与编译是两个阶段，暂存的 Roslyn 校验不证明 Unity asmdef 引用正确。
- 详细签名、限制和验收见包根 `Documentation~/Guides/Engine-LiveCode-Export-Contract.md`。
- 稳定代码也可直接维护 Assets 下正常 .cs，编译后按真实类型加组件，无需 LiveCode。Bind 会通过 Undo 加组件、恢复字段并保存原场景，必须明确授权；不要为自动化擅自保存或覆盖用户脏场景。
- `Bind` / `live_remove` / 重挂之后，旧 handle 可能报 `ObjectHandleExpired` 或
  `A behaviour ID cannot silently move to another object`。重建过场景对象后，先
  `live_remove` 该 id，再重新 `Attach`。

## 调参循环的工程注意

- **程序集预算**：先读 script_status 的 loadedAssemblies/loadedBytes、maxAssemblies/maxLoadedBytes、remainingAssemblies/remainingBytes。当前开发宿主为 4096 次/64 MiB，但以在线值为准，仍是源码常量。删除记录/handle 不卸载程序集。
- 脚本源码/引用快照命中同一 loader 的 128 项缓存时，不编译、不加载；未命中占 1，每次 Attach（包括同 ID 重挂）再占 1。SetField 本身不消耗，源码中改数值会让外层缓存失效。正常 Unity 编译不占 Roslyn 预算，但可能重置场景。
- loadedBytes 只是累计 PE+PDB 字节，不是 Unity 进程或托管堆内存。script_status 的 cachedScripts/maxCachedScripts、scriptCacheHits/scriptCacheMisses 可核对复用。冷启动依赖加载可让第二次引用快照变化；每次执行仍有独立上下文/结果，程序集静态状态不清零。淘汰不卸载，不同代码或重挂仍累积。
- **`WaitFrames(1)` 当前已修复**：gameFrame 等待至少一个后续真实 Update/LateUpdate 完成。暂停不推进；不是渲染/物理精确步进，也不保证恢复时恰好只过一帧。旧宿主用 Time.frameCount 的路径没有这个屏障，不能靠放大参数证明正确。
- **重复截图**：`await engine.Capture("game", path, autoNumber:true)` 自动编号且返回实际路径；默认不覆盖已有证据。
- gameFrame 只在 Play 可用；EditMode 用 editorTick。先检查 enterPlayModeOptionsEnabled 和 enterPlayModeOptions，不只看掩码 3；禁用域重载时退出重进 Play 不重置预算，禁用场景重载时不承诺重新读取磁盘层级。
- 目标仍在移动时，「瞄准一次再开火」会打空：瞄准和命中判定之间目标已经走掉了。验证命中要先把巡逻速度/幅度置 0，或让测试自己重瞄。

## 观察与调试

- 读状态用 `ReadField`；结果只会给出最后一次提交的值，要多帧采样就自己做。
- Game view 失焦或光标锁定时，不能用 Input.mousePosition 的旧值推断真实鼠标输入；自动开火测试不等于物理输入验证。
- 临时行为在**播放切换、域重载、授权撤销**时被清掉；已导出文件和记录不会。重建场景对象后 handle 也会失效。

## 零编译调参与文件热载

先查 domain_state/live_status 获取 sessionId/generation/revision，再发 Dangerous
`live_set_fields`，例如：

```json
{"target":"play","confirmed":true,"sessionId":"<current>","generation":123,
 "updates":[{"id":"Weapon","revision":1,"fields":[
   {"name":"Health","type":"System.Int32","value":75}]}]}
```

必须执行/trusted 双开关，精确字段类型，整批验证；48 KiB、64 行为、总 256 字段上限。
支持 int/有限 float/bool/string/枚举名字、Unity 向量/Quaternion/Color 数值数组，
Unity 另支持一维 `T[]`、`List<T>`、`[Serializable]` 数据 class/struct 的整字段替换。
不支持属性或 JSON 对象引用。不需新脚本，不加载程序集。同 ID 删除重挂 revision 递增。

```json
{"name":"Waypoints","type":"UnityEngine.Vector3[]","value":[[0,0,0],[2,0,1]]}
{"name":"Weights","type":"System.Collections.Generic.List<System.Single>","value":[0.2,0.8]}
{"name":"Stats","type":"WeaponBehaviour+StatsData","value":{"Health":100,"Speeds":[1.5,2.0]}}
```

这些是 `fields` 中的单项；`type` 也接受精确 `Type.FullName`，稳定 List 名避免带临时程序集名。
对象值须给齐声明的 public/SerializeField 可写字段，遗漏/拼错/多余字段都拒绝。
对象构造函数、字段初始化器和属性不执行，未序列化字段为默认值；适用于纯数据。
数组/List/class 可传 null，struct 不可。每集合最多 256 元素、深度 8、单字段解码 1024 节点。
同一批全部解码成功后写入，非法元素不会留下半批修改。不支持 `Stats.Items[0]` 路径更新。

策划文件放 `.yokiframe/tuning/*.json`，内容为上述载荷。显式 `live_tuning_bind`
接收 id/path/target=play/confirmed=true，最多 8 个绑定；后续只授权改 value，
不能改 session、ID、revision 或字段名单。250ms 检查 mtime/size，稳定 500ms 后应用，
不放 Assets，不做每帧 IO，不自动编译。
`live_tuning_status` 只读内存；`live_tuning_refresh` 需 id/target/confirmed，hash 相同不重写；
`live_tuning_unbind` 只需 id，关闭权限也可停止。状态 error 保留旧值，stopped 必须显式重绑。
换版、移除、权限撤销、会话切换和重载都停止，不自动续跑。

`script_status`/`live_status` 的 budgetWarning/recoveryHint、Attach 返回 handle.Budget、
AttachMany 返回 result.Budget 提供主动预警，剩余 3 个程序集或 1 MiB 字节时提示，不自动重载。
AttachMany 是 N 个行为程序集，外层脚本未命中缓存时另占 1，不能声称单程序集或原型类型互引。

## 显式快照与恢复

1. 预算耗尽也能直接 `yoki command send --kit RoslynKit --action live_snapshot --payload '{"target":"play"}'`。
   这是写盘的 UserAction，要求执行/trusted 开关；不是 ReadOnly，也不需要新脚本程序集。
   必须核对 complete=true 和每项 error。记录位于返回的项目内 path，不自动覆盖或删除。
2. 保存原成员源码及 snapshotId，检查记录中的目标和引用 key。运行时对象需要调用方建立
   key 到业务对象的映射；有 GlobalObjectId 也不一定是保存过的对象，不自行推测持久性。
3. 由外部显式重载，等待新 session/generation 和 Play target，再提交 Restore。禁止旧域
   Task 等待重载，禁止在响应未知时重放；先 run_result 对账。
4. 新提交中读取源码并重建对象，调用：

```csharp
var result = await engine.LiveCode.Restore(snapshotId,
    entry => sourceById[entry.Id],
    identity => objectsByKey[identity.Key]);
test.Equal(result.Success, true, result.Stage + ": " + result.Error);
```

sourceProvider 返回**原成员文本**，不是完整类；SHA-256 必须完全一致，没有自动迁移。
resolver 对临时目标/字段引用提供对象；同一 key 只解析一次，不按 Name 或旧 InstanceID 猜。
目标 key 通常 `<id>:target`，其他临时引用 `<id>:field:<field>`，以快照为准，不是永久业务 ID。
持久对象自动反查并校验类型/原场景，需原场景已加载；干净已保存场景的身份清单在进 Play
前采集并跨域保留，无清单时保守要求映射。项目移动/引擎版本变化会拒绝，v1 不支持迁移。

N 个行为恢复占 N 个程序集，外层脚本未命中缓存时另占 1；先检查全部 hash/数量预算，再内存编译
全部 PE/PDB 并检查精确字节，之后才加载/建 host。所有字段恢复后发布全部 ID，再依次 Awake。
失败报告 Stage/Error/Items，并清理本批 host；不会回滚回调、构造函数或 Awake 的任意副作用。
Snapshot 的 complete 只覆盖支持字段，不是整个场景。Patch、Faulted/禁用/非激活行为、
临时 host 引用和不支持的字段显式报不完整；私有非序列化状态、协程、场景层级不包含。
文件上限 2 MiB，64 个行为，每项字段 64 KiB/256 项。API 签名与边界见契约 §8.1。

## 已知边界

- Godot 支持本页末节列出的子集；导出 Player / IL2CPP / AOT 不支持。
- 没有专门的 Workbench LiveCode 编辑界面；走受信任脚本 API 加 `live_status` / `live_remove` / `live_snapshot`。
- 没有 prefab 资产绑定；`Bind` 只认保存过的场景对象。
- Unity 字段迁移、Snapshot/Restore、Export/Bind 支持上述数组/List/数据类及嵌套 Unity 引用，
  引用按 `Stats.Targets[0]` 路径保存，临时引用仍须 resolver。每行为最多 1024 展开节点，
  快照仍限 256 根字段/256 引用/64 KiB。旧标量记录可读，新集合记录要求升级后的宿主。
- 字典、多维/交错数组、直接嵌套集合、SerializeReference、自定义继承/多态、循环/共享
  托管对象不支持。集合嵌套用 Serializable 数据类包装；同一 Unity 对象的多处引用支持。
  含引用的非 null 数据对象通过受信任 SetField 设置，JSON 不接受对象引用。
- 回调异常禁用 host 并报 status=unavailable；手动 enabled=true 不清 Faulted。同 ID Attach 可用修复后源码重建，故障字段不自动迁移；框架不回滚任意副作用。
- RoslynKit/asset_ops refresh 曾出现未复验的 UnknownCommand；2026-10-06 稳定宿主已通过 FileBridge 复验成功。新故障应记录 requestId、session、在线 catalog 和错误，不据旧记录认定永久不支持，也不自动重放。
- Unity 与 Godot 均有 Snapshot/Restore、AttachMany、零编译字段、预算预警和文件热载。不自动重载或重放。

## Godot .NET / Tools 差异

- `--engine godot-editor --target editor` 与 `--engine godot-runtime --target runtime` 分别调用各自进程；没有 Godot play target。上述成员示例、Unity 字段类型、Export/Bind 不直接套用。
- `project.godot` 的 `yokiframe/engine/operations_enabled` 与 `yokiframe/engine/trusted_csharp` 均为布尔 true；先读 script_status.trustedSetting，不使用 Unity JSON 设置文件。
- Attach target 是已进入树的 Node，facade 提供 Node/GetNode/CallLive；生命周期为无参 void `_EnterTree`/`_Ready`/`_ExitTree`、void `_Process(double)`/`_PhysicsProcess(double)`。原生信号/Call 不自动转发到 facade。
- **Godot 碰撞回调是信号式的，和 Unity 名字不同**：`_OnBodyEntered/Exited(Node)`、`_OnAreaEntered/Exited(Area3D)`。宿主是从 `Node` 派生的 sealed 类型，**不是碰撞体**，所以碰撞来源按此顺序解析：目标自身是 `Area3D`/`RigidBody3D` → 接它；目标是 `CharacterBody3D` 等其他 `CollisionObject3D` → 无 `body_entered` 信号，不接；向上找最近的 `CollisionObject3D` 父级 → 接它；都没有 → **创建一个临时 `Area3D` 子节点**（SphereShape3D 半径 0.5）承载检测，宿主停止时回收。临时节点会出现在运行场景层级里。
- 延迟调用用 `Delay(float seconds, Action callback)`、`CancelDelay()`；基于 `SceneTreeTimer`，宿主停止或故障后不再触发。没有 Unity 那样的句柄，因为 Godot 侧同时只有一个待触发延迟。
- 声明的 public/[Export] 字段支持标量、枚举、Godot.Vector2/3/4、Quaternion、Color；快照可含 GodotObject 引用，直接 JSON 调参仍不接受引用。向量/颜色 value 是数值数组。
- live_snapshot/live_set_fields/live_tuning_bind/refresh 显式传当前 editor/runtime target。Snapshot/Restore 所有对象都须 resolver，包括保存场景里的 Node/Resource；还原全部字段后才执行 `_EnterTree`/`_Ready`，不猜 NodePath/旧实例 ID。
- runtime gameFrame 计真实处理帧，暂停和帧号跳跃不计数；editor 用 editorTick。默认 Bootstrap 随 SceneTree 暂停，CLI/调度也暂停，需游戏逻辑或 processAlways 计时器恢复。
- 编译产物在 Godot 的游戏 AssemblyLoadContext 加载；已加载且 Location 为空的程序集引用从 `.godot/mono/temp/bin/Debug` 匹配。先完成业务构建，不并行替换该目录。isCompiling=false 不是可靠的 Godot 构建守卫。
- 已验证恢复方式是显式停止/重启进程后 Restore；不保证 Godot Editor 内建 Build 的托管热重载恢复或 GC 回收。重建插件/bootstrap 不重置预算。Patch/Export/Bind/Capture 明确不支持，稳定代码维护正常 Godot C# 脚本。
