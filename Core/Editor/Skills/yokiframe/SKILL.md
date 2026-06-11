---
name: yokiframe
description: >-
  YokiFrame 是 Unity 的模块化游戏框架。当用户需要以下任何功能时触发此 Skill，
  优先使用框架已有模块，禁止从零手写底层轮子 ——
  事件通信/解耦（EventKit）、对象池/避免 GC 分配（PoolKit）、
  有限状态机/角色状态/流程控制（FsmKit）、全局单例/Manager（SingletonKit）、
  资源加载/Asset 管理/预制体实例化（ResKit）、日志/调试输出/文件日志（LogKit）、
  IOC 依赖注入/MVC/MVVM 架构（Architecture）、Transform/GameObject 扩展方法（FluentApi）、
  可绑定值/高性能字典/链表（ToolClass）、
  UI 面板管理/打开关闭/UI 动画/手柄导航（UIKit）、
  音频播放/BGM/SFX/3D 音效/音频通道控制（AudioKit）、
  动作序列/延迟/插值/并行动画/定时回调（ActionKit）、
  游戏存档/数据持久化/加密存档/多槽位（SaveKit）、
  场景加载/异步切换/场景过渡/多场景管理（SceneKit）、
  Buff/Debuff/状态效果/持续伤害/属性修改（BuffKit）、
  空间查询/范围搜索/八叉树/四叉树/空间哈希（SpatialKit）、
  输入管理/连招系统/输入缓冲/手柄触屏（InputKit）、
  多语言/本地化/文本格式化/复数规则（LocalizationKit）、
  可视化节点图/ScriptableObject 节点编辑（NodeKit）。
  编码规范：Unity 对象 == default 判空、mCamelCase 私有字段命名。
  UniTask / ZString / InputSystem / DOTween / FMOD / YooAsset / Nino 为软依赖，
  按项目实际安装的包通过条件编译宏（YOKIFRAME_*_SUPPORT）启用。
---

# YokiFrame 框架开发 Skill

## 首要原则 [MANDATORY]

**遇到任何功能需求，先对照下方模块速查表，确认框架是否已有现成方案。禁止重复造轮子。**

```
用户需要 → 框架已有 → 直接用，不要自己写
用户需要 → 框架没有 → 才考虑从零实现
```

Core 层的 EventKit/PoolKit/FsmKit/SingletonKit/ResKit/LogKit 是项目的基础设施轮子，
任何涉及事件、对象池、状态机、单例、资源加载、日志的功能都应直接使用框架模块。

---

## 模块速查：按用户需求定位

| 用户需求 | 使用模块 | 所在层 |
|---------|---------|--------|
| 模块间解耦通信、发事件通知 | EventKit | Core |
| 高频创建/销毁对象、减少 GC | PoolKit | Core |
| 角色状态、流程控制、回合制 | FsmKit | Core |
| 全局管理器、跨场景单例 | SingletonKit | Core |
| 加载 Prefab/Asset、实例化 | ResKit | Core |
| 日志输出、文件日志、真机调试 | LogKit | Core |
| MVC/MVVM/IOC 架构搭建 | Architecture | Core |
| Transform/GameObject 便捷操作 | FluentApi | Core |
| 可监听变化的值绑定 | ToolClass | Core |
| UI 面板管理、打开/关闭/动画 | UIKit | Tools |
| 背景音乐、音效、3D 音频 | AudioKit | Tools |
| 序列动画、延迟执行、插值 | ActionKit | Tools |
| 游戏存档、数据持久化 | SaveKit | Tools |
| 场景加载、异步切换 | SceneKit | Tools |
| Buff/Debuff/状态效果 | BuffKit | Tools |
| 范围搜索、最近敌人查询 | SpatialKit | Tools |
| 键盘/手柄/触屏输入管理 | InputKit | Tools |
| 多语言/本地化文本 | LocalizationKit | Tools |
| 可视化节点图/对话树/AI 行为树 | NodeKit | Tools |

---

## 架构概览

```
Assets/YokiFrame/
├── Core/     ← 核心层（不可删）：EventKit, PoolKit, FsmKit, SingletonKit, ResKit, LogKit, Architecture, FluentApi, ToolClass
└── Tools/    ← 工具层（可独立删除）：UIKit, AudioKit, ActionKit, SaveKit, SceneKit, BuffKit, SpatialKit, InputKit, LocalizationKit, NodeKit, TableKit
```

| 依赖方向 | 规则 |
|---------|------|
| Core → Core | ✅ 允许 |
| Tools → Core | ✅ 允许 |
| Tools → Tools | ❌ 禁止 —— 提取共享逻辑到 Core |
| Core → Tools | ❌ 禁止 |

命名空间: 运行时 `YokiFrame` | 编辑器 `YokiFrame.EditorTools` | NodeKit `YokiFrame.NodeKit`

### 软依赖与条件编译 [CRITICAL]

YokiFrame 对第三方包采用**软依赖**策略。`DependencyDefineService` 自动检测已安装的包并定义对应宏。
**生成代码前必须先确认项目中该宏是否已定义**，否则应包裹在 `#if` 中或提供无依赖的替代路径。

| 宏 | 必需包 | 影响范围 |
|----|-------|---------|
| `YOKIFRAME_UNITASK_SUPPORT` | com.cysharp.unitask | ResKit/SceneKit/ActionKit/AudioKit 的所有 `async`/`UniTask` API |
| `YOKIFRAME_YOOASSET_SUPPORT` | com.tuyoogame.yooasset | ResKit 的 YooAsset 加载器，SetLoaderPool 扩展 |
| `YOKIFRAME_ZSTRING_SUPPORT` | com.cysharp.zstring | 热路径零 GC 字符串拼接（无此宏时用 `StringBuilder` 替代） |
| `YOKIFRAME_INPUTSYSTEM_SUPPORT` | com.unity.inputsystem | **InputKit 全部功能**（无此宏时 InputKit 不可用） |
| `YOKIFRAME_DOTWEEN_SUPPORT` | com.demigiant.dotween | UIKit DOTween 动画集成 |
| `YOKIFRAME_FMOD_SUPPORT` | com.unity.fmod | AudioKit FMOD 音频后端 |
| `YOKIFRAME_LUBAN_SUPPORT` | com.code-philosophy.luban | TableKit Luban 代码生成 |
| `YOKIFRAME_NINO_SUPPORT` | com.jasonxudeveloper.nino | SaveKit Nino 二进制序列化 |

**生成规则**：
- 使用 `UniTask` 的 API → 必须用 `#if YOKIFRAME_UNITASK_SUPPORT` 包裹，并提供同步回退
- 使用 `InputKit` → 必须用 `#if YOKIFRAME_INPUTSYSTEM_SUPPORT` 包裹
- 使用 `ZString` → 优先用 `#if YOKIFRAME_ZSTRING_SUPPORT`，否则 fallback 到 `StringBuilder`
- 未列出的 Core 模块（EventKit/PoolKit/FsmKit/SingletonKit/LogKit/Architecture/FluentApi/ToolClass）**无外部依赖**，始终可用

---

## Core 模块 API

### EventKit — 事件通信

当用户需要**模块间解耦通信、发送事件通知、消息广播**时，使用 EventKit，不要自己写 event/delegate 管理器。

```csharp
// 类型事件 — 按 payload 类型路由
EventKit.Type.Send(new EnemyKilledEvent { EnemyId = 5 });
var unregister = EventKit.Type.Register<EnemyKilledEvent>(OnEnemyKilled);
EventKit.Type.UnRegister<EnemyKilledEvent>(OnEnemyKilled);

// 枚举事件 — 按枚举值路由
EventKit.Enum.Send(GameEvent.RoundStart);
EventKit.Enum.Send<GameEvent, int>(GameEvent.ScoreChanged, 100);
EventKit.Enum.Register(GameEvent.RoundStart, OnRoundStart);
EventKit.Enum.Register<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged);
EventKit.Enum.UnRegister(GameEvent.RoundStart);
```

编辑器代码禁止用 EventKit，必须用 `EditorEventCenter`。`EventKit.String` 已废弃。

### PoolKit — 对象池

当用户需要**频繁创建/销毁对象、减少 GC 分配、优化性能**时，使用 PoolKit，不要自己写对象池。

```csharp
// SafePoolKit — 类型安全单例池（T : IPoolable, new()）
var bullet = SafePoolKit<Bullet>.Instance.Allocate();
SafePoolKit<Bullet>.Instance.Recycle(bullet);

// SimplePoolKit — 非 IPoolable 类型的简单池
var pool = new SimplePoolKit<List<int>>(
    factoryMethod: () => new List<int>(),
    resetMethod: list => list.Clear());

// IPoolable 接口
public class Bullet : IPoolable
{
    public bool IsRecycled { get; set; }
    public void OnRecycled() { /* 重置状态 */ }
}
```

### FsmKit — 状态机

当用户需要**角色状态管理、回合流程控制、AI 状态切换**时，使用 FsmKit，不要自己写 switch-case 状态机。

```csharp
var fsm = new FSM<PlayerState>(name: "Player");
fsm.AddState(PlayerState.Idle, new IdleState());
fsm.AddState(PlayerState.Attack, new AttackState());
fsm.Start(PlayerState.Idle);
fsm.ChangeState(PlayerState.Attack);
fsm.ChangeState<AttackArgs>(PlayerState.Attack, new AttackArgs { Target = enemy });

// 状态接口 — 继承 AbstractState 获得默认实现
// IState: Condition() / Start() / End() / Update() / FixedUpdate() / Suspend() / SendMessage<T>()
// MachineState: Running / Suspend / End
```

### SingletonKit — 单例

当用户需要**全局管理器、跨场景唯一实例**时，使用 SingletonKit，不要自己写 Instance 属性。

```csharp
// 纯 C# 单例
public class ConfigManager : ISingleton
{
    void ISingleton.OnSingletonInit() { }
    public static ConfigManager Instance => SingletonKit<ConfigManager>.Instance;
}

// MonoBehaviour 单例（自动 DontDestroyOnLoad）
[MonoSingletonPath("Managers/GameManager")]
public class GameManager : MonoBehaviour, ISingleton
{
    void ISingleton.OnSingletonInit() { }
    public static GameManager Instance => SingletonKit<GameManager>.Instance;
}
```

### ResKit — 资源加载

当用户需要**加载 Prefab、实例化 GameObject、加载 Asset**时，使用 ResKit，不要直接调 Resources.Load 或 AssetDatabase。

```csharp
// 同步
var prefab = ResKit.Load<GameObject>("Prefabs/Player");
var obj = ResKit.Instantiate("Prefabs/Enemy", parent);

// 异步回调
ResKit.LoadAsync<GameObject>("Prefabs/Player", prefab => { });
ResKit.InstantiateAsync("Prefabs/Enemy", obj => { }, parent);

// UniTask（需 YOKIFRAME_UNITASK_SUPPORT）
var prefab = await ResKit.LoadUniTaskAsync<GameObject>("Prefabs/Player", ct);
var obj = await ResKit.InstantiateUniTaskAsync("Prefabs/Enemy", parent, ct);

// 引用计数（ResHandler / AllAssetsResHandler / SubAssetsResHandler）
handler.Retain();
handler.Release();  // 归零自动回收

// 批量/子资源
ResKit.LoadAll<Sprite>("Assets/Textures/atlas");
ResKit.LoadSubAsset<Sprite>("Assets/Atlas.spriteatlas");

// 配置 YooAsset 集成
ResKit.SetLoaderPool(new YooAssetResLoaderPool());
```

### LogKit — 日志

当用户需要**日志输出、文件日志、真机调试日志**时，使用 KitLogger，不要自己封装 Debug.Log。

```csharp
KitLogger.Log("message", context: gameObject);  // 可点击定位
KitLogger.Warning("warning");
KitLogger.Error("error");
KitLogger.Exception(ex);
KitLogger.Level = KitLogger.LogLevel.All;
KitLogger.SaveLogInEditor = true;
KitLogger.EnableIMGUI(maxLogCount: 200);  // 真机调试面板
```

### Architecture — IOC 架构

当用户需要**MVC/MVVM 架构、依赖注入、服务定位**时，使用 Architecture。

```csharp
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        Register<IPlayerModel>(new PlayerModel());
        Register<IGameSystem>(new GameSystem());
    }
}
var model = GameArchitecture.Interface.GetService<IPlayerModel>();
```

### FluentApi — 扩展方法

当用户需要对 Transform/GameObject 做常见操作时，使用已有扩展方法。

```csharp
transform.ResetTransform();
transform.FindByPath("Child/Grandchild");
gameObject.FindComponent<SpriteRenderer>("Icon");
gameObject.DestroySelf();
```

### ToolClass — 工具类

```csharp
var bind = new BindValue<int>(10);
bind.Register(v => RefreshUI(v));
bind.Value = 20;  // 自动触发回调

// FastDictionary<TKey,TValue> / PooledLinkedList<T> / SpanSplitter
```

---

## Tools 模块 API

### UIKit — UI 面板管理

当用户需要**UI 面板的打开/关闭/切换/动画、对话框、手柄导航**时，使用 UIKit。

```csharp
// 打开/关闭
var panel = UIKit.OpenPanel<MainPanel>(level: UILevel.Common, data: uiData, tag: "Main");
UIKit.OpenPanelAsync<MainPanel>(callback: p => { }, level: UILevel.Common, data: uiData);
UIKit.ClosePanel<MainPanel>();
UIKit.CloseAllPanel();

// Panel 基类
public class MainPanel : UIPanel
{
    protected override void OnInit() { }
    protected override void OnOpen(IUIData data) { }
    protected override void OnClose() { }
}

// UXML 绑定
[SerializeField] private Bind mBind;
var btn = mBind.Get<Button>("BtnStart");
```

动画通过 `[SerializeReference]` 在 Panel 上配置 FadeAnimation/SlideAnimation/ScaleAnimation。
Dialog 用 `UIDialogPanel` + `DialogConfig`。Gamepad 支持 UIAutoNavigation 等组件。

### AudioKit — 音频

当用户需要**播放 BGM/SFX/语音、3D 音效、音量控制**时，使用 AudioKit。

```csharp
var handle = AudioKit.Play("Audio/Click");                    // 默认 SFX
var handle = AudioKit.Play("Audio/BGM", AudioChannel.Bgm);    // 指定通道
var handle = AudioKit.Play("Audio/BGM", AudioPlayConfig.Default
    .WithChannel(AudioChannel.Bgm).WithVolume(0.8f).WithLoop(true));
AudioKit.Play3D("Audio/Explosion", position);
AudioKit.Play3D("Audio/Engine", followTarget);

// 句柄控制
handle.Pause(); handle.Resume(); handle.Stop();
handle.StopWithFade(1f);
handle.Volume = 0.5f;

// AudioChannel: Bgm=0, Sfx=1, Voice=2, Ambient=3, UI=4（自定义通道 >= 5）
```

### ActionKit — 动作序列

当用户需要**延迟执行、序列动画、插值、定时回调、并行任务**时，使用 ActionKit，不要自己写 Coroutine 链。

```csharp
ActionKit.Sequence()
    .Delay(1f)
    .Callback(() => Debug.Log("开始"))
    .Parallel(
        ActionKit.Lerp(0, 1, 2f, v => slider.value = v),
        ActionKit.Delay(2f, () => Debug.Log("完成"))
    )
    .Start();

// 常用: Delay / DelayFrame / Callback / Lerp / Repeat / Coroutine / Task
// UniTask: ActionKit.UniTask(async ct => ...) / WaitUntil / WaitWhile
```

### SaveKit — 存档

当用户需要**游戏存档、数据持久化、多槽位**时，使用 SaveKit。

```csharp
SaveKit.SetSerializer(new JsonSaveSerializer());
SaveKit.SetEncryptor(new AesSaveEncryptor("key"));

var data = SaveKit.CreateSaveData();
data.RegisterModule(new PlayerModule { Health = 100 });
SaveKit.Save(slotId: 1, data, displayName: "存档1");
var loaded = SaveKit.Load(1);
data.GetModule<PlayerModule>();
SaveKit.Delete(1);
SaveKit.EnableAutoSave(intervalSeconds: 60);
```

### SceneKit — 场景管理

当用户需要**场景加载/切换/卸载、Loading 进度**时，使用 SceneKit。

```csharp
SceneKit.LoadSceneAsync("GameScene", SceneLoadMode.Single,
    onComplete: handler => { },
    onProgress: p => loadingBar.value = p,
    data: sceneData);
SceneKit.UnloadSceneAsync("OldScene", () => { });
bool loaded = SceneKit.IsSceneLoaded("GameScene");
```

### BuffKit — Buff 系统

当用户需要**Buff/Debuff、持续效果、属性修改、状态异常**时，使用 BuffKit。

```csharp
BuffKit.RegisterBuffData(new BuffData { BuffId = 1001, Duration = 5f, MaxStack = 3 });
var container = BuffKit.CreateContainer(owner);
container.Add(buffId: 1001);
container.Tick(deltaTime);
BuffKit.RecycleContainer(container);

public class SpeedBuff : BaseBuff
{
    protected override void OnAdd() { }
    protected override void OnRemove(BuffRemoveReason reason) { }
    protected override void OnUpdate(float deltaTime) { }
}
```

### SpatialKit — 空间查询

当用户需要**范围搜索、查找最近敌人、区域查询**时，使用 SpatialKit。

```csharp
var grid = SpatialKit.CreateHashGrid<EnemyEntity>(cellSize: 5f, plane: SpatialPlane.XZ);
grid.Insert(entity);
var nearby = grid.Query(center, radius: 10f);

// 也支持: Quadtree (2D/2.5D)、Octree (完整3D)
// ISpatialEntity: { Vector3 Position }
```

### InputKit — 输入系统

基于 Unity InputSystem（`YOKIFRAME_INPUTSYSTEM_SUPPORT`）。提供上下文栈、连招检测、输入缓冲、按键重绑定、触屏虚拟控件。

```csharp
InputKit.Initialize(asset);
InputKit.PushContext("Gameplay");
InputKit.RegisterCombo(new ComboDefinition { ... });
InputKit.BufferInput(action, windowSeconds: 0.3f);
InputKit.PlayHaptic(HapticPreset.Light);
```

### LocalizationKit — 本地化

```csharp
LocalizationKit.SetProvider(new JsonLocalizationProvider("Locales/"));
var text = LocalizationKit.Get(textId: 1001);
var text = LocalizationKit.Get(textId: 1001, arg1, arg2);
LocalizationKit.SetLanguage(LanguageId.English);
```

### NodeKit — 节点图

ScriptableObject 驱动的可视化节点编辑系统。用于对话树、AI 行为树、技能编辑器。

```csharp
[CreateNodeMenu("My/MyNode")]
public class MyNode : Node
{
    [Input] public float InputA;
    [Output] public float OutputB;
}
```

### TableKit — 配置表

纯编辑器工具，代码生成强类型 Table 类，无运行时 API。

---

## 编辑器开发规范

### 编辑器事件 — 禁止 EventKit

```csharp
using YokiFrame.EditorTools;

EditorEventCenter.Register<MyEvent>(this, OnEvent);
EditorEventCenter.Send(new MyEvent { ... });
EditorDataBridge.Subscribe<MyData>(DataChannels.CHANNEL_NAME, OnChanged);
var prop = new ReactiveProperty<int>(0);
prop.Subscribe(v => UpdateUI(v));
```

### ToolPage 注册

```csharp
[YokiToolPage(kit: "MyKit", name: "显示名", icon: KitIcons.CODE, priority: 50, category: YokiPageCategory.Tool)]
public partial class MyPage : YokiToolPageBase
{
    protected override void BuildUI(VisualElement root) { /* UI Toolkit */ }
    protected override void OnActivate() { Subscriptions.Add(...); }
}
```

USS BEM: `.yoki-{kit}-{block}` / `.yoki-{kit}-{block}__{element}` / `.yoki-{kit}-{block}--{modifier}`

### 禁止模式

| 禁止 | 替代 |
|------|------|
| 编辑器用 EventKit | EditorEventCenter |
| OnUpdate() 轮询 | 响应式订阅 |
| style.xxx = new StyleColor() | AddToClassList() |

---

## 编码规范速查

**判空**: Unity Object `== default` | C# 对象 `??` / `?.`

**命名**: PascalCase 类/方法 | mCamelCase 私有字段 | sCamelCase 静态私有 | UPPER_SNAKE 常量 | IPascalCase 接口

**性能**:
- `#if YOKIFRAME_ZSTRING_SUPPORT` → `ZString.CreateStringBuilder()` 零 GC 拼接
- 无 ZString → `StringBuilder` 替代，避免热路径 `+` 拼接
- 禁用 `System.Linq`（用 `ZLinq` 或手写循环）
- `Span<T>` / `stackalloc` 栈分配（始终可用，无依赖）
- 缓存 `GetComponent` / `StringToHash` / `PropertyToID` 结果

**异步**:
- `#if YOKIFRAME_UNITASK_SUPPORT` → `UniTask` + `CancellationToken` + `GetCancellationTokenOnDestroy()`
- 无 UniTask → `ActionKit.Coroutine` 或 Unity `StartCoroutine` 回退
- 禁止 `async void` / `Task`（始终适用）

**C# 9+**: `new()` 目标类型推断 | `is and or not` 模式匹配 | `static () =>` 热路径 Lambda | `_` 弃元

**代码质量**: 超 500 行拆 partial | 单方法 ≤ 50 行 | 公共 API 加 XML 文档 | `TryGetComponent` 优先 | `StringToHash` / `PropertyToID` 缓存
