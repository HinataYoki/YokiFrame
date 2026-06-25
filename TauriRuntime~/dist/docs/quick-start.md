# 快速上手

本页覆盖运行时代码接入和常用 API 速查。目标是让你在一个 Unity 项目里完成最小可用路径：初始化运行时适配器、发送一个事件、启动一个状态机、复用对象、加载资源，并能在同一页快速查到后续会用到的核心入口。

## 1. 引用命名空间

大多数核心 Kit 位于 `YokiFrame`：

```csharp
using YokiFrame;
```

Unity 运行时适配类型位于：

```csharp
using YokiFrame.Unity;
```

ActionKit 也使用统一命名空间：

```csharp
using YokiFrame;
```

如果项目使用 asmdef，运行时代码通常引用这些程序集：

| 程序集 | 用途 |
|--------|------|
| `YokiFrame` | EventKit、FsmKit、PoolKit、ResKit、SingletonKit、AudioKit 和接口。 |
| `YokiFrame.Unity.Runtime` | UnityBootstrap、UnityResourceProvider、MonoSingleton、UnityAudioKitBackend。 |
| ActionKit 兼容程序集 | 代码命名空间仍是 `YokiFrame`。 |

## 2. 初始化 Unity 运行时适配器

如果你要使用默认 Unity 资源和音频后端，先确保项目启动时创建 `UnityBootstrap`。它会调用 `ResKit.SetProvider(new UnityResourceProvider())` 和 `AudioKit.SetBackend(new UnityAudioKitBackend())`。

```csharp
using UnityEngine;
using YokiFrame.Unity;

public sealed class GameStartup : MonoBehaviour
{
    private void Awake()
    {
        _ = UnityBootstrap.Instance;
    }
}
```

只使用 `EventKit`、`FsmKit`、`PoolKit` 或纯 C# 单例时，不强制依赖 `UnityBootstrap`。但只要用到 `ResKit` 或 `AudioKit` 默认 Unity 后端，就应先完成这一步。

## 3. 写一个 EventKit 事件

强类型事件适合绝大多数业务通知。事件 payload 建议使用不可变结构。

```csharp
using UnityEngine;
using YokiFrame;

public readonly struct PlayerDiedEvent
{
    public readonly string PlayerName;

    public PlayerDiedEvent(string playerName)
    {
        PlayerName = playerName;
    }
}

public sealed class PlayerEventDemo : MonoBehaviour
{
    private void OnEnable()
    {
        EventKit.Type.Register<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnDisable()
    {
        EventKit.Type.UnRegister<PlayerDiedEvent>(OnPlayerDied);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            EventKit.Type.Send(new PlayerDiedEvent("Player"));
        }
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        Debug.Log(evt.PlayerName + " died");
    }
}
```

要点：

- `Register` 和 `UnRegister` 成对出现。
- 监听对象销毁或禁用时及时注销。
- 新代码优先用 `EventKit.Type` 或 `EventKit.Enum`，不要新增 String 事件。

## 4. 写一个 FsmKit 状态机

`AbstractState<TEnum,TBlack>` 构造函数需要传入所属 FSM 和黑板对象。状态机不会自动更新，业务脚本要在 `Update()` 中驱动它。

```csharp
using UnityEngine;
using YokiFrame;

public sealed class PlayerFsmDemo : MonoBehaviour
{
    private enum PlayerState
    {
        Idle,
        Run
    }

    private FSM<PlayerState> mFsm;

    private void Awake()
    {
        mFsm = new FSM<PlayerState>("PlayerFSM");
        mFsm.Add(PlayerState.Idle, new IdleState(mFsm, this));
        mFsm.Add(PlayerState.Run, new RunState(mFsm, this));
        mFsm.Start(PlayerState.Idle);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            mFsm.Change(PlayerState.Run);
        }

        mFsm.Update();
    }

    private void OnDestroy()
    {
        ((IState)mFsm).Dispose();
    }

    private sealed class IdleState : AbstractState<PlayerState, PlayerFsmDemo>
    {
        public IdleState(FSM<PlayerState> fsm, PlayerFsmDemo owner) : base(fsm, owner)
        {
        }

        protected override void OnEnter()
        {
            Debug.Log("enter idle");
        }
    }

    private sealed class RunState : AbstractState<PlayerState, PlayerFsmDemo>
    {
        public RunState(FSM<PlayerState> fsm, PlayerFsmDemo owner) : base(fsm, owner)
        {
        }

        protected override void OnEnter()
        {
            Debug.Log("enter run");
        }
    }
}
```

要点：

- `Change()` 只有在状态机 `Running` 时生效。
- `OnCondition()` 返回 `false` 时不会切换。
- 拥有者销毁时调用 `((IState)fsm).Dispose()`，让状态进入清理流程。

## 5. 使用对象池

普通局部池使用 `SimplePoolKit<T>`：

```csharp
using YokiFrame;

public sealed class BulletToken
{
    public int Damage;

    public void Reset()
    {
        Damage = 0;
    }
}

private readonly SimplePoolKit<BulletToken> mBulletPool =
    new SimplePoolKit<BulletToken>(
        factoryMethod: () => new BulletToken(),
        resetMethod: token => token.Reset(),
        initCount: 16);

var token = mBulletPool.Allocate();
token.Damage = 10;
mBulletPool.Recycle(token);
```

临时集合使用 action 版本，正常执行完会自动清空并归还：

```csharp
Pool.List<int>(list =>
{
    list.Add(1);
    list.Add(2);
    Use(list);
});
```

## 6. 使用 ResKit 加载资源

如果已经创建 `UnityBootstrap`，默认 Provider 是 `UnityResourceProvider`，基于 Unity `Resources`。

```csharp
using UnityEngine;
using YokiFrame;

var icon = ResKit.Load<Sprite>("Sprites/Icon");
```

需要明确生命周期时使用句柄：

```csharp
var handle = ResKit.LoadAsset<Sprite>("Sprites/Icon");
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

如果没有设置 Provider，调用加载 API 会抛出 `InvalidOperationException`。自定义资源系统实现 `IResourceProvider` 后调用 `ResKit.SetProvider(provider)`。

## 7. 运行一个 ActionKit 序列

ActionKit 核心由宿主适配器驱动。Unity 项目由 `UnityActionKitInstaller` 接入 PlayerLoop，Godot 项目由 `GodotActionKitInstaller` 在 `_Process` 中 tick；调用 `Start()` 后返回控制器，可用于暂停、恢复和取消。

```csharp
using UnityEngine;
using YokiFrame;

public sealed class ActionDemo : MonoBehaviour
{
    private IActionController mController;

    private void Start()
    {
        mController = ActionKit.Sequence()
            .Callback(() => Debug.Log("start"))
            .Delay(0.5f)
            .Callback(() => Debug.Log("done"))
            .Start();
    }

    private void OnDestroy()
    {
        mController.Cancel();
    }
}
```

## 8. API 接口速查

下面只列运行时代码会直接使用的 API。内部工具协议、自动化诊断入口和非业务代码接口不在这里展开。

### 常用命名空间

```csharp
using YokiFrame;
using YokiFrame.Unity;
// TableKit 由 Luban 工具生成到项目 Scripts，通常是项目侧全局 TableKit 类型。
```

| 命名空间 | 内容 |
|----------|------|
| `YokiFrame` | EventKit、FsmKit、PoolKit、ResKit、SingletonKit、AudioKit、ActionKit、InputKit、SceneKit、SpatialKit、UIKit、基础类型和调试信息类型。 |
| `YokiFrame` | `IResourceProvider`、`IRawResourceProvider`、`IAudioBackend`、`IEngineObject`、`IEngineTime`、`IEngineLogger`、`ISerializationProvider` 等跨引擎接口。 |
| `YokiFrame.Unity` | Unity 运行时适配器：`UnityBootstrap`、`UnityResourceProvider`、`MonoSingleton<T>`、`UnityAudioKitBackend` 等。 |
| `YokiFrame.Godot` | Godot 运行时适配器：`GodotBootstrap`、`GodotResourceProvider`、`GodotSingleton<T>` 等。 |

### Kit API 总览

| Kit | 入口 | 常用成员 |
|-----|------|----------|
| EventKit | `EventKit.Type` | `Register<T>()`、`UnRegister<T>()`、`Send<T>()`、`Clear()` |
| EventKit | `EventKit.Enum` | `Register<TEnum>()`、`Register<TEnum,TArgs>()`、`UnRegister(...)`、`Send(...)`、`Clear()` |
| FsmKit | `FSM<TEnum>` | `Add()`、`Remove()`、`Start()`、`Change()`、`Update()`、`FixedUpdate()`、`CustomUpdate()`、`Suspend()`、`End()`、`Clear()` |
| FsmKit | `FSM<TEnum,TArgs>` | `Start(args)`、`Start(id,args)`、`Change(id,args)` |
| FsmKit | `HierarchicalSM<TEnum>` | `Add()`、`Remove()`、`Start()`、`Change(id, MachineState)`、`Update()`、`Suspend()`、`End()`、`Clear()` |
| PoolKit | `SimplePoolKit<T>` | 构造函数、`Allocate()`、`Recycle()` |
| PoolKit | `SafePoolKit<T>` | `Instance`、`Init()`、`Allocate()`、`Recycle()`、`MaxCacheCount` |
| PoolKit | `Pool` | `List<T>()`、`Dictionary<TKey,TValue>()`、`Set<T>()` |
| PoolKit | `ListPool<T>` / `DictPool<TKey,TValue>` / `SetPool<T>` | `Get()`、`Release()`、`Clear()` |
| ResKit | `ResKit` | `SetProvider()`、`GetProvider()`、`Load()`、`LoadAsset()`、`LoadAsync()`、`LoadAssetAsync()`、`LoadRaw()`、`LoadRawText()`、`LoadRawAsync()`、`LoadRawTextAsync()`、`Instantiate()`、`Release()`、`ClearAll()`、`ClearUnloadHistory()` |
| ResKit | `ResHandle<T>` | `Asset`、`Path`、`RefCount`、`Retain()`、`Release()`、`Dispose()` |
| TableKit 生成代码 | `TableKit` | Luban 环境启用后由编辑器生成到项目 Scripts：`RuntimePathPattern`、`SetBinaryLoader()`、`SetJsonLoader()`、`Init()`、`Tables`、`TablesEditor`、`Reload()`、`Clear()` |
| SingletonKit | `SingletonKit<T>` | `Instance`、`Dispose()` |
| SingletonKit | `Singleton<T>` | `Instance`、`Dispose()`、`OnSingletonInit()` |
| Unity Singleton | `MonoSingleton<T>` | `Instance`、`Dispose()`、`OnSingletonInit()`、`OnDestroy()` |
| Godot Singleton | `GodotSingleton<T>` | `Instance`、`Dispose()`、`OnSingletonInit()`、`_EnterTree()`、`_ExitTree()` |
| CodeGenKit (Editor) | `CodeGenKit` | `Root()`、`GenerateToString()`、`GenerateToFile()`、`WriteToFile()`、`Lines()` |
| ActionKit | `ActionKit` | `Sequence()`、`Parallel()`、`Repeat()`、`Delay()`、`DelayFrame()`、`NextFrame()`、`Lerp()`、`Callback()`、`Coroutine()`、`Task()` |
| ActionKit | `IActionController` | `Pause()`、`Resume()`、`TogglePause()`、`Cancel()`、`UpdateMode` |
| InputKit | `InputKit` | `SetBackend()`、`Update()`、`IsPressed()`、`WasPressedThisFrame()`、`WasReleasedThisFrame()`、`GetValue()`、`BufferInput()`、`ConsumeBufferedInput()`、`RegisterContext()`、`PushContext()`、`PopContext()` |
| InputKit | `IInputBackend` | `BackendName`、`CurrentDeviceType`、`IsGamepadConnected`、`Poll()`、`SetEnabledActionMaps()` |
| InputKit | `InputContext` | `ContextName`、`Priority`、`EnabledActionMaps`、`BlockedActions`、`BlockAllLowerPriority` |
| UIKit | `UIKit` | `SetBackend()`、`OpenPanel()`、`GetPanel()`、`ShowPanel()`、`HidePanel()`、`ClosePanel()`、`PushPanel()`、`PushOpenPanel()`、`PopPanel()`、`PeekPanel()`、`ClearStack()` |
| UIKit | `IUIBackend` | `BackendName`、`OpenPanel()`、`Show()`、`Hide()`、`Close()` |
| UIKit | `IPanel` | `PanelName`、`Level`、`State`、`Tag`、`Data` |
| AudioKit | `AudioKit` | `SetBackend()`、`Play()`、`PlayMusic()`、`PlaySfx()`、`Stop()`、`StopAll()`、`SetVolume()`、`GetVolume()`、`Update()` |
| SceneKit | `SceneKit` | `SetBackend()`、`LoadSceneAsync()`、`PreloadSceneAsync()`、`ActivatePreloadedScene()`、`UnloadSceneAsync()`、`UnloadUnusedAssets()` |
| SceneKit | `SceneHandler` | `SceneName`、`BuildIndex`、`State`、`Progress`、`IsSuspended`、`IsPreloaded`、`SceneData` |
| SpatialKit | `SpatialKit` | `CreateHashGrid<T>()`、`CreateQuadtree<T>()`、`CreateOctree<T>()` |
| SpatialKit | `ISpatialIndex<T>` | `Insert()`、`Remove()`、`Update()`、`QueryRadius()`、`QueryBounds()`、`QueryNearest()`、`Clear()` |
| SpatialKit | `ISpatialEntity` | `SpatialId`、`Position` |

### EventKit

```csharp
EventKit.Type.Register<PlayerDiedEvent>(OnPlayerDied);
EventKit.Type.Send(new PlayerDiedEvent("Player"));
EventKit.Type.UnRegister<PlayerDiedEvent>(OnPlayerDied);
```

```csharp
EventKit.Enum.Register<GameSignal, int>(GameSignal.ScoreChanged, OnScoreChanged);
EventKit.Enum.Send(GameSignal.ScoreChanged, 100);
EventKit.Enum.UnRegister<GameSignal, int>(GameSignal.ScoreChanged, OnScoreChanged);
```

注意：

- `Register<T>()` 返回 `LinkUnRegister<T>`，可以保存后调用 `UnRegister()`。
- `EventKit.String` 标记为 `Obsolete`，只用于旧代码兼容。
- `Clear()` 会清空对应通道所有监听器，谨慎在全局代码中调用。

### FsmKit

```csharp
var fsm = new FSM<PlayerState>("PlayerFSM");
fsm.Add(PlayerState.Idle, new IdleState(fsm, owner));
fsm.Start(PlayerState.Idle);
fsm.Change(PlayerState.Run);
fsm.Update();
```

`MachineState` 当前有三个值：

| 值 | 含义 |
|----|------|
| `End` | 停止。 |
| `Suspend` | 暂停。 |
| `Running` | 运行中。 |

`HierarchicalSM<TEnum>` 的 `Change(TEnum id)` 是接口占位，控制子状态时使用：

```csharp
hsm.Change(WorldState.Combat, MachineState.Running);
hsm.Change(WorldState.Exploration, MachineState.Suspend);
```

### PoolKit

```csharp
var pool = new SimplePoolKit<BulletToken>(
    () => new BulletToken(),
    token => token.Reset(),
    initCount: 16);

var token = pool.Allocate();
pool.Recycle(token);
```

```csharp
SafePoolKit<DamageTextToken>.Instance.Init(initCount: 8, maxCount: 32);
var token = SafePoolKit<DamageTextToken>.Instance.Allocate();
SafePoolKit<DamageTextToken>.Instance.Recycle(token);
```

集合池：

```csharp
Pool.List<int>(list =>
{
    list.Add(1);
    Use(list);
});
```

### ResKit

```csharp
ResKit.SetProvider(new UnityResourceProvider());

var handle = ResKit.LoadAsset<MyConfig>("Configs/GameConfig");
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

`Load<T>()` 返回资源对象；`LoadAsset<T>()` 返回可释放句柄。相同 `path + T` 会复用缓存并增加引用计数。句柄释放到 `RefCount == 0` 时，ResKit 会移除缓存并调用 Provider 的 `Release(asset)`。

Unity 和 Godot 都直接使用 `YokiFrame.ResKit`。安装 UniTask 后 Unity Editor 会自动启用 `YOKIFRAME_UNITASK_SUPPORT`，同名 `LoadAsync()` / `LoadAssetAsync()` / `LoadRawAsync()` / `LoadRawTextAsync()` 会直接返回 `UniTask<T>`；未启用宏时回退为 `Task<T>`。

raw 文件读取同样走 ResKit 统一 API：

```csharp
var bytes = ResKit.LoadRaw("Configs/GameConfig");
var text = ResKit.LoadRawText("Configs/GameConfig");
```

默认 Unity Provider 基于 `TextAsset`，默认 Godot Provider 基于 `FileAccess`。自定义 Provider 要支持 raw 读取时实现 `IRawResourceProvider`。

### TableKit

TableKit 是 Luban 配置表工作流的 Tauri 编辑器生成器，不是 YokiFrame Runtime Kit。Unity 和 Godot 都会自动检测 Luban 环境并维护 `YOKIFRAME_LUBAN_SUPPORT`；Tauri 页面读取 engine registry 的 `optionalDependencies.luban` 后展示环境状态和生成参数。

```csharp
TableKit.RuntimePathPattern = "Art/Table/{0}";
TableKit.Init();

var tables = TableKit.Tables;
```

生成的 `TableKit.cs` 和 Luban 生成代码一起落到项目 `Assets/Scripts/TableKit/` 或用户配置的代码输出目录。配置由 Tauri 前端保存在 `localStorage`，YokiFrame 包内不提供 TableKit Runtime 源码。默认运行时加载统一委托给 `YokiFrame.ResKit.LoadRaw()` / `LoadRawText()`；Unity、Godot、YooAsset 或自定义资源系统的差异应由 ResKit Provider 处理。

```csharp
TableKit.SetBinaryLoader(fileName => LoadBytesFromProjectRuntime(fileName));
TableKit.SetJsonLoader(fileName => LoadTextFromProjectRuntime(fileName));
```

Tauri TableKit 页面只读取 engine registry 的 `optionalDependencies.luban`，不会发送 TableKit 命令，也不会读取 `TableKit/state` snapshot。

### SingletonKit

```csharp
public sealed class ConfigService : ISingleton
{
    private ConfigService()
    {
    }

    public static ConfigService Instance => SingletonKit<ConfigService>.Instance;

    public void OnSingletonInit()
    {
    }
}
```

```csharp
public sealed class AudioConfig : Singleton<AudioConfig>
{
    public override void OnSingletonInit()
    {
    }
}
```

Unity 生命周期单例：

```csharp
public sealed class AudioRoot : MonoSingleton<AudioRoot>
{
    public override void OnSingletonInit()
    {
        DontDestroyOnLoad(gameObject);
    }
}
```

### ActionKit

```csharp
IActionController controller = ActionKit.Sequence()
    .Callback(OnStarted)
    .Delay(0.5f)
    .Callback(OnFinished)
    .Start();

controller.Pause();
controller.Resume();
controller.Cancel();
```

ActionKit 核心由宿主适配器驱动；Unity 侧注册到 PlayerLoop，Godot 侧在 `_Process` 中 tick。

### InputKit

```csharp
InputKit.Update(unscaledTime);

if (InputKit.WasPressedThisFrame("Jump"))
{
    Jump();
}

var moveValue = InputKit.GetValue("Move");
```

输入缓冲：

```csharp
InputKit.SetBufferWindow(160f);
InputKit.BufferInput("Dodge");

if (InputKit.ConsumeBufferedInput("Dodge"))
{
    Dodge();
}
```

上下文栈：

```csharp
InputKit.RegisterContext(new InputContext(
    "Gameplay",
    enabledActionMaps: new[] { "Gameplay" }));

InputKit.PushContext("Gameplay");
InputKit.PopContext();
```

Unity 和 Godot 都通过 `IInputBackend` 接入宿主输入系统。业务代码不要直接依赖 Unity Input、Godot InputMap 或项目输入插件来绕过 InputKit。

### UIKit

```csharp
UIKit.SetBackend(myUiBackend);

var menu = UIKit.OpenPanel<MenuPanel>(UILevel.Common, tag: "main");
UIKit.PushPanel(menu, "Main");
UIKit.PopPanel(showPreLevel: true, autoClose: true);
```

UIKit 当前仍包含 Unity UI runtime 实现。业务代码保持使用 `UIKit` 静态入口；面板实例实现 `IPanel`，运行时层级使用 `UILevel`。新增能力不要继续扩大 GameObject / Canvas / DOTween / YooAsset 这类 Unity 依赖，Godot 完整接入需要独立 `IUIBackend`。

### AudioKit

```csharp
AudioKit.SetBackend(new UnityAudioKitBackend());

int voiceId = AudioKit.PlaySfx("Audio/Click", volume: 0.8f);
AudioKit.SetVolume(AudioBus.Sfx, 0.7f);
AudioKit.Stop(voiceId);
```

`AudioKit.PlayMusic(path, loop, volume)` 默认使用 `AudioBus.Music`，`PlaySfx(path, volume, pitch)` 默认使用 `AudioBus.Sfx`。

### SceneKit

```csharp
SceneKit.LoadSceneAsync(
    "Gameplay",
    SceneLoadMode.Single,
    handler => { },
    progress => { });
```

预加载和卸载：

```csharp
var handler = SceneKit.PreloadSceneAsync("Battle", suspendAtProgress: 0.9f);
SceneKit.ActivatePreloadedScene(handler);
SceneKit.UnloadSceneAsync("Battle");
```

Unity 和 Godot 都通过 `ISceneBackend` 接入场景加载实现。业务代码不要直接依赖 Unity `SceneManager` 或 Godot 场景树来绕过 SceneKit。

### SpatialKit

```csharp
var grid = SpatialKit.CreateHashGrid<MySpatialEntity>(cellSize: 2f);
grid.Insert(new MySpatialEntity(1, new YokiVector3(0f, 0f, 0f)));

var results = new List<MySpatialEntity>();
grid.QueryRadius(YokiVector3.Zero, 4f, results);
```

四叉树和八叉树：

```csharp
var quadtree = SpatialKit.CreateQuadtree<MySpatialEntity>(
    new YokiRect(-50f, -50f, 100f, 100f),
    plane: SpatialPlane.XZ);

var octree = SpatialKit.CreateOctree<MySpatialEntity>(
    new YokiBounds(YokiVector3.Zero, new YokiVector3(100f, 100f, 100f)));
```

SpatialKit 使用 `YokiVector3`、`YokiRect` 和 `YokiBounds`，不直接绑定 Unity 或 Godot 类型。查询结果写入调用方传入的 `List<T>`，高频查询时应复用列表。

### Base 接口

| 接口 | 作用 |
|------|------|
| `IResourceProvider` | 资源加载、异步加载、实例化和释放。 |
| `IRawResourceProvider` | 原始资源文本、bytes 和文件路径读取。 |
| `IAudioBackend` | 音频播放、停止、音量总线和活跃声音查询。 |
| `IEngineObject` | 引擎对象的名称、激活状态、组件获取、销毁和实例化。 |
| `IEngineTime` | `DeltaTime`、`UnscaledDeltaTime`、`RealtimeSinceStartup`。 |
| `IEngineLogger` | 宿主日志输出，使用 `LogLevel` 区分级别。 |
| `ISerializationProvider` | `Serialize<T>()` / `Deserialize<T>()`。 |

这些接口主要用于 Kit 后端或项目基础设施。普通业务代码优先调用 Kit API，不需要直接依赖 Adapter 的实现细节。

## 9. 下一步读什么

| 目标 | 文档 |
|------|------|
| 理解服务化架构 | Architecture |
| 事件发送和注销细节 | EventKit 事件 |
| 状态生命周期和带参数状态 | FsmKit 状态机 |
| 对象池选择和回收规则 | PoolKit 对象池 |
| Provider、自定义资源后端和引用计数 | ResKit 资源 |
| 纯 C#、Unity、Godot 单例 | SingletonKit 单例 |
| 延迟、并行、重复动作组合 | ActionKit 动作 |
| 音效、音乐和音量总线 | AudioKit 音频 |
