# YokiFrame 高频用法模式

本文件提供 AI 编写 YokiFrame 业务代码时可直接照抄的最小骨架。骨架签名已对照 `Documentation~/Api` 与源码核实；每个骨架只展示最小路径，完整约束、失败语义和更多重载以所标注的 Kit 主页面为准，不要凭记忆扩展 API。

## 通用约束（写任何 Kit 代码前先过一遍）

- 业务代码只依赖 Core 或当前 Tool 的公开 API，不引用宿主类型；宿主差异交给 Adapter。
- 每个事件订阅、资源 handle、状态机、动作 controller、异步工作都要有明确 owner；owner 退出时注销、释放或取消。
- Runtime 代码保持 C# 9.0 兼容语法。
- Unity 对象判空用 `== default` / `!= default`，不用 `?.` / `??`；纯 C# 对象可以正常使用。
- 热路径（`Update`、Tick、池循环、协议轮询）禁 LINQ、闭包分配、装箱和临时集合。
- 显式注入的 Provider/Backend 始终优先；默认后端只在第一次真实业务调用时惰性创建，读取和诊断调用不得隐式创建业务后端。
- TableKit 未生成前项目不存在对应 Runtime 类型；不要提前引用。

## 任务 → Kit 速查

| 要做什么 | 入口 | 详细文档（Documentation~/Api/ 下） |
|---|---|---|
| 组织服务、模型、系统 | `Architecture<T>` | `01-Architecture/Architecture.md` |
| 跨模块通知 | `EventKit.Type` / `EventKit.Enum` | `02-Core/EventKit.md` |
| 业务状态机 | `FSM<TEnum>` | `02-Core/FsmKit.md` |
| 对象复用 | `PoolKit` / `PoolKit.Shared` | `02-Core/PoolKit.md` |
| 资源加载 | `ResKit` | `02-Core/ResKit.md` |
| 动作/流程编排 | `ActionKit` | `03-Tool/ActionKit.md` |
| 纯 C# 单例 | `Singleton<T>` / `SingletonKit<T>` | `02-Core/SingletonKit.md` |
| 存档读写 | `SaveKit`、`SaveTarget` | `03-Tool/SaveKit.md` |
| Unity UI 面板 | `UIKit`、`UIPanel`（仅 Unity） | `03-Tool/UIKit.md` |
| 音频播放 | `AudioKit`、`AudioVoiceHandle` | `03-Tool/AudioKit.md` |
| 本地化 | `LocalizationKit`、`ILocalizationProvider` | `03-Tool/LocalizationKit.md` |
| 空间查询 | `SpatialKit`、`ISpatialIndex<T>` | `03-Tool/SpatialKit.md` |
| 场景切换 | `SceneKit`、`SceneHandler` | `03-Tool/SceneKit.md` |
| 配表读取 | TableKit 生成产物 | `03-Tool/TableKit.md`；配表 AI 走 `tablekit-luban.md` |

## 架构与服务（Architecture）

来源：包根 `README.md` 快速开始、`01-Architecture/Architecture.md`。

```csharp
public sealed class SessionService
{
    public void StartSession() { }
}

public sealed class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit() => Register<SessionService>(new SessionService());
}

// 首次访问 Interface 时创建架构并初始化服务
GameArchitecture.Interface
    .GetService<SessionService>()
    .StartSession();
```

## 事件（EventKit）

来源：`02-Core/EventKit.md`。

```csharp
public readonly struct DamageTaken
{
    public DamageTaken(int amount) { Amount = amount; }
    public int Amount { get; }
}

LinkUnRegister<DamageTaken> link =
    EventKit.Type.Register<DamageTaken>(_ => { });
EventKit.Type.Send(new DamageTaken(10));
link.UnRegister(); // 由订阅方 owner 在停用时注销，不要依赖 Clear()
```

- 新代码优先强类型 `TypeEvent`；固定协议信号用 `EnumEvent`；`StringEvent` 仅旧代码兼容。
- 对象内部生命周期事件用局部 `EasyEvent` / `EasyEvent<T>`，不进全局总线。
- 事件总线在宿主主线程使用；后台线程先切回主线程。

## 状态机（FsmKit）

来源：`02-Core/FsmKit.md`。

```csharp
public enum PlayerState { Idle, Run }

public sealed class IdleState : AbstractState<PlayerState, object>
{
    public IdleState(FSM<PlayerState> fsm, object blackboard)
        : base(fsm, blackboard) { }

    protected override void OnEnter() { }
}

FSM<PlayerState> fsm = new();
fsm.Add(PlayerState.Idle, new IdleState(fsm, new object()));
fsm.Start(PlayerState.Idle);
fsm.Update();     // 由宿主 Update / _Process 主动驱动；框架不会自动 Tick
fsm.Dispose();    // 由创建它的 owner 释放
```

## 对象池（PoolKit）

来源：`02-Core/PoolKit.md`。

```csharp
ObjectPool<Bullet> pool = PoolKit.Create<Bullet>(
    static () => new Bullet(),
    onRecycled: static bullet => bullet.Reset(),
    options: new PoolOptions(initialCount: 8, maxRetained: 32));

Bullet bullet = pool.Allocate();
pool.Recycle(bullet);
pool.Dispose();   // 池 owner 负责释放；之后借还会抛 ObjectDisposedException
```

- 类型可控时实现 `IPoolable` 并使用约定重载。
- 局部池由独占系统持有；跨系统共享用 `PoolKit.Shared` 按类型注册。
- 池只管理普通 C# 引用类型，不管 Unity `GameObject` 场景归属。

## 资源（ResKit）

来源：`02-Core/ResKit.md`。

```csharp
// 简单加载：try/finally 保证释放
ConfigAsset config = ResKit.Load<ConfigAsset>("Configs/Main");
try { Use(config); }
finally { ResKit.Release(config); }

// 需要独立所有权时使用 handle
using ResHandle<ConfigAsset> handle =
    ResKit.LoadAsset<ConfigAsset>("Configs/Main");
Use(handle.Asset);
```

- 自定义 Provider 必须在第一次资源调用前 `ResKit.SetProvider(...)` 显式注入。
- 场景流程由 SceneKit 编排，不走 ResKit 场景入口。

## 动作编排（ActionKit）

来源：`03-Tool/ActionKit.md`。

```csharp
IActionController flow = ActionKit.Sequence()
    .Callback(ShowLoading)
    .Delay(0.5f)
    .Condition(IsReady)
    .Lerp01(0.25f, SetProgress, OnFinished)
    .Start();

flow.Pause();
flow.Resume();
flow.Cancel();   // 仍运行的动作由创建它的业务 owner 调用 Cancel
```

- Start、Tick、暂停、恢复在同一宿主线程；跨线程只允许 `Cancel()`。
- UniTask / Unity Coroutine 分别进入对应 Integration / Adapter（`YOKIFRAME_UNITASK_SUPPORT`、`ActionKitUnityCoroutine`），不建第二套 Tick。

## 单例（SingletonKit）

来源：`02-Core/SingletonKit.md`。

```csharp
public sealed class SettingsService : Singleton<SettingsService>
{
    public override void OnSingletonInit() { }
}

SettingsService settings = SettingsService.Instance;
Singleton<SettingsService>.Dispose();   // 由明确 owner 在会话结束时调用
```

- 需要按依赖组织多个服务时优先 `Architecture<T>`，不要用单例替代注入。

## 存档（SaveKit）

来源：`03-Tool/SaveKit.md`。

```csharp
var data = SaveKit.CreateSaveData();
data.RegisterModule(new PlayerSaveModule { Level = 3 }, "game.player");
SaveKit.Save(SaveTarget.Slot(0), data, "Chapter 1");

SaveLoadResult result = SaveKit.TryLoad(SaveTarget.Slot(0));
if (!result.Succeeded)
{
    LogKit.Warning("Load failed: " + result.Status);
    return;
}
PlayerSaveModule player = result.Data.GetModule<PlayerSaveModule>("game.player");
```

- 玩家存档用 `SaveTarget.Slot(n)`，全局设置用 `SaveTarget.Global`；不要用负数槽位模拟 Global。
- 需要区分“没有存档”和“存档损坏”时用 `TryLoad`，不要用失败返回 null 的 `Load`。

## UI 面板（UIKit，仅 Unity）

来源：`03-Tool/UIKit.md`。

```csharp
// Panel Prefab 根节点挂 UIPanel 派生组件；默认 location: Art/UIPrefab/<TypeName>
public sealed class MainMenuPanel : UIPanel
{
    protected override void OnInit(IUIData data = null) { }   // 实例物化时一次
    protected override void OnOpen(IUIData data = null) { }    // 每次打开
    protected override void OnClose() { }                      // 每轮关闭，释放本轮订阅
}

var panel = UIKit.OpenPanel<MainMenuPanel>(
    level: UILevel.Common,
    data: new MainMenuData("Yoki"),
    cachePolicy: PanelCachePolicy.Reusable);
```

- 每种 Panel 类型最多一个实例；Root 创建后不可替换。
- 项目定制 Root 用 Prefab Variant + `UIKit.SetRootPrefab`，不改包内模板。
- Godot 没有 UIKit；不要为 Godot 创建 UIKit 代码。

## 音频与本地化

这两个 Kit 的入口形态依赖项目配置（音频索引类、本地化源类型），骨架以文档为准：

- 音频：`03-Tool/AudioKit.md`；保留完整 `AudioVoiceHandle`，索引生成用 `yoki audio index`（见 `yokiframe-cli`）。
- 本地化：`03-Tool/LocalizationKit.md`；Provider 显式注入优先，查询入口见对应文档。

## 完成后验证

1. Unity 项目：让宿主完成编译（Unity Editor 或项目已配置的编译自动化）并确认 Console 无 Error。
2. Godot 项目：编译对应 .NET 工程确认无错误。
3. 需要运行态证据时转入 `yokiframe-cli`：只读命令（`telemetry read` / `snapshot read` / `harness catalog`）核对。
4. 发现框架行为与文档不一致时向用户报告差异；修改包内文档或 `kit-index.md` 属于 YokiFrame 框架开发者职责，用户项目 AI 不执行。
