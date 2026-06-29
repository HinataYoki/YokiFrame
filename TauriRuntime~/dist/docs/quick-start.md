# 快速上手

## 1. 安装 AI Skill

先让 AI 知道当前项目里的 YokiFrame 规则。打开工作台后进入 `System` 页面，找到 `安装Skill` 卡片。

| 操作 | 说明 |
|---|---|
| 打开工作台 | Unity 菜单 `YokiFrame/Editor UI/Launch`，或 `Ctrl+E`。 |
| 选择 Skill | 默认安装 `yokiframe`、`yokiframe-editor`、`yokiframe-command-bridge`。 |
| 选择目标 | Codex、Claude Code、Cursor、Windsurf、GitHub Copilot、Agents 或项目内自定义目录。 |
| 点击安装 | 工作台会把包内 Skill 复制到目标目录。 |

安装后，AI 可以读取框架规则、理解 Kit API，并通过 YokiFrame 的诊断通信查询运行时状态。

## 2. 初始化 Unity 项目

Unity 项目建议在启动脚本里调用统一入口。

```csharp
using UnityEngine;
using YokiFrame;

public sealed class GameStartup : MonoBehaviour
{
    private void Awake()
    {
        YokiFrameKit.Initialize(YokiFrameEngine.Unity);
    }
}
```

如果希望使用 Unity 生命周期外壳，也可以放置或创建 `UnityBootstrap`，它会负责初始化、Tick 和 Shutdown。

只使用 `EventKit`、`FsmKit`、`PoolKit` 或纯 C# 单例时不强制初始化；只要用到资源、音频、输入、场景、存档或 UI 后端，就先初始化。

Unity Adapter 类型额外使用：

```csharp
using YokiFrame.Unity;
```

## 3. 初始化 Godot 项目

Godot 安装器会创建插件入口和 autoload。启用插件后，`GodotBootstrap` 会在运行时初始化 YokiFrame。

自动入口：

```text
addons/yokiframe/plugin.cfg
addons/yokiframe/plugin.gd
addons/yokiframe/package/YokiFrame/
```

手动初始化时调用同一套统一入口：

```csharp
using YokiFrame;

YokiFrameKit.Initialize(YokiFrameEngine.Godot);
```

Godot Adapter 类型额外使用：

```csharp
using YokiFrame.Godot;
```

## 4. 引用命名空间

业务代码通常只需要：

```csharp
using YokiFrame;
```

跨引擎业务逻辑不要直接依赖 Unity 或 Godot 类型。需要宿主能力时，通过 Provider / Backend / Adapter 接入。

## 5. EventKit 发送事件

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

public sealed class EventDemo : MonoBehaviour
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

注册和注销必须成对出现。

## 6. FsmKit 跑状态机

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

状态机不会自动更新。你要在宿主生命周期里调用 `Update()`、`FixedUpdate()` 或 `CustomUpdate()`。

## 7. PoolKit 复用对象

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

var pool = new SimplePoolKit<BulletToken>(
    factoryMethod: () => new BulletToken(),
    resetMethod: token => token.Reset(),
    initCount: 16);

var token = pool.Allocate();
token.Damage = 10;
pool.Recycle(token);
```

临时集合优先使用 action 版本：

```csharp
Pool.List<int>(list =>
{
    list.Add(1);
    list.Add(2);
    Use(list);
});
```

## 8. ResKit 加载资源

默认 Unity Provider 走 `Resources`。需要明确释放时用句柄。

```csharp
using UnityEngine;
using YokiFrame;

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

接入 YooAsset 或项目资源系统时只替换 Provider：

```csharp
ResKit.SetProvider(new ProjectResourceProvider());
```

SceneKit 和 UIKit 默认也会跟随当前 ResKit Provider。

## 9. ActionKit 执行动作序列

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

拥有者销毁时取消仍在运行的动作，避免回调访问已销毁对象。

## 10. 工作台检查

打开工作台后先看：

| 页面 | 看什么 |
|---|---|
| System | 引擎连接是否在线，框架通信是否健康，AI Skill 是否已安装。 |
| 对应 Kit 页面 | 运行时状态是否有数据，列表、历史、引用计数或栈是否符合预期。 |
| Runtime Log | 启动、连接、命令或 Kit 状态异常。 |

工作台是给人看的调试入口；AI 诊断通信用于让 AI 读取相同框架状态并辅助分析。

## 11. API 速查

### 命名空间

| 命名空间 | 内容 |
|---|---|
| `YokiFrame` | Core Kit、Tool Kit、跨引擎接口和基础数据类型。 |
| `YokiFrame.Unity` | Unity Adapter、`UnityBootstrap`、`UnityResourceProvider`、`MonoSingleton<T>` 等。 |
| `YokiFrame.Godot` | Godot Adapter、`GodotBootstrap`、`GodotResourceProvider`、`GodotSingleton<T>` 等。 |

### Core Kit

| Kit | 入口 | 常用成员 |
|---|---|---|
| Architecture | `Architecture<T>` | `Interface`、`Register<T>()`、`GetService<T>()` |
| EventKit | `EventKit.Type` | `Register<T>()`、`UnRegister<T>()`、`Send<T>()` |
| EventKit | `EventKit.Enum` | `Register<TEnum>()`、`Register<TEnum,TArgs>()`、`Send()`、`UnRegister()` |
| FsmKit | `FSM<TEnum>` | `Add()`、`Start()`、`Change()`、`Update()`、`End()`、`Clear()` |
| PoolKit | `SimplePoolKit<T>` | 构造函数、`Allocate()`、`Recycle()` |
| PoolKit | `Pool` | `List<T>()`、`Dictionary<TKey,TValue>()`、`Set<T>()` |
| ResKit | `ResKit` | `SetProvider()`、`Load<T>()`、`LoadAsset<T>()`、`LoadRawText()`、`ClearAll()` |
| SingletonKit | `Singleton<T>` / `SingletonKit<T>` | `Instance`、`Dispose()`、`OnSingletonInit()` |

### Tool Kit

| Kit | 入口 | 常用成员 |
|---|---|---|
| ActionKit | `ActionKit` | `Sequence()`、`Parallel()`、`Repeat()`、`Delay()`、`Callback()` |
| AudioKit | `AudioKit` | `SetBackend()`、`PlaySfx()`、`PlayMusic()`、`Stop()`、`SetGlobalVolume()` |
| InputKit | `InputKit` | `Update()`、`IsPressed()`、`WasPressedThisFrame()`、`GetValue()`、`PushContext()` |
| LocalizationKit | `LocalizationKit` | `SetProvider()`、`SetLanguage()`、`Get()`、`GetPlural()` |
| SaveKit | `SaveKit` | `CreateSaveData()`、`Save()`、`Load()`、`GetAllSlots()` |
| SceneKit | `SceneKit` | `LoadSceneAsync()`、`PreloadSceneAsync()`、`UnloadSceneAsync()` |
| SpatialKit | `SpatialKit` | `CreateHashGrid<T>()`、`CreateQuadtree<T>()`、`CreateOctree<T>()` |
| UIKit | `UIKit` | `OpenPanel<T>()`、`ClosePanel<T>()`、`PushPanel()`、`PopPanel()` |
| TableKit | 生成代码 `TableKit` | `RuntimePathPattern`、`Init()`、`Tables`、`Reload()` |

### 常用接口

| 接口 | 用途 |
|---|---|
| `IResourceProvider` / `IRawResourceProvider` | 资源和 raw 文件读取后端。 |
| `IAudioBackend` | 音频播放、停止、音量和活跃 voice。 |
| `IInputBackend` | 输入轮询、设备状态和 ActionMap。 |
| `ISceneBackend` | 场景加载、预加载和卸载。 |
| `IUIBackend` | 面板打开、显示、隐藏和关闭。 |
| `ISaveStorage` | 存档读写、删除和槽位扫描。 |

## 下一步

| 想做什么 | 继续读 |
|---|---|
| 做模块通信 | `EventKit` |
| 做角色或流程状态 | `FsmKit` |
| 接资源系统 | `ResKit` |
| 做 UI 面板 | `UIKit` |
| 做配置表 | `TableKit` |
| 看工作台怎么用 | 对应 Kit 文档的 `工作台诊断` |
