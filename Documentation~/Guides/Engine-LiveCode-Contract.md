# EngineKit LiveCode：运行中方法补丁与行为原型

> 当前验证环境：Unity 2022.3.16f1、Windows x64、Editor Mono，2026-10-06。
> 这是源码开发项目的实现与验收记录，不代表 Installer 已正式发布。
> Unity 已实现 Snapshot/Restore、AttachMany、零编译调参、预算预警和文件热载。
> Godot 4.7 .NET/Tools 的 editor/runtime 已接入 Roslyn、行为、恢复与调参，并完成真实进程验证。
> Godot 不支持 Patch、Export/Bind、Capture；正式分发不在本轮范围，见 `Engine-LiveCode-Completion.md`。

## 1. API 与使用分层

受信任的 `yoki script` 提交可使用 `engine.LiveCode`：

```csharp
await engine.LiveCode.Patch(id, originalMethod, source, patchType, patchMethod, "replace");
await engine.LiveCode.Attach(id, sceneObject, className, memberSource);
engine.LiveCode.SetField(id, "FireInterval", 0.12f);
float interval = (float)engine.LiveCode.ReadField(id, "FireInterval");
object result = engine.LiveCode.Invoke(id, "Hit", 5);
string exportId = await engine.LiveCode.Export(id, "Assets/Weapons/WeaponController.cs");
```

导出后等待正常 Unity 编译，退出 Play，再显式提交 `engine.LiveCode.Bind(exportId)`。

- 公共 `Export` **只有两个参数**：`Export(string id, string outputPath)`，返回 `Task<string>`。className、源码和字段来自该 ID 已挂载的版本，不另传四个参数。宿主内部持久化接口不是脚本 API。
- 批量用 `await ExportMany(requests, batchId = null)` 暂存，`CommitExport(batchId)` 显式提交；
  更新用 `await Reexport(previousExportId, members, batchId = null)`，无需 Play handle。
  `Bind(exportId, target = null)` 支持多个目标。完整流程见 [版本化导出契约](Engine-LiveCode-Export-Contract.md)。
- `List()` 返回 ID、kind、revision、源码 hash、session、target 与状态；`Remove(id)` 只移除该项。命令 `Engine/live_status` 诊断，`Engine/live_remove` 接收 `{"id":"..."}`，关闭执行开关仍可移除。
- 所有新执行仍需 Engine 操作与 trusted C# 授权、确认、当前 session/target 和宿主主线程。结束一次提交不移除它注册的行为或补丁。
- ID 和 className 为最多 80 字符的 ASCII 标识符。最多 64 个活动 handle；复用 ID 更新版本，同一行为 ID 不得静默换目标。
- 调参优先 `live_set_fields` 或显式文件绑定，方法调用优先 Invoke。
  **通过 `yoki script` 提交 SetField/Invoke，外层脚本仅在编译缓存未命中时消耗一次加载；数值写进不同源码会使缓存失效。**
- 稳定代码用正常 .cs/MonoBehaviour 和 Unity 编译流程；LiveCode 用于还没落盘且必须保持 Play 的原型。业务编译不占 Roslyn 预算，但可能触发重载、重置现场，不承诺固定一秒完成。

## 2. 预算与生命周期

`script_status` 返回实际 loadedAssemblies、loadedBytes、maxAssemblies、
maxLoadedBytes、remainingAssemblies、remainingBytes、budgetScope=domain、
budgetConfiguration=hostConstants。以在线值为准，不根据文档猜上限。

当前开发源码的上限为 4096 次加载、累计 PE+PDB 64 MiB；历史实现是 64 次加载。
这是宿主源码常量，不是运行时设置。本轮保留用户改过的值，没有替用户提高或降低预算。

- `yoki script` 缓存未命中后成功编译并加载消耗一个程序集；相同源码/引用快照在同一 loader 的 128 项缓存内复用入口，不增加预算。每次 Attach 成功编译并加载再消耗一个，**同 ID 重挂也一样**；Patch 共用预算。冷启动依赖加载可改变引用快照，细则见 [Roslyn 契约](Engine-Roslyn-Automation-Contract.md) §3。
- loadedBytes 只计累计 PE+PDB，不是 Unity 内存占用；缓存统计由 script_status 的 cachedScripts/maxCachedScripts、scriptCacheHits/scriptCacheMisses 给出。缓存不共享运行上下文，但复用程序集的静态状态，不会卸载旧程序集。
- Export 编译正式包装作校验但不加载；字段读写、方法调用、Remove 不增加或退还预算。
- 失败发生在加载之后（如激活异常）也不能退还已经加载的程序集。编译失败未加载则不消耗。
- 达到任一上限拒绝新加载，错误指向 script_status。不会自动重载、清空计数或重放代码。
- 域重载才重置计数。关闭 Domain Reload 时，仅退出/进入 Play 不一定重置预算；删除记录/handle 不卸载程序集。
- Play 切换、域重载、退出编辑器和撤销授权会清理临时 handle，并阻止尚在编译的旧请求加载。
- `script_status` / `live_status` 返回 budgetWarning/recoveryHint：剩余程序集 <=3 或剩余
  字节 <=1 MiB 时预警，不自动重载；前者同时返回两个 warningRemaining 阈值。
  Attach/Patch 的 handle.Budget、AttachMany 的 result.Budget 返回当时预算。
  一个 manager 内同 ID 删除后重挂的 revision 继续递增，旧调参请求失效。

预算错误现在指向无编译 `live_snapshot`：先核对 complete/errors，再显式重载和 Restore。
不承诺恢复 Patch、任意场景状态或业务副作用；框架不会自动重载。

## 3. 已有方法补丁

可选 HarmonyX 2.16.1 包在 Unity 进程内拦截托管 C# 方法。源码是完整 static patch 类：

```csharp
public static class ReadPatch
{
    public static bool Prefix(string id, ref EngineKitDemo.DemoUnitStats __result)
    {
        __result = new EngineKitDemo.DemoUnitStats(id, 150, 150, 30, 0);
        return false;
    }
}
```

prefix/postfix 遵循 Harmony 参数约定；replace 是 bool prefix，返回 false 才跳过原方法，
非 void 结果须赋 ref __result。返回 true 仍执行原方法，其他所有者的 postfix 也可能
修改结果。这不是替换源文件或任意原生内存修改。

拒绝 abstract/native、泛型方法/类型、值类型声明类、by-ref 返回及引擎/运行时/
YokiFrame 基础设施程序集。内联、Burst、async 状态机、native API、IL2CPP/AOT
不在已验证路径内。补丁针对方法而非单实例；实例过滤需显式用 __instance。

编译失败保留旧补丁。更新仅撤销旧的精确 patch 方法，不移除其他工具的补丁。
补丁执行异常不自动 unpatch，业务副作用无法事务回滚。

依赖构建：`dotnet build YokiFrameWorkbench~/src/YokiFrame.EngineKit.Patching -c Release`。
完整 DLL、manifest、许可证复制到
`Core/Adapters/Unity/Editor/EngineKit/Dependencies~/harmonyx-2.16.1`；
Unity 忽略 ~ 目录，由适配器反射加载。独立分发的回退路径为
`.yokiframe/automation/patches/harmonyx-2.16.1`。不要只复制 0Harmony.dll。
patchInstalled 仅证明入口 DLL 存在，不保证所有目标兼容；Roslyn 编译器仍独立安装。

## 4. 行为原型与通信

Attach 接收类成员，不是完整类或方法体，不在成员顶层写 using。宿主生成普通
C# facade，预编译的 editor-only MonoBehaviour host 转发生命周期：
Awake、OnEnable、Start、Update、FixedUpdate、LateUpdate、OnDisable、OnDestroy。
仅非泛型、无参、void 实例回调；帧回调缓存 delegate。

facade 提供 gameObject、transform、单个/复数 GetComponent 查询、
GetComponentsInChildren/GetComponentsInParent（含 includeInactive 和 List 输出）、
Instantiate、Destroy。它不是完整 MonoBehaviour：
UnityEvent 目标、enabled、动态 AddComponent<原型>、Inspector 编辑仍不支持
（原型不是 Component，这几项原理上无法支持）。

物理与延迟回调按方法名转发，签名固定，都不需要原型自己写轮询：
`OnCollisionEnter/Stay/Exit(Collision)`、`OnTriggerEnter/Stay/Exit(Collider)`、
`OnControllerColliderHit(ControllerColliderHit)`；延迟用
`Delay(float seconds, Action callback)` / `CancelDelay(handle)`。
物理消息是声明式的，宿主必须**在编译期**实现它们 Unity 才会回调，因此宿主恒定
实现全部 7 个消息；原型未声明对应方法时委托为空、直接返回。签名不符在 Attach
阶段拒绝，不会退化成运行时静默不触发。目标对象仍需自己具备 Collider /
Rigidbody，原型不替它添加物理组件。

**原生 SendMessage/SendMessageUpwards/BroadcastMessage 不转发到 facade。**
不同原型独立编译，不能直接引用另一个内存程序集的类。改用明确的 ID 调用：

```csharp
// 接收原型的 members
public int Health = 100;
public int Hit(int amount) { return Health -= amount; }

// 发送原型的方法内部
int remaining = (int)CallLive("TargetPrototype", "Hit", 5);

// 外部受信任脚本
int remainingFromScript = (int)engine.LiveCode.Invoke("TargetPrototype", "Hit", 5);
```

- 每次按 manager 的当前 ID 解析，重挂后指向新版本；不缓存旧 Instance/MethodInfo。
- host 的 Id 和 Instance 只读，同一对象可有不同 ID 的多个 host，不按组件顺序选择。
- 仅调用原型自己声明的 public 实例方法，不调用 private、属性访问器、开放泛型或 ref/out/pointer 签名。
- 参数按声明类型匹配，不隐式数值转换，不补默认参数；多个可匹配重载直接拒绝，不猜。复杂高频通信优先已有事件系统或预编译共享接口。
- 普通返回值按原 C# 引用语义返回；Task 是返回对象，调用方需显式转换并 await。异常保留业务异常，不包装为成功。
- CallLive 是 editor-only 原型 API，正式 MonoBehaviour 没有它。导出前改成共享接口/事件通信；导出编译验证会拒绝未迁移的调用。

旧版本先暂停，新版本激活成功后再释放旧 host。编译失败不改旧版本，激活失败尝试恢复
仍存活的旧版本。Awake 会重跑，订阅/资源清理由生命周期负责，不能回滚任意游戏状态。
回调异常禁用 host，标 Faulted、live_status 为 unavailable；手动 enabled=true
不会恢复。修复后同 ID Attach 会创建新 host，故障版本字段不自动迁移，旧 host 在成功后释放。
Configure 仅允许一次，不作为清错重启 API。

字段迁移/导出只覆盖声明的 public 或 SerializeField 非 readonly 字段：
int/float/bool/string/枚举、Vector2-4、Quaternion、Color、Unity 对象引用。
SetField 要求精确类型；缺失字段忽略，同名类型变化拒绝，未标记的私有状态重新初始化。
Unity 另支持一维 `T[]`、`List<T>`、标记 `[Serializable]` 的无自定义基类 class/struct，
可递归组合（集合的元素不能直接是集合，须用数据类包装，遵守 Unity 序列化限制）。
数组/List 元素及数据字段支持上述标量、向量、颜色、枚举和 Unity 对象引用。
只处理声明的 public / `[SerializeField]` 可写字段，不执行属性访问器；数据实例重建
不调用用户构造函数，私有非序列化状态为默认值，不适用于依赖构造函数建立不变量的业务对象。
字典、多维/交错数组、直接嵌套 List、自定义继承、多态、SerializeReference、循环及共享
托管引用拒绝；多个字段引用同一 Unity 对象仍支持。每个集合最多 256 元素、根值深度 0
到 8、一次行为字段状态最多 1024 节点。SetField 本身仍接受精确类型；不可持久的对象图
会在重挂/快照/导出时显式失败，不会静默截断。
状态采用扁平路径（如 `Stats.Targets[0]`）；List 的持久类型名不含动态程序集身份，
因此同 ID 重编译可以还原 `List<原型内嵌数据类型>`。旧标量状态保持可读。

## 5. 等帧、截图与 Play 前提

旧实现从 EditorApplication.update 采样 Time.frameCount，不能证明完成一次业务 Update；
放大 WaitFrames 参数不是可靠修复。

当前 Unity gameFrame 时钟在 ScriptRunBehaviourUpdate 前记录开始，在
ScriptRunBehaviourLateUpdate 后记录完成。WaitFrames(1) 从调用时已开始帧数起等待
至少一个后续帧完成 Update/LateUpdate；editorTick 只推进调度，不满足这个屏障。
计数 0 立即完成，暂停不推进，恢复后继续。它不是渲染完成、物理精确步数或 GPU 完成屏障；
低帧率/编辑器调度可能使恢复晚于第一个满足条件的帧，不保证“恰好一次更新”。
编辑态用 clock=editorTick，不自动进入 Play。

`await engine.Capture("game", path, autoNumber:true)` 返回实际 PNG 相对路径。
默认仍不覆盖；自动编号选择 name-001.png 等并预留路径，连续调用保留旧证据。
限运行且未暂停的 Game view，单边最多 4096、文件最多 32 MiB、路径限
`.yokiframe/automation/evidence/`。不改场景相机，不保存场景。

开始验收先读 EditorSettings.enterPlayModeOptionsEnabled 和 enterPlayModeOptions，
两者一起决定是否禁用域/场景重载。掩码 3 单独不证明生效。2026-10-06 本轮观察为
enabled=false、掩码 3，未修改它们。禁用 Scene Reload 时不承诺 Play 会重新读取磁盘
场景；修改磁盘层级后须显式处理编辑器内存场景。不要为测试擅自保存/重开用户脏场景。

## 6. 导出与绑定

Export 生成包含 using、完整类和 MonoBehaviour 基类的源码包装，**不是只写成员片段**。
2026-10-06 已改为版本化流程：ExportMany 先编译校验并暂存到
`.yokiframe/engine/live-exports/`，不写 Assets；CommitExport/live_export_commit
统一写文件并请求导入。最后的导入仍可域重载。旧两参 Export 自动安排单项提交，不用于循环批量导出。

首次导出要求路径 Assets/.../<className>.cs、进 Play 前已保存的场景对象及可持久字段。
相同类/路径/源码合并，保留逐目标字段和引用；运行时临时对象/引用拒绝。新导出不覆盖已有
脚本，显式 Reexport 则比较旧源码/meta hash 后原位替换，保留 .meta 和历史记录，
无需删组件、删脚本或重新 Attach。源码依赖其他原型类型、CallLive 或临时 host 时须先迁移。

Bind 仅 idle EditMode；核验源 hash 与具体编译版本，目标场景已保存/加载且干净。
一个版本可绑多个对象：已捕获目标恢复自身字段，额外目标用默认值，既有受管组件保留当前值。
新组件走 Undo 并只保存目标场景；重复 Bind 不加组件。新账本逐目标记录组件身份，
旧记录迁移和 GlobalObjectId 复用限制见 [版本化导出契约](Engine-LiveCode-Export-Contract.md)。
Committed 不等于编译成功，必须再查询 live_export_status.compiled。不支持 prefab 资产或自动字段结构迁移。

## 7. 双引擎边界

Core 共享 API/manager、版本/hash、诊断、预算、权限/session/target、取消、补丁归属
与项目内路径，不引用 UnityEngine/Godot。适配器负责包装、Prepare/Suspend/Activate、
IsAlive、字段、Invoke、Export/Bind、对象身份和场景事务。

| 边界 | Unity 已实现 | Godot 已实现或明确限制 |
|---|---|---|
| 目标 | Play 场景 GameObject | editor/runtime 各自进程内、已进入树的 Node |
| 临时宿主 | 预编译 MonoBehaviour + facade | 预编译 Node + facade |
| 帧回调 | Update/FixedUpdate/LateUpdate 等 | `_EnterTree`、`_Ready`、`_Process(double)`、`_PhysicsProcess(double)`、`_ExitTree` |
| 碰撞回调 | `OnCollision*`、`OnTrigger*`、`OnControllerColliderHit`，按方法名转发 | `_OnBodyEntered/Exited(Node)`、`_OnAreaEntered/Exited(Area3D)`，走信号；宿主为 sealed Node，碰撞体必须来自目标或父级节点 |
| 延迟调用 | `Delay(seconds, callback)` / `CancelDelay(handle)` | `Delay(seconds, callback)` / `CancelDelay()`，基于 `SceneTreeTimer` |
| 恢复身份 | GlobalObjectId + 场景路径或显式映射 | 所有 Node/Resource 均显式映射，校验精确类型与原 SceneFilePath |
| 持久类 | 正常 MonoBehaviour | 正常 C# Node 脚本 |
| 绑定 | Undo + 场景保存 | 未实现，不隐式保存场景 |

Godot 使用 `GodotLiveAutomation` 接入两个正式宿主，不经过 GDScript eval。
两个 project.godot 布尔设置为 `yokiframe/engine/operations_enabled` 和
`yokiframe/engine/trusted_csharp`；每次执行仍需 confirmed。编译器包路径与 Unity 相同。
程序集加载到 Godot 自身的游戏 AssemblyLoadContext，不另载一份 Godot/业务类型；对内存
加载且 Location 为空的已加载程序集，从 `.godot/mono/temp/bin/Debug` 中匹配程序集身份的
DLL 取得编译引用。依赖必须先正常构建，不能一边替换该目录一边提交脚本。
预算在该加载上下文内共享；重建插件/bootstrap 不重置，单个 Remove 不卸载。
编译器退出解绑 AssemblyResolve，不宣称已证明 Godot 热重载上下文的完整 GC 回收。

Godot facade 提供 Node、GetNode、CallLive，不是完整 Node；原生信号/Call 不自动转发。
Suspend/回滚恢复不重跑 `_EnterTree`/`_Ready`，真正新版本会重跑；停止只调用一次退出回调。

**Godot 碰撞与 Unity 不同，必须说清**：Godot 没有"声明式碰撞消息"，碰撞依赖节点类型与信号。宿主 `YokiFrameGodotLiveBehaviourHost` 是从 `Node` 直接派生的 sealed 类型，**永远不是碰撞体**，因此原型声明 `_OnBody*` / `_OnArea*` 时宿主按以下顺序解析碰撞来源：① 目标节点自身是 `Area3D`/`RigidBody3D` → 接它的信号；② 目标是 `CharacterBody3D` 等其他 `CollisionObject3D` → 无 `body_entered` 信号，不接；③ 向上查找最近的 `CollisionObject3D` 父级 → 接它；④ 都没有 → 创建临时 `Area3D` 子节点（含 SphereShape3D，半径 0.5）承载检测，宿主停止时 `QueueFree` 回收。`RigidBody3D` 的 `body_entered` 携带 `Node`、`Area3D` 的携带 `Node3D`，原型统一按 `Node` 绑定以兼容两者；`_OnAreaEntered/Exited` 参数为 `Area3D`。临时 `Area3D` 会出现在运行场景层级里，这是可观察的副作用，不是隐藏行为。
字段为声明的 public/[Export] 可写字段：标量、枚举、Godot 向量/Quaternion/Color、
GodotObject 引用；引用仅用于快照/脚本 SetField，直接 JSON 调参仍不接受对象引用。
所有目标/引用都要求 resolver；不把 NodePath、名称、旧 InstanceId 当成跨进程身份。
恢复先写全部字段再调用任何 `_EnterTree`/`_Ready`，source hash/项目/引擎版本/target 均须匹配。

runtime 的 gameFrame 只计已观察的非暂停处理帧，用后续帧证明上一处理阶段完成；
暂停期间的全局帧号跳跃不计入等待。editor 使用 editorTick，gameFrame 明确失败。
默认 Bootstrap 随 SceneTree 暂停，桥接/调度也会暂停；需外部游戏逻辑或 processAlways
计时器恢复，不能靠暂停中的 CLI 自救。等待不是渲染/物理精确屏障。

Godot 目前没有可靠的公开构建中状态接线，domain_state.isCompiling=false 不证明磁盘 DLL
未变化。恢复验证采用显式停止并重启 Godot 进程，不自动触发/重放托管热重载；编辑器
内建 Build 热重载期间的自动恢复与 GC 卸载不属于本版保证。正式 Installer 分发、
Godot Patch/Export/Bind/Capture 和专用 Workbench 原型编辑界面仍未实现。

## 8. 长期迭代恢复

目标是让预算耗尽变得可预期、现场可恢复，而不是依靠不断提高上限。
显式 behaviour 快照与恢复见 §8.1；批量、调参和文件热载见 §8.2。

### 8.1 Snapshot / Restore 第一版（已实现）

```bash
yoki command send --kit Engine --action live_snapshot --payload '{"target":"play"}'
```

这是 **UserAction**，不是 ReadOnly/FastChannel 查询，因为它写磁盘。需要执行与 trusted
C# 开关、Play target 和空闲宿主；不编译、不加载程序集、不执行用户 getter、无新脚本
槽位需求。返回 snapshotId、path、complete、逐项 id/error、sessionId/generation。
只有 complete=true 才进入恢复；false 仍保存诊断记录，不代表“全量现场”可用。

文件固定为 `.yokiframe/engine/live-snapshots/<snapshotId>.json`：schema=1，项目绝对路径
指纹、引擎/版本、原 session/generation/target、每项 id/className/sourceHash、目标
身份/场景、字段和对象引用。随机新 ID、不覆盖、先临时文件再发布，拒绝路径越界和链接。
上限为 64 项、总文件 2 MiB、每项字段 64 KiB/256 个字段/引用；不持久化源码或 delegate。
不自动删除历史快照。项目移动后指纹不匹配，第一版不支持跨项目迁移。

脚本内真实签名：

```csharp
YokiFrameLiveSnapshot saved = engine.LiveCode.Snapshot(); // 同样写盘，返回 SnapshotId
YokiFrameLiveSnapshot record = engine.LiveCode.ReadSnapshot(snapshotId);
YokiFrameLiveRestoreResult result = await engine.LiveCode.Restore(
    snapshotId,
    entry => sourceById[entry.Id],       // string 成员源码；核对 ClassName/SourceHash
    identity => objectsByKey[identity.Key]); // 可省略；有临时对象时必需
test.Equal(result.Success, true, result.Stage + ": " + result.Error);
```

- Snapshot 自身不加载，但 `yoki script` 外壳在缓存未命中时加载一次；预算耗尽优先用直接命令入口。
- sourceProvider/resolver 只由新提交提供，不读取快照里的代码执行。源码按 UTF-8 原文
  SHA-256 比较，换行变化也算改变；没有隐式迁移或 allowMismatch 选项。
- 自动身份不能仅检查 GlobalObjectId 非零：真机证明运行时新对象也能有可反查的 ID。
  Unity 进入 Play 前记录**干净、已保存且已加载场景**的对象清单，SessionState 跨域保留；
  只有清单命中或持久资产才用 GlobalObjectId。遍历超过 32768 对象/组件或 2 MiB 清单时
  保守放弃清单，改用显式映射。Play 期间新加载场景、脏/未保存场景、无前置清单的对象
  都不冒充持久对象。恢复时再核对项目、引擎版本、场景路径、GlobalObjectId 和精确类型。
- 临时目标 key 为 `<id>:target`，非目标的临时字段引用为 `<id>:field:<field>`。
  同一对象在整个快照内共享 key。key 是本快照恢复标识，**不是永久业务 ID**；
  调用方在重载前保存 key 到自身业务标识的映射，新域重建全部目标后再映射字段引用。
  不根据 name、层级路径或旧 instanceId 猜测；名称仅供诊断。
- 不恢复 Patch、Faulted/禁用/非激活行为、临时 host 引用和任意对象图。
  Unity 支持上文规定的集合和数据类，嵌套引用按字段路径映射并清除所有旧 InstanceID；
  256 上限指根字段/引用数量，展开节点上限 1024，64 KiB 字段文本限制仍适用。
  这些项记 error；私有非序列化字段、协程、Transform/场景层级本来就不在字段契约内。
  complete 只代表本契约内的记录完整，不代表整个游戏现场。
- 恢复顺序：验证记录、冲突 ID、全部源码 hash 与数量预算；所有源码编译到内存 PE/PDB
  **但不加载**，按精确累计字节预检；解析全部目标，再解析全部引用；加载/创建禁用
  host；写回全部字段；发布全部 ID，最后依次激活。所有字段先于任何恢复 Awake。
  每次异步返回和用户回调后复核权限/session/target/取消；恢复期间拒绝并发 Attach/Remove。
- result 包含 Success、Stage、Error、RequiredAssemblies/RequiredBytes、Items 的
  Status/Error 和 UserCodeMayHaveRun。失败停止整批并清理本轮 host，保留已有无关 ID；
  清理失败逐项标 cleanupFailed。已经加载的预算不可退还，构造函数/sourceProvider/
  resolver/Awake 的任意副作用不可撤销。输入文件/权限等前置错误仍可能直接抛异常。
- 恢复仍需 N 个行为程序集，外层脚本在缓存未命中时另占 1；没有把 N 个行为合成一个程序集。
  预算预警已实现，但不自动重载。
- 域重载由外部明确触发，等新 session/generation 和正确 target 后再提交 Restore。
  旧域 Task 不串起重载；未知执行结果按原 runId 对账，绝不自动重放。

### 8.2 批量与调参分层（Unity / Godot 已实现）

- `await engine.LiveCode.AttachMany(IReadOnlyList<YokiFrameLiveAttachmentRequest>)` 接受 1..64 项，
  每项构造为 `(id, target, className, members)`。全部校验/编译但不加载，核对精确预算后准备
  所有 host、迁移字段、暂停旧版本、发布全部新 ID、最后激活。返回 Success/Stage/Error/
  Items(status/error/handle)、RequiredAssemblies/RequiredBytes、Budget、UserCodeMayHaveRun。
  失败清理新 host 并恢复可用旧版本；回调副作用和加载预算不可回滚。提交后旧 host 清理
  失败报 cleanupFailed，不冒充回滚。N 个行为仍分别编译，2N 次加载变 N+1，不是降低数量级。
- 真正批量编成一个程序集能进一步减量，但跨版本类型依赖、替换粒度、命名冲突是另一个
  设计，不隐含在 AttachMany 内。编译成功不等于所有行为激活成功。
- `live_set_fields` 为 Dangerous，要求 CLI/Workbench、target=play、confirmed:true、执行/
  trusted 双开关。最多 48 KiB/64 行为/总共 256 字段；绑定 sessionId/generation/revision。
  public/SerializeField 可写实例字段白名单，精确 type.FullName，整批验证后写，失败还原
  已写字段并报告回滚失败。不调用 getter/setter，不加载程序集。JSON 对象引用/属性不支持。
- 字段支持 int、有限 float、bool、string（可 null）、枚举精确声明名字，以及 Vector2/3/4、
  Quaternion、Color 的对应长度数值数组。Unity 还支持上述数组、List 和数据 class/struct，
  采用整字段替换；数据对象必须给齐全部白名单字段，缺失/多余字段均拒绝，不运行构造函数
  或字段初始化器。数组/List/class 可用 null，struct 不可；含引用的非 null 数据对象用
  受信任 SetField。每次解码最多 1024 节点、深度 8、每集合 256 元素；请求外壳不计值深度。
  `type` 接受精确 FullName 或稳定名（例如 `System.Collections.Generic.List<System.Int32>`），
  自定义内嵌类型用 `Behaviour+Stats`，不使用 C# 别名。集合长度变化不改变调参授权字段范围。
  Godot 仍限原有字段类型。先从 domain_state/live_status 获取上下文：

```json
{"target":"play","confirmed":true,"sessionId":"<current>","generation":123,
 "updates":[{"id":"Weapon","revision":1,"fields":[
   {"name":"Health","type":"System.Int32","value":75},
   {"name":"Tint","type":"UnityEngine.Color","value":[1,0.5,0.2,1]}]}]}
```

- 文件热载复用上述完整载荷，只允许 `.yokiframe/tuning/*.json` 子树，拒绝越界/链接。
  `live_tuning_bind` 为 Dangerous：id/path/target/confirmed，显式授权并立即应用，最多 8 个绑定。
  授权范围固定 session/generation/ID/revision/字段名/类型，文件只能改 value，改范围需重新绑定。
- `live_tuning_status` 为 ReadOnly，只读内存的 state/error/applyCount/appliedFields/contentHash；
  `live_tuning_refresh` 为 Dangerous，id/target/confirmed，hash 未变不重复写；
  `live_tuning_unbind` 为 UserAction 取消豁免，id，关闭权限仍可停止。
- 最多每 250ms 检查 mtime/size，变化稳定 500ms 后读有界文件并整批应用；缓存 FieldInfo，
  不做每帧 IO/反射。保留 mtime/size 的外部编辑需显式 refresh。错误保留旧字段并报告。
  权限撤销、会话/目标切换、行为换版/移除、域重载后停止，不自动续跑或重放。

建议顺序：无编译快照与恢复闭环 + 预算预警 → 批量预检/恢复 → 调参用法和直接通道 →
策划文件热载。上述通路已接入 Unity 与 Godot；Unity target=play，Godot target=editor/runtime，
Godot 字段类型使用 Godot.Vector3 等全名，授权设置与恢复限制见 §7。

## 9. 验证证据

### 长期迭代增量最终回归（2026-10-06）

EngineKit **240/240**、Roslyn **7/7**、Godot Editor **31/31**、Runtime **96/96**；
Unity 编译门与 Godot 隔离验收工程构建 **0 警告/0 错误**。独立测试工程有既有 nullable 警告。
Skill 的入口及 engine-kit/livecode/cli-commands/installer 共 5 文件已同步安装副本，
SHA-256 相同，UTF-8 模式下两份 Skill 校验通过。

| runId / requestId | 本次证据 |
|---|---|
| `b0b8d90f702547f7884651927c79991a` | Unity 6/6：最新加载器下批量、真实等帧、字段、快照、预算与场景脏状态不变 |
| `02fb11707fb644c8a11b03c35242140e` | Godot Editor 8/8：正式插件经 FileBridge 执行，暂停恢复不重入、非有限字段拒绝、字段早于 Ready |
| `8b126e1db88b459ebf4bf309ed604ec0` | Godot Runtime 5/5：正式 Bootstrap 经 CLI 执行、等帧、暂停期间不推进、不计帧号跳跃、快照 |
| `0860c3bf96e94c45a85d30872bd7bb53` | Godot 2/2：调参准备、重建自动化实例不重置上下文预算 |
| `cli-1791290238615-f4d03bef` / `cli-1791290238884-5ac63477` | Godot 直接字段与文件绑定；加载数 2、字节 18748 前后不变，后续文件修改 applyCount=2 仍不增加加载 |
| `9e688d399c8c422ba13f417cdb694001` | Godot 4/4：文件热载值 42、Color 解码、Capture 明确拒绝、清理零 handle |

Godot 4.7 .NET/Tools 的隔离工程 `scripts/engine/godot-live-tests/` 使用正常项目构建。
最新快照 `4334c2e0a88f4373a9eb088c52134d93`：第一个进程 snapshot **9/9**，
第二个独立进程 restore **14/14**，覆盖新 session、目标/共享引用映射、所有字段早于 Ready、
直接字段调参不加载程序集。不是 Editor 内建 Build 热重载/GC 卸载验收。
场景、测试行为和绑定均已清理，Unity 回 EditMode，测试 Godot 进程正常退出。
历史证据如下，计数只对应当时源码，不冒充当前全量回归。

2026-10-06 本轮真实 Unity 运行记录：

| runId / requestId | 结果 |
|---|---|
| `21579a1c32b946cf9346dbcda7c00627` | 32 条断言：跨原型 ID 调用、重挂转向新版本、复数组件查询、12 次 Update/LateUpdate 等帧、自动编号截图、操作窗口无 Unity 编译/重载 |
| `06524878c5b54bbea847b6b5f771e4f3` | 7 条断言：Faulted 不能靠 enabled 复活、同 ID 修复重挂、旧 host 清理、暂停不推进 gameFrame、Export 包装与两参签名 |
| `cli-1791283108189-cf652725` | 稳定宿主 FileBridge 的 asset_ops refresh 成功；之前的 UnknownCommand 未复现，不宣称已找到原错误根因 |

截图为 `.yokiframe/automation/evidence/livecode-regression.png` 与
`livecode-regression-001.png`。故障用例故意写出一条
expected-livecode-fault-regression 控制台异常，不混同未预期错误。
两次提交处于 session `3d1ed51ac648411eafb5c873db4c82b0`，
generation `639268799486573568`。临时对象和 handle 在 finally 中释放，没有保存场景、
修改相机或 Play 设置；后续观察已回 EditMode。

同 ID 重挂预算另有真实 Roslyn 的宿主无关回归：两次 Attach 分别增加一次加载，
50 次 SetField 和 Remove 不再增加、不退还；在线复测时宿主已切 EditMode，请求在
执行前被拒，不能将这次零增量当作成功证据。

Snapshot/Restore 第一版新增证据（2026-10-06）：

- `514c2faaf55b476c85c50b66215f4547`：准备 3 个行为，6 条断言通过。
- `cli-1791285529205-a6da3f02`：无编译快照，snapshotId
  `314e173a5d4e49f6bfbd5d8ba8dbe6aa`。前后 loadedAssemblies=4、
  loadedBytes=65692，均零增量；两个运行时目标使用显式 key，保存场景目标使用 GlobalObjectId。
- `525ae04c109e4cb29ac4f7677b0367a3`：显式 RequestScriptReload，CLI 等待中断后
  按原 runId 对账为 Passed，未重放。随后显式进入 Play，session 从
  `9d24f3a651d04e31a00cd1a6812e1d26` 变为 `9993cc38f24a432893ecf9511c70837c`。
- `ad74154f87eb425a8d4d5fe8e06d2562`：16 条断言通过，恢复 3 个行为，
  46212 PE/PDB 字节；临时目标及共享字段引用显式重建，持久目标自动反查，
  所有字段在 Awake 前已还原。脚本总用时 2611ms，不是固定性能承诺。
  场景仍干净，未保存场景/修改相机或 Play 设置，finally 清理并退出 Play。

首次实测发现仅“非零 GlobalObjectId + 当场反查”会误判临时对象，已改为前置场景
清单。旧试验快照 `b6e3d143ccf24d17b70607ebdcd6ddde` 不作为恢复成功证据。
本地新增覆盖耗尽预算、换 manager/session、错误 hash/项目/schema、重复 ID、限额、
取消、并发冲突、解析/字段/激活失败清理、全批次字段先于激活。

最终复验：EngineKit **207/207**（新增 25 例）、Roslyn **7/7**、Godot Editor **31/31**、
Godot Runtime **96/96**；Unity 编译门 **0 警告/0 错误**。测试工程保留已有 nullable 警告。
补齐字段清单预校验、引用去重与缺失映射拒绝后，真机
`6aab5a15530440b9a0bf8e66dd404d69` 再次 **16/16**，总用时 2852ms；
`ea8ab73a029f4069bfcc0240a8ec9060` **4/4** 验证禁用行为的快照明确不完整且可持久读取。
可复用验收脚本位于开发项目 `scripts/engine/live-recovery-*.csx` 和
`live-recovery-probe-members.cs`，不生成 Assets 下的任务源码。

先前验收记录保留：`91298e2955dc46e086785a94d581af87`（补丁 9 断言）、
`5128fd57a3c74e288f6d79fac67a8c7a`（行为 8 断言）、
`7715e93e4d4d4c268dc8c6a2bfb039c7`（开火/截图/导出）、
`cfdea51c05ca4bf8958646306819b893`（绑定 6 断言）、
`f762eb3da6544aa7bea8fabe1f3bd888`（正常 MonoBehaviour 6 断言）。
历史 exportId `2855d9d1f915443c8b6bbf2e10624bcb`，
场景 `Assets/EngineKitLiveDemo/EngineKitLiveDemo.unity`，
脚本 `scripts/engine/live-gun.csx`，截图 live-gun-runtime.png /
live-gun-persisted.png。自动开火不是物理鼠标验证；历史记录不代替本轮完整导出验收。
