# YokiFrame Kit 使用速查

## EventKit

优先使用强类型事件：

```csharp
public readonly struct EnemyKilledEvent
{
    public readonly int EnemyId;

    public EnemyKilledEvent(int enemyId)
    {
        EnemyId = enemyId;
    }
}

void OnEnable()
{
    EventKit.Type.Register<EnemyKilledEvent>(OnEnemyKilled);
}

void OnDisable()
{
    EventKit.Type.UnRegister<EnemyKilledEvent>(OnEnemyKilled);
}

void OnEnemyKilled(EnemyKilledEvent evt)
{
    score += 100;
}

void KillEnemy(int enemyId)
{
    EventKit.Type.Send(new EnemyKilledEvent(enemyId));
}
```

枚举事件适合系统级 key：

```csharp
public enum GameEvent
{
    LevelLoaded,
    PauseChanged
}

EventKit.Enum.Register<GameEvent, bool>(GameEvent.PauseChanged, OnPauseChanged);
EventKit.Enum.Send(GameEvent.PauseChanged, true);
EventKit.Enum.UnRegister<GameEvent, bool>(GameEvent.PauseChanged, OnPauseChanged);
```

规则：

- 注册和注销成对出现。
- payload 优先不可变结构。
- `EventKit.String` 只做兼容，新代码不扩展。
- 编辑器工具通信不要使用运行时 EventKit；使用 Editor 内存通道或文件桥。

## FsmKit

```csharp
public enum PlayerState
{
    Idle,
    Move,
    Jump
}

public sealed class IdleState : AbstractState<PlayerState, PlayerController>
{
    protected override void OnEnter()
    {
        mBlack.PlayAnimation("idle");
    }

    protected override void OnUpdate()
    {
        if (mBlack.HasMoveInput)
        {
            mFSM.Change(PlayerState.Move);
        }
    }
}

var fsm = new FSM<PlayerState>("PlayerFSM");
fsm.Add(PlayerState.Idle, new IdleState());
fsm.Add(PlayerState.Move, new MoveState());
fsm.Start(PlayerState.Idle);
fsm.Update();
```

规则：

- 状态逻辑放在状态类，MonoBehaviour 只承担输入、视图或编排。
- 业务每帧驱动 `Update` / `FixedUpdate`。
- 编辑器监控历史由 Adapter 订阅 `FsmEditorHook` 维护，不在运行时状态机里写文件。
- 层级状态机使用 `HierarchicalSM<TEnum>`，修改时同步考虑普通 FSM 和 HSM。

## PoolKit

局部对象池使用 `SimplePoolKit<T>`：

```csharp
public sealed class Bullet
{
    public int Damage;

    public void Reset()
    {
        Damage = 0;
    }
}

var pool = new SimplePoolKit<Bullet>(
    () => new Bullet(),
    bullet => bullet.Reset(),
    initCount: 16);

var bullet = pool.Allocate();
pool.Recycle(bullet);
```

全局可回收对象使用 `SafePoolKit<T>`：

```csharp
public sealed class DamageText : IPoolable
{
    public bool IsRecycled { get; set; }
    public int Value;

    public void OnRecycled()
    {
        Value = 0;
    }
}

SafePoolKit<DamageText>.Instance.Init(initCount: 8, maxCount: 32);
var text = SafePoolKit<DamageText>.Instance.Allocate();
SafePoolKit<DamageText>.Instance.Recycle(text);
```

临时集合使用集合池：

```csharp
Pool.List<int>(list =>
{
    list.Add(1);
    list.Add(2);
});
```

规则：

- 高频对象复用优先 PoolKit，避免每帧 new。
- 热路径不写文件、不序列化 JSON。
- 需要调试统计时走 `PoolDebugger`、`PoolKit/state` snapshot 或命令桥，不把调试文件 I/O 放进 allocate/recycle。
- `PoolDebugger.EnableTracking` 记录 active/inactive 对象；`EnableEventHistory` 记录事件历史；`EnableStackTrace` 记录借出代码位置，成本最高，只在定位问题时开启。
- AI 优先读 `.yokiframe/engines/<engineId>/snapshots/PoolKit/state.json`，需要显式详情时调用 `PoolKit/get_workbench_snapshot`、`get_pool_detail` 或 `check_leak`。
- `check_leak` 只表示“当前仍借出对象”的候选列表，不等于真实内存泄漏。

## SingletonKit

纯 C# 单例：

```csharp
public sealed class AudioService : Singleton<AudioService>
{
    public override void OnSingletonInit()
    {
    }

    public void Play(string key)
    {
    }
}

AudioService.Instance.Play("click");
AudioService.Dispose();
```

需要 Unity 生命周期时使用 Unity Adapter 的 `MonoSingleton<T>`：

```csharp
public sealed class AudioRoot : MonoSingleton<AudioRoot>
{
    public override void OnSingletonInit()
    {
    }
}
```

Godot 侧使用 Godot Adapter 的 `GodotSingleton<T>`，推荐作为 Autoload 或场景根节点。

规则：

- Unity Mono 单例和 Godot Node 单例留在 Adapter/Runtime 或项目引擎侧代码，不把引擎依赖放进 Base。
- 命令桥只显示已创建并登记到 `SingletonRegistry` 的实例，不做全项目反射扫描。
- `Dispose()`、Unity `OnDestroy()` 或 Godot `_ExitTree()` 后记录会标记 `isAlive=false`，用于生命周期诊断。
- AI 优先读 `SingletonKit/state` snapshot，需要详情时调用 `SingletonKit/get_singleton_detail`。

## Unity Adapter 辅助入口

Unity Adapter 的公共命名空间是 `YokiFrame.Unity`。业务运行时代码需要在 Unity 数学类型和 YokiFrame 跨引擎数学类型之间转换时，使用 Adapter 提供的扩展方法，不在调用点手写字段映射：

```csharp
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

var bounds = new Bounds(Vector3.zero, Vector3.one * 1000f).ToYokiBounds();
var octree = SpatialKit.CreateOctree<MySpatialEntity>(bounds);

var position = transform.position.ToYokiVector3();
mIndex.QueryRadius(position, sensor.Range, mQueryBuffer);
```

当前 Unity Adapter 提供 `Vector2` / `YokiVector2`、`Vector3` / `YokiVector3`、`Rect` / `YokiRect`、`Bounds` / `YokiBounds` 双向转换。转换 helper 位于 Unity Adapter，Core Runtime 仍不引用 `UnityEngine`。

Unity Editor UI Toolkit 模板、图标和样式服务也位于 `YokiFrame.Unity`。自定义 Inspector 或 EditorWindow 中使用：

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.Unity;
using static YokiFrame.Unity.YokiFrameUIComponents;

public sealed class MyInspector : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        YokiStyleService.Apply(root, YokiStyleProfile.CoreOnly);
        root.style.marginTop = Spacing.SM;

        root.Add(CreateModernToggle("启用", true, value => { }));
        return root;
    }
}
#endif
```

`YokiStyleService`、`YokiStyleProfile`、`KitIcons` 来自 `YokiFrame.Unity`；`Spacing`、`Colors`、`Radius`、`CreateModernToggle()` 等来自 `YokiFrameUIComponents`，裸用时需要 `using static YokiFrame.Unity.YokiFrameUIComponents;`。2.0 不再把这套 Editor 工具入口声明在 `YokiFrame.EditorTools`。

从 1.0 迁移时，先读 `Assets/YokiFrame/TauriRuntime~/dist/docs/quick-start.md` 的迁移速查。`UIPanel.Data`、SceneKit 事件、YooInit、AudioKit、SaveKit 和 Unity UI Toolkit 的 2.0 对应关系都以那里的实际 API 为准。

## ResKit

ResKit 的公开入口保持统一静态 API，真正加载后端由引擎 Adapter 注入：

```csharp
var handle = ResKit.LoadAsset<MyConfig>("Configs/GameConfig");
var config = handle.Asset;

handle.Release();
```

默认异步加载使用 `Task<T>` / `CancellationToken`；启用 `YOKIFRAME_UNITASK_SUPPORT` 后，同一套 Base API 会直接切换为 `UniTask<T>`：

```csharp
var handle = await ResKit.LoadAssetAsync<MyConfig>("Configs/GameConfig", token);
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

Unity 和 Godot 调用侧都直接使用 `YokiFrame.ResKit`。Unity 项目安装 UniTask 后，`DependencyDefineService` 会自动维护 `YOKIFRAME_UNITASK_SUPPORT` 宏；此时 `LoadAsync` / `LoadAssetAsync` / `LoadRawAsync` / `LoadRawTextAsync` 会直接返回 `UniTask<T>`，未启用宏时回退为 `Task<T>`。

自定义后端通过 `IResourceProvider` 接入：

```csharp
ResKit.SetProvider(new MyResourceProvider());
```

Provider 如果同时实现 `IRawResourceProvider` 和 `IResSceneBackend`，raw 文件读取和 SceneKit 默认场景加载会自动跟随当前 ResKit Provider。内置 `UnityResourceProvider` 和 `YooAssetResourceProvider` 已经同时覆盖普通资源、raw 文件和场景加载；UIKit 默认 `DefaultPanelLoader` 也通过 `ResKit.LoadAsset<GameObject>()` 加载面板。因此切换 YooAsset 时只需要：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
```

不要再额外要求用户调用 `SceneKit.SetBackend()`。UIKit 不再提供 YooAsset 专用初始化入口；除非项目确实要显式覆盖场景系统或面板加载策略，否则只切换 ResKit Provider。

原始文件通过统一 ResKit API 读取，Unity 和 Godot 调用侧保持一致：

```csharp
var bytes = ResKit.LoadRaw("Configs/GameConfig");
var text = ResKit.LoadRawText("Configs/GameConfig");
var asyncBytes = await ResKit.LoadRawAsync("Configs/GameConfig", token);
```

Unity Adapter 默认使用 `Unity.Resources`，raw 读取基于 `TextAsset`，场景加载基于 Unity `SceneManager`。Godot Adapter 默认使用 `Godot.ResourceLoader`，raw 读取基于 `FileAccess`。项目需要 YooAsset、Addressables 或 Godot 第三方资源插件时，实现 `IResourceProvider`；需要支持 raw 文件时同时实现 `IRawResourceProvider`；需要 SceneKit 默认跟随后端时同时实现 `IResSceneBackend`，再调用 `ResKit.SetProvider(customProvider)`。

规则：

- Base 层的 ResKit API 只依赖 `IResourceProvider` / `IRawResourceProvider` / `IResSceneBackend`，不直接引用 Unity、Godot 或具体资源库；UniTask 只作为 `YOKIFRAME_UNITASK_SUPPORT` 下的异步返回类型。
- `Load<T>()` 适合直接取资源，`LoadAsset<T>()` 返回带引用计数的 `ResHandle<T>`。
- `ResKit.LoadAsync<T>()` / `LoadRawAsync()` 在 `YOKIFRAME_UNITASK_SUPPORT` 启用时返回 `UniTask<T>`，否则返回 `Task<T>`。
- `LoadRaw()` 返回 bytes，`LoadRawText()` 返回文本；1.x 的 `LoadRawFileData()` / `LoadRawFileText()` raw 别名已移除。
- `Release(handle)` 会维护引用计数和卸载历史；Resources 后端实际资源生命周期仍由 Unity 管理。
- `ResKit.EnableLoadLocationTracking` 会采集 Load 调用位置，默认关闭；开启后只影响新加载资源。
- 监控页和 AI 查询优先读 `ResKit/state` snapshot，显式诊断时调用 `ResKit/get_workbench_snapshot`、`diagnose_resource`、`set_tracking`，不在加载热路径写文件。

## InputKit

InputKit 是纯 C# 输入门面，Unity 和 Godot 业务代码都使用同一个静态入口读取动作状态、输入缓冲和上下文栈。具体按键、手柄、触摸或项目输入插件都应隐藏在 `IInputBackend` 后端里。

```csharp
using YokiFrame;

InputKit.Update(unscaledTime);

if (InputKit.WasPressedThisFrame("Jump"))
{
    Jump();
}

float moveValue = InputKit.GetValue("Move");
```

输入缓冲用于容错窗口：

```csharp
InputKit.SetBufferWindow(160f);
InputKit.BufferInput("Dodge");

if (InputKit.ConsumeBufferedInput("Dodge"))
{
    Dodge();
}
```

上下文栈用于菜单、战斗、对话等输入域切换：

```csharp
var gameplay = new InputContext(
    "Gameplay",
    enabledActionMaps: new[] { "Gameplay" });

var menu = new InputContext(
    "Menu",
    priority: 100,
    enabledActionMaps: new[] { "UI" },
    blockedActions: new[] { "Attack", "Dodge" },
    blockAllLowerPriority: true);

InputKit.RegisterContext(gameplay);
InputKit.RegisterContext(menu);
InputKit.PushContext("Gameplay");
InputKit.PushContext("Menu");
InputKit.PopContext();
```

规则：

- `InputKit.SetBackend()` 由 Unity/Godot Adapter 或项目启动器安装，业务代码通常不直接 new 宿主后端。
- 宿主后端在 `Poll(IInputStateWriter writer)` 中写入动作状态；业务侧继续调用 `InputKit.IsPressed()`、`WasPressedThisFrame()`、`WasReleasedThisFrame()`、`GetValue()`。
- `InputKit.Update()` 是帧边沿清理和后端轮询入口，不要在同一帧重复调用多次。
- ActionMap 切换通过 `InputContext` 或 `InputKit.EnableActionMaps()` 传给后端，Unity/Godot 差异留在 `IInputBackend.SetEnabledActionMaps()`。
- 命令桥只暴露 `InputKit/state`、`stats`、`list_actions`、`list_contexts`、`get_workbench_snapshot` 这类只读诊断；不要通过文件桥注入按键、模拟输入、修改绑定或切换上下文。

## UIKit

UIKit 当前是 Unity UI 实现与 `IUIBackend` 兼容层并存的面板系统。业务代码仍通过 `UIKit` 静态入口调用；Unity 的 GameObject / Canvas / DOTween 细节仍在 UIKit runtime 中，后续跨引擎拆分时应先把纯契约和宿主实现分离。

```csharp
using YokiFrame;

var menu = UIKit.OpenPanel<MenuPanel>(UILevel.Common, data: null, tag: "main");
UIKit.PushPanel(menu, "Main", hidePreLevel: true);
UIKit.PopPanel(showPreLevel: true, autoClose: true);
```

规则：

- `UIKit` 作为业务统一入口，不在业务层散落宿主 UI API。
- `IUIBackend` 由 Unity Adapter 或项目启动器安装；Godot 接入需要独立后端后再声明完整支持。
- `IPanel` 只暴露 `PanelName`、`Level`、`State`、`Tag`、`Data`。
- `UILevel`、`PanelState` 和面板栈语义应保持宿主无关。
- 当前 Unity 的 GameObject / Canvas 细节仍在 runtime 实现内；新增能力不要继续扩大这部分耦合。
- 默认面板加载器走 `ResKit.LoadAsset<GameObject>()`。如果 ResKit Provider 切到 YooAsset，UIKit 默认面板加载也会跟随 YooAsset；不要再为 YooAsset 面板引入平行 loader 或独立初始化入口。
- 命令桥只暴露 `UIKit/state`、`stats`、`list_panels`、`list_stacks`、`get_workbench_snapshot` 这类只读诊断；不要通过 `.yokiframe` 打开、关闭、显示、隐藏、压栈或弹栈面板。
- AI 排查 UI 状态时优先读 `UIKit/state` snapshot；只有用户要求显式刷新或拆分列表时才发命令。

## SpatialKit

SpatialKit 是纯 C# 空间索引工具，Unity 和 Godot 业务代码都使用同一个静态入口创建索引，不直接绑定 Unity `Vector3`、`Bounds` 或 Godot 节点类型。

```csharp
public sealed class EnemySpatialEntity : ISpatialEntity
{
    public int SpatialId { get; }

    public YokiVector3 Position { get; private set; }

    public EnemySpatialEntity(int spatialId, YokiVector3 position)
    {
        SpatialId = spatialId;
        Position = position;
    }

    public void MoveTo(YokiVector3 position)
    {
        Position = position;
    }
}

var grid = SpatialKit.CreateHashGrid<EnemySpatialEntity>(
    cellSize: 2f,
    plane: SpatialPlane.XZ);

var enemy = new EnemySpatialEntity(1, new YokiVector3(0f, 0f, 3f));
grid.Insert(enemy);

var results = new List<EnemySpatialEntity>(16);
grid.QueryRadius(new YokiVector3(0f, 0f, 0f), 5f, results);
```

固定区域查询可使用四叉树或八叉树：

```csharp
var quadtree = SpatialKit.CreateQuadtree<EnemySpatialEntity>(
    new YokiRect(-100f, -100f, 200f, 200f),
    maxDepth: 8,
    maxEntitiesPerNode: 8,
    plane: SpatialPlane.XZ);

var octree = SpatialKit.CreateOctree<EnemySpatialEntity>(
    new YokiBounds(YokiVector3.Zero, new YokiVector3(200f, 80f, 200f)));
```

规则：

- `SpatialKit.CreateHashGrid<T>()` 适合分布较均匀、动态移动频繁的实体。
- `CreateQuadtree<T>()` 适合 2D 或 2.5D 投影查询，`SpatialPlane.XZ` 用于常见 3D 地面平面，`SpatialPlane.XY` 用于 2D 平面。
- `CreateOctree<T>()` 适合完整 3D 空间查询。
- 实体必须实现 `ISpatialEntity`，`SpatialId` 在同一个索引内应稳定且唯一。
- 实体移动后调用 `Update(entity)`；批量移动使用 `UpdateBatch()`。
- 查询结果写入调用方传入的 `List<T>`，高频查询时复用列表，避免每帧分配。
- 命令桥只暴露 `SpatialKit/state`、`stats`、`list_indexes`、`get_workbench_snapshot` 这类只读诊断；不要通过文件桥插入、更新、删除或查询实体。

## ActionKit

ActionKit 当前位于 `Assets/YokiFrame/Tools/ActionKit`，已有纯 C# Runtime 和 Tests。Unity/Godot 适配器负责在宿主帧循环中调用调度器 tick。

```csharp
var seq = ActionKit.Sequence()
    .Delay(0.5f)
    .Callback(() => Debug.Log("done"));
```

规则：

- ActionKit 核心不依赖 Unity PlayerLoop；Unity 侧由 `UnityActionKitInstaller` 驱动，Godot 侧由 `GodotActionKitInstaller` 在 `_Process` 中驱动。
- 复用内部对象池，避免临时 Action 大量分配。
- 新增 Action 时补充对应 Tests，并检查 Godot package compatibility。

## Tauri 编辑器

Unity 菜单：

```text
YokiFrame/Editor UI/Launch
YokiFrame/Editor UI/Close
YokiFrame/Editor UI/Restart
YokiFrame/Editor UI/Build Tauri Binary
YokiFrame/Editor UI/Package Binary (Release)
```

EventKit 页面：

- 实时监控展示运行时事件、扫描拓扑和最近事件流。
- 代码扫描调用 Rust `scan_eventkit_code`。
- “排除 Editor”用于过滤 Editor 目录，避免编辑器代码污染运行时关系判断。
- 点击代码位置通过宿主默认代码编辑器打开并聚焦。

FsmKit 页面：

- 左侧活动状态机列表优先 telemetry/snapshot。
- 详情、状态流图和历史通过 `get_workbench_snapshot` 或 fallback 命令补齐。
- 高频刷新只更新必要区域，不应阻塞滚动或点击。
