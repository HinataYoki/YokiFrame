---
name: yokiframe
description: YokiFrame 使用指南。Use when Codex 需要在 Unity 或 Godot 项目中使用或说明 YokiFrame Kit API、跨引擎 Adapter/Provider/Backend、Tauri 工作台、EventKit、FsmKit、PoolKit、SingletonKit、ResKit、ManagedRuntimeKit、ActionKit、AudioKit、SaveKit、LocalizationKit、SceneKit、SpatialKit、UIKit、TableKit、GraphKit、命令桥、snapshot、telemetry、事件流、代码扫描或 AI 运行时查询。
---

# YokiFrame 使用指南

YokiFrame 是不绑定具体游戏引擎的 C# 游戏 Kit 框架。业务代码优先使用 `YokiFrame` 命名空间下的统一 Kit API；Unity、Godot 或未来宿主差异放进 Adapter、Provider、Backend 或 Installer。不要重新手写事件总线、对象池、状态机、单例、资源加载、UI 面板栈、空间索引、运行时调试协议或 AI 文件通信层。项目输入直接使用 Unity / Godot 原生输入系统。

## 结构判断

开始任何 YokiFrame 任务前先判断落点：

| 目标 | 首选入口 | 不要做 |
|---|---|---|
| 游戏业务逻辑 | `YokiFrame` 统一 Kit API | 直接依赖 Unity/Godot 内部对象模型 |
| 宿主差异 | Adapter / Provider / Backend / Installer | 在 Core Runtime 通用层新增宿主依赖 |
| AI 查询框架状态 | `.yokiframe/engines/<engineId>` snapshot 或 command/result | 写 root `.yokiframe/commands` 或高频轮询命令 |
| 人类调试 | Tauri Workbench | 把工作台当营销页或静态文档页 |
| 配置表和图编辑 | TableKit / GraphKit 工作台页面 | 把它们当 runtime command handler |

## 选择入口

- 业务代码：使用 `YokiFrame` 命名空间下的统一 Kit API。
- 人类调试：在 Unity 菜单打开 `YokiFrame/Editor UI/Launch`，快捷键为 `Ctrl+E`。
- AI/脚本诊断：通过 `.yokiframe/engines/<engineId>` 文件桥读取 snapshot 或发送命令。
- Tauri 页面：高频可视状态优先 shared memory telemetry；不可用或过期时读 snapshot。
- Godot 项目：使用 Godot adapter 和 `addons/yokiframe` 薄插件入口；命令桥路径同样走 `.yokiframe/engines/<engineId>`。
- 配置表：使用 Tauri TableKit 页面配置/验证/生成 Luban 产物；它不是 Runtime command handler。
- 图编辑：使用 Tauri GraphKit 页面编辑 graph 项目、节点类型、黑板、子图、Luban XML 和 runtime contract；它不是 Runtime command handler。

## 快速选择

- 事件通信：使用 `EventKit.Type` 或 `EventKit.Enum`。
- 状态机：使用 `FsmKit` 的 `FSM<TEnum>` 和 `AbstractState<TEnum, TBlackboard>`。
- 对象复用：使用 `SimplePoolKit<T>`、`SafePoolKit<T>` 或集合池。
- 单例：纯 C# 使用 `Singleton<T>`，Unity 生命周期对象使用 Unity Adapter 的 `MonoSingleton<T>`。
- 资源：使用 `ResKit`，通过 Provider 适配 Unity、Godot 或项目资源系统。
- 托管运行时：使用 `ManagedRuntimeKit` 查询和选择 C# 运行时后端；LeanCLR、HybridCLR、Unity IL2CPP、Godot .NET 等具体能力由可选 Adapter 注册，Core 不直接依赖。Tauri 工作台可镜像 Unity LeanCLR 的 `ProjectSettings/LeanCLR.asset` 核心字段，并提供打开 Unity 原生 Project Settings > LeanCLR 面板的入口；原生 SettingsProvider/IMGUI 面板不能直接嵌入 Web 前端。
- LeanCLR Unity 包：当前 Git URL 为 `https://github.com/focus-creative-games/leanclr-unity.git`，包名 `com.code-philosophy.leanclr`；Unity Editor Adapter 通过 PackageManager 探测和注册 `LeanCLR` 后端，不直接引用 LeanCLR API。
- UI：使用 `UIKit` 管理面板、层级和面板栈；当前 runtime 仍包含 Unity UI 实现，Godot 完整接入需要独立 `IUIBackend`。
- 空间查询：使用 `SpatialKit` 的 HashGrid、Quadtree 或 Octree。
- 动作流程：使用 `ActionKit` 组合 Delay、Callback、Sequence、Parallel 等动作。
- ActionKit 提前停止：`IActionController.Cancel()` 只表示取消并释放，不触发完成回调；需要提前完成并落最终状态时，显式设置最终状态，或保留具体 `IAction` / tween 引用使用对应完成语义。
- 配置表生成：使用 `TableKit` 工作台；生成代码落在用户项目，运行时默认通过 ResKit 读取表数据。
- 图数据编辑：使用 `GraphKit` 工作台；它产出 graph JSON/XML、Luban definition/data 和 runtime contract，运行时执行器由项目侧接入。
- AI/脚本状态查询：使用 `.yokiframe` 文件命令桥，优先读 snapshot，再发送 command。

## 查询顺序

查看当前状态时按这个顺序：

1. Tauri 可视高频页面：`read_telemetry`。
2. AI 或脚本：优先读 `snapshots/<kit>/<name>.json`。
3. 需要详情、历史、显式操作或 snapshot 缺失：发送 command/result 请求。
4. 请求超时：先发 `System/bridge_status`，再看 pending、processing、deadletter、lastError。

不要用高频 `send_command` 轮询实时页面；命令桥是可靠控制面，不是运行时事件总线。

## 使用规则

1. 先查现有 Kit API，再写项目代码。
2. 业务代码依赖统一 Kit 入口，不直接依赖宿主内部实现。
3. 高频运行时逻辑不要写 `.yokiframe` 文件，不要每帧序列化 JSON。
4. 查询当前状态优先读 snapshot；只有需要详情、历史或显式操作时才发送 engine-scoped 命令。
5. 变更型命令，例如删除存档、停止音频、切换语言、卸载场景，只在用户明确要求时执行。
6. 注册事件、打开 UI、启动自动保存等生命周期能力必须有成对释放路径。
7. Unity/Godot 差异优先放在 Adapter、Provider 或 Backend，业务仍调用同一套 YokiFrame API。
8. AI 需要框架状态时优先使用 `.yokiframe` 协议和 `yokiframe-command-bridge` Skill，不把 Unity MCP 当作唯一状态来源。
9. TableKit 和 GraphKit 属于 Tauri 编辑器工具流，不读取 `TableKit/state`、`GraphKit/state`，也不发送 `TableKit/*` 或 `GraphKit/*` runtime command。
10. ManagedRuntimeKit 的 Core 层只能定义托管运行时接口、默认后端、能力和诊断模型；LeanCLR 接入必须作为可选后端，不得让未安装 LeanCLR 的项目编译失败。

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

### UIKit

面板显示和栈管理走统一 UI 门面：

```csharp
var menu = UIKit.OpenPanel<MenuPanel>(UILevel.Common, data: null, tag: "main");
UIKit.PushPanel(menu, "Main", hidePreLevel: true);
UIKit.PopPanel(showPreLevel: true, autoClose: true);
```

## 常用诊断任务

- 查看桥状态：读 `references/command-bridge.md` 的 `System/bridge_status`。
- 查看 FSM：优先 `FsmKit/state` snapshot，再用 `FsmKit/get_workbench_snapshot`。
- 查看 EventKit：优先 `EventKit/state` snapshot，再用 `EventKit/get_workbench_snapshot`；代码关系由扫描器提供。
- 查看 SpatialKit：优先 `SpatialKit/state` snapshot，再用 `SpatialKit/get_workbench_snapshot`；实体插入、更新、删除和查询留在运行时代码的 `ISpatialIndex<T>` 对象上执行。
- 查看 UIKit：优先 `UIKit/state` snapshot，再用 `UIKit/get_workbench_snapshot`；面板开关、显示/隐藏和压栈不通过命令桥执行。
- 使用 TableKit：在工作台 TableKit 页面检查 Luban 环境、输出目录、运行时路径模式、验证和生成日志；不要通过命令桥查询 TableKit 状态。
- 使用 GraphKit：在工作台 GraphKit 页面编辑节点图、黑板、placemat、子图、Luban XML 和 GraphRuntime contract；不要通过命令桥查询 GraphKit 状态。
- 扫描 EventKit 代码：在 Tauri EventKit 页面点击“扫描代码”，需要时勾选“排除 Editor”。
- 打开源码：通过 Tauri 页面或 `System/open_code_location`，由引擎默认代码编辑器处理。

## 参考资料

- `references/kits.md`：各 Kit API 速查和示例。
- `references/command-bridge.md`：文件命令桥请求/响应协议和调试顺序。
- `yokiframe-command-bridge` Skill：命令桥完整命令目录和压力验证说明。
- `yokiframe-editor` Skill：YokiFrame 编辑器工作台、安装 Skill、Kit 页面和日志诊断使用说明。
