---
name: yokiframe
description: YokiFrame 使用指南。Use when Codex 需要在 Unity 或 Godot 项目中使用 YokiFrame 的 EventKit、FsmKit、PoolKit、SingletonKit、ResKit、ActionKit、AudioKit、SaveKit、LocalizationKit、SceneKit、SpatialKit、InputKit、UIKit、TableKit、Tauri 工作台或命令桥进行业务开发、调试和框架状态查询。
---

# YokiFrame 使用指南

YokiFrame 是面向游戏项目的模块化框架。优先使用框架现有 Kit，不要重新手写事件总线、对象池、状态机、单例、资源加载、输入、UI 面板栈、空间索引或运行时调试协议。

## 快速选择

- 事件通信：使用 `EventKit.Type` 或 `EventKit.Enum`。
- 状态机：使用 `FsmKit` 的 `FSM<TEnum>` 和 `AbstractState<TEnum, TBlackboard>`。
- 对象复用：使用 `SimplePoolKit<T>`、`SafePoolKit<T>` 或集合池。
- 单例：纯 C# 使用 `Singleton<T>`，Unity 生命周期对象使用 Unity Adapter 的 `MonoSingleton<T>`。
- 资源：使用 `ResKit`，通过 Provider 适配 Unity、Godot 或项目资源系统。
- 输入：使用 `InputKit` 和 `InputContext`，宿主按键细节留给 backend。
- UI：使用 `UIKit` 管理面板、层级和面板栈；当前 runtime 仍包含 Unity UI 实现，Godot 完整接入需要独立 `IUIBackend`。
- 空间查询：使用 `SpatialKit` 的 HashGrid、Quadtree 或 Octree。
- 动作流程：使用 `ActionKit` 组合 Delay、Callback、Sequence、Parallel 等动作。
- 调试工作台：在 Unity 菜单打开 `YokiFrame/Editor UI/Launch`。
- AI/脚本状态查询：使用 `.yokiframe` 文件命令桥，优先读 snapshot，再发送 command。

## 使用规则

1. 先查现有 Kit API，再写项目代码。
2. 业务代码依赖统一 Kit 入口，不直接依赖宿主内部实现。
3. 高频运行时逻辑不要写 `.yokiframe` 文件，不要每帧序列化 JSON。
4. 查询当前状态优先读 snapshot；只有需要详情、历史或显式操作时才发送 engine-scoped 命令。
5. 变更型命令，例如删除存档、停止音频、切换语言、卸载场景，只在用户明确要求时执行。
6. 注册事件、订阅输入、打开 UI、启动自动保存等生命周期能力必须有成对释放路径。
7. Unity/Godot 差异优先放在 Adapter、Provider 或 Backend，业务仍调用同一套 YokiFrame API。

## 常用入口

### EventKit

使用强类型事件做业务解耦：

```csharp
public readonly struct EnemyKilledEvent
{
    public readonly int EnemyId;

    public EnemyKilledEvent(int enemyId)
    {
        EnemyId = enemyId;
    }
}

EventKit.Type.Register<EnemyKilledEvent>(OnEnemyKilled);
EventKit.Type.Send(new EnemyKilledEvent(enemyId));
EventKit.Type.UnRegister<EnemyKilledEvent>(OnEnemyKilled);
```

### FsmKit

状态逻辑放进状态类，业务脚本负责驱动：

```csharp
var fsm = new FSM<PlayerState>("PlayerFSM");
fsm.Add(PlayerState.Idle, new IdleState());
fsm.Add(PlayerState.Move, new MoveState());
fsm.Start(PlayerState.Idle);
fsm.Update();
```

### PoolKit

普通对象用局部池，可回收对象用全局安全池：

```csharp
var pool = new SimplePoolKit<Bullet>(
    () => new Bullet(),
    bullet => bullet.Reset(),
    initCount: 16);

var bullet = pool.Allocate();
pool.Recycle(bullet);
```

### ResKit

资源加载走统一入口，引用结束后释放 handle：

```csharp
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

### InputKit

读取动作状态，不在业务里绑定宿主输入 API：

```csharp
InputKit.Update(unscaledTime);

if (InputKit.WasPressedThisFrame("Jump"))
{
    Jump();
}
```

### UIKit

面板显示和栈管理走统一 UI 门面：

```csharp
var menu = UIKit.OpenPanel<MenuPanel>(UILevel.Common, data: null, tag: "main");
UIKit.PushPanel(menu, "Main", hidePreLevel: true);
UIKit.PopPanel(showPreLevel: true, autoClose: true);
```

## 调试顺序

1. 打开工作台：Unity 菜单 `YokiFrame/Editor UI/Launch`。
2. 看运行状态：在对应 Kit 页面查看 telemetry 或 snapshot。
3. AI/脚本读取：先查 `.yokiframe/engines/<engineId>/snapshots/<Kit>/state.json`。
4. 需要详情：发送 `.yokiframe/engines/<engineId>/commands/<requestId>.json`。
5. 命令超时：发送 `System/bridge_status`，检查 engine-scoped pending、processing、deadletter、lastError。

## 参考资料

- `references/kits.md`：各 Kit API 速查和示例。
- `references/command-bridge.md`：文件命令桥请求/响应协议和调试顺序。
- `yokiframe-command-bridge` Skill：命令桥完整命令目录和压力验证说明。
- `yokiframe-editor` Skill：YokiFrame 编辑器工作台、安装 Skill、Kit 页面和日志诊断使用说明。
