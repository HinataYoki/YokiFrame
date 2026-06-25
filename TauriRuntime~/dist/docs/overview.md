# YokiFrame 2.0 框架概览

YokiFrame 2.0 是一套面向游戏运行时代码的模块化 Kit 框架。它把事件、状态机、对象池、资源、单例、动作序列和音频播放等常用能力整理成统一 API，让业务代码优先依赖稳定的 C# 门面，而不是把项目逻辑写死在某一个宿主 API 上。

入门阶段不需要先理解内部目录分层。你只需要知道：业务脚本统一使用 `YokiFrame` 命名空间；需要宿主能力时，由 Unity 或 Godot 的运行时适配器负责注入资源、音频、时间、日志等后端。

## 主要特性

| 特性 | 说明 |
|------|------|
| 统一 Kit API | 事件、状态机、对象池、资源、单例、动作和音频都有固定入口，业务代码写法一致。 |
| 强类型优先 | EventKit、FsmKit 等核心模块优先使用泛型、枚举和明确的 payload 类型，减少字符串拼错和重构风险。 |
| 宿主能力可替换 | 资源、音频、时间、日志、序列化等能力通过接口或后端注入，项目可以替换为自己的实现。 |
| 低分配运行时 | PoolKit、ActionKit 等热路径尽量复用对象，避免把调试或诊断成本塞进每帧逻辑。 |
| Unity / Godot 适配 | Unity 侧提供 `YokiFrameKit.Initialize(YokiFrameEngine.Unity)`、`UnityBootstrap`、`UnityResourceProvider`、`MonoSingleton<T>`、`UnityAudioKitBackend`；Godot 侧提供对应运行时适配类型。 |

## 当前可用 Kit

| Kit | 命名空间 | 入口 | 主要用途 |
|-----|----------|------|----------|
| EventKit | `YokiFrame` | `EventKit.Type`、`EventKit.Enum` | 模块解耦、运行时事件发送和监听。 |
| FsmKit | `YokiFrame` | `FSM<TEnum>`、`FSM<TEnum,TArgs>`、`HierarchicalSM<TEnum>` | 角色、敌人、流程、UI 状态控制。 |
| PoolKit | `YokiFrame` | `SimplePoolKit<T>`、`SafePoolKit<T>`、`Pool.List<T>` | 普通对象、可回收对象和临时集合复用。 |
| ResKit | `YokiFrame` | `ResKit`、`ResHandle<T>`、`IResourceProvider` | 统一资源加载、引用计数、后端替换。 |
| TableKit | 项目生成代码 | `TableKit`、`<topModule>.<manager>` | Luban 编辑器工作流；安装 Luban 并启用 `YOKIFRAME_LUBAN_SUPPORT` 后生成到项目 Scripts。 |
| SingletonKit | `YokiFrame` | `SingletonKit<T>`、`Singleton<T>` | 纯 C# 服务单例和生命周期管理。 |
| Unity Singleton | `YokiFrame.Unity` | `MonoSingleton<T>` | 需要 Unity 生命周期的单例。 |
| Godot Singleton | `YokiFrame.Godot` | `GodotSingleton<T>` | 需要 Godot Node 生命周期的单例。 |
| CodeGenKit | `YokiFrame` | `CodeGenKit`、`ICode`、`ICodeScope` | Editor 层纯 C# 代码生成工具，支持结构化 AST 和模板式构建。 |
| ActionKit | `YokiFrame` | `ActionKit.Sequence()`、`ActionKit.Delay()` | 延迟、回调、并行、重复、协程和 Task 组合。 |
| AudioKit | `YokiFrame` | `AudioKit`、`AudioPlayOptions` | 播放音效/音乐、停止播放、音量总线控制。 |

## 推荐使用方式

| 目标 | 推荐入口 |
|------|----------|
| 业务模块之间发通知 | 新代码优先用 `EventKit.Type`；固定系统信号用 `EventKit.Enum`。 |
| 管理一个对象的运行状态 | 用 `FSM<TEnum>`，在宿主的帧循环中手动调用 `Update()` / `FixedUpdate()`。 |
| 管理多个可并行的子状态 | 用 `HierarchicalSM<TEnum>`，通过 `Change(id, MachineState)` 控制子状态。 |
| 复用普通对象 | 用 `SimplePoolKit<T>`，传入 factory 和 reset。 |
| 复用带回收标记的对象 | 实现 `IPoolable`，使用 `SafePoolKit<T>.Instance`。 |
| 临时使用 List / Dictionary / Set | 用 `Pool.List<T>`、`Pool.Dictionary<TKey,TValue>`、`Pool.Set<T>` 的 action 版本。 |
| 统一加载资源 | 先确保宿主已调用 `ResKit.SetProvider(...)`，再用 `Load<T>()` 或 `LoadAsset<T>()`。 |
| 统一加载配置表 | 先确保 Luban 环境已启用 `YOKIFRAME_LUBAN_SUPPORT`，再用 Tauri 的 TableKit 页面配置 Luban 参数，把 Luban 代码和 `TableKit.cs` 生成到项目 Scripts。 |
| 纯 C# 全局服务 | 实现 `ISingleton` 后使用 `SingletonKit<T>.Instance`，或继承 `Singleton<T>`。 |
| Unity 全局组件 | 继承 `MonoSingleton<T>`，只在确实需要 `GameObject` / `Transform` 时使用。 |
| 串联一组延迟和回调 | 用 `ActionKit.Sequence().Delay(...).Callback(...).Start()`。 |
| 播放音效或音乐 | 确保宿主已设置 `AudioKit` 后端，再调用 `AudioKit.PlaySfx()` 或 `AudioKit.PlayMusic()`。 |

## 运行时接入

Unity 项目通常在启动阶段调用统一入口，注册 Unity 侧默认资源、日志、序列化和可选 Tool 后端：

```csharp
using YokiFrame;

public sealed class GameEntry : MonoSingleton<GameEntry>
{
    private void Awake()
    {
        YokiFrameKit.Initialize(YokiFrameEngine.Unity);
    }
}
```

`UnityBootstrap` 仍可作为场景生命周期外壳使用；它内部同样调用 `YokiFrameKit.Initialize`，并在宿主生命周期中转发 tick 和 shutdown。

如果项目有自己的资源系统或音频系统，也可以跳过默认后端，直接实现 `IResourceProvider` 或 `IAudioBackend`，再调用：

```csharp
ResKit.SetProvider(new ProjectResourceProvider());
AudioKit.SetBackend(new ProjectAudioBackend());
```

Godot 项目使用 Godot 运行时适配器时，由 `GodotBootstrap` 注册资源等运行时 Provider。AudioKit 当前需要项目提供音频后端后再调用 `AudioKit.SetBackend(...)`。

## 约束和边界

- Base 层 API 不依赖 UnityEngine 或 Godot API；引擎对象、资源、音频等差异通过 Adapter 或项目后端接入。
- `EventKit.String` 仅用于旧代码兼容；新事件优先使用 Type 或 Enum 通道。
- `FSM<TEnum>` 不会自动更新，必须由业务代码在合适的帧循环中调用更新方法。
- `SimplePoolKit<T>` 当前不防重复回收；需要防重复时使用 `SafePoolKit<T>` 或在业务侧保证只回收一次。
- `ResKit.Load<T>()` 只返回资源对象；需要明确释放生命周期时使用 `LoadAsset<T>()` 并释放句柄。
- TableKit 编辑器和文档都在 Tauri 前端；YokiFrame 不携带 TableKit Runtime，配置表运行时代码由工具生成到项目 Scripts，配置表内容不会写入 Tauri snapshot。
- ActionKit 核心为纯 C# 调度器，Unity/Godot 适配器负责在宿主帧循环中驱动 tick。
