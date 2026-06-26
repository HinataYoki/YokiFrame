# YokiFrame

<p align="center">
  <img src="Core/Editor/Resources/yoki.png" alt="YokiFrame Logo" width="128" height="128">
</p>

<p align="center">
  <b>跨引擎游戏 Kit 框架 + AI 原生通信层 + Tauri 可视化工作台</b><br>
  用统一的 C# Kit API 承载运行时能力，用文件协议让 AI、工具前端和引擎宿主可靠协作。
</p>

---

## YokiFrame 是什么

YokiFrame 不再只是一个绑定 Unity 的工具包，而是一套面向多宿主的游戏开发框架。它把事件、状态机、对象池、资源、单例、日志、动作、音频、输入、场景、存档、UI、配置表和本地化等能力整理为稳定的 Kit API；Unity、Godot 或未来其它宿主只负责提供 Adapter。

新版框架重点完成了三件事：

| 方向 | 做到了什么 |
| --- | --- |
| 跨引擎设计 | Runtime 核心优先保持纯 C#；宿主差异通过 Unity / Godot Adapter 接入，业务侧继续调用 `YokiFrame` 统一入口。 |
| AI 原生通信 | `.yokiframe/` 文件桥成为框架级控制面，AI Agent 不依赖 Unity MCP 也能发现引擎、发送命令、读取响应和检查快照。 |
| 可视化编辑器工具 | Tauri + Web 前端提供独立工作台，把 Kit 状态、命令桥健康、代码扫描、生成器和运行时诊断集中到一个现代桌面工具里。 |

---

## 核心能力

### 统一 Kit API

业务代码优先依赖 `YokiFrame` 命名空间下的统一入口：

| Kit | 能力 |
| --- | --- |
| Architecture | 服务注册、模块化组织和运行时架构诊断。 |
| EventKit | 类型事件、枚举事件和旧字符串事件兼容，用于模块解耦。 |
| FsmKit | 普通 FSM、带参数 FSM、层级状态机和运行时状态诊断。 |
| PoolKit | C# 对象池、可回收对象池、集合池和对象池工作台快照。 |
| ResKit | 统一资源加载、raw 文件读取、场景资源后端、引用计数和 Provider 替换。 |
| SingletonKit | 纯 C# 单例、Unity `MonoSingleton`、Godot `GodotSingleton` 的统一生命周期视角。 |
| LogKit | 引擎日志适配、运行时日志文件、工作台日志诊断。 |
| ActionKit | 延迟、回调、并行、重复、Task / Coroutine 组合和动作树调试。 |
| AudioKit | 音效、音乐、音量总线、活跃 voice 诊断和音频 ID 生成辅助。 |
| SaveKit | 多槽位存档、序列化/加密/迁移后端、自动保存状态。 |
| InputKit | 输入后端、动作状态、输入缓冲和上下文栈。 |
| SceneKit | 跨引擎场景加载、预加载、激活和卸载后端。 |
| SpatialKit | HashGrid、Quadtree、Octree 空间索引和查询诊断。 |
| LocalizationKit | 多语言 Provider、formatter、缓存、binder 和语言切换。 |
| UIKit | UI 后端、面板栈、层级、面板创建和绑定辅助。 |
| TableKit | 基于 Tauri 的 Luban 配置表生成、参数管理和输出校验。 |

### 跨引擎 Adapter

框架分层的原则是：核心 API 不知道自己跑在 Unity 还是 Godot。

```text
User Code
  -> YokiFrame Kit API
  -> Core Runtime interfaces / providers / handlers
  -> Unity or Godot Adapter
  -> Engine runtime and editor
```

当前包内落点：

```text
YokiFrame/
├── Core/
│   ├── Runtime/
│   │   ├── Architecture, EventKit, FsmKit, PoolKit, ResKit, Singleton, LogKit
│   │   ├── Interfaces, ToolClass, FluentApi, Settings
│   │   ├── CommandBridge
│   │   └── Adapters/
│   │       ├── Unity/
│   │       └── Godot/
│   └── Editor/
│       ├── Skills/
│       └── Resources/
├── Tools/
│   ├── ActionKit, AudioKit, InputKit, LocalizationKit
│   ├── SaveKit, SceneKit, SpatialKit, TableKit, UIKit
│   └── ...
└── Installer~/
```

核心抽象包括 `IEngineLogger`、`IEngineTime`、`IEngineObject`、`IResourceProvider`、`IRawResourceProvider`、`IResSceneBackend`、`ISerializationProvider` 等；上层工具 Kit 也通过 `IAudioBackend`、`IInputBackend`、`ISceneBackend`、`IUIBackend` 等后端接口隔离宿主能力。SceneKit 默认委托 ResKit 的场景后端，UIKit 默认面板加载器通过 ResKit 加载面板，因此切换 Unity Resources / YooAsset / 项目自定义资源系统时，优先只替换 ResKit Provider。

Unity Adapter 提供 `UnityBootstrap`、`UnityResourceProvider`、`MonoSingleton<T>`、Unity 数学类型转换扩展、Unity CommandBridge Host、Tauri Launcher、事件/快照/遥测发布器、Editor UI Toolkit 组件与样式服务，以及 YooAsset、DOTween、FMOD、Unity Input、Unity UI 等可选集成。

Godot Adapter 提供 `GodotBootstrap`、`GodotAutoBootstrap`、`GodotResourceProvider`、`GodotSingleton<T>`、Godot CommandBridge Host、事件桥、FSM 桥、Kit 快照发布器，以及输入、场景、UI、存档等 Installer 入口。Godot 侧以 Godot 4.7 `.NET / C#` 项目为主要接入对象。

---

## AI 原生通信机制

YokiFrame 把 AI 访问设计成框架能力，而不是某个编辑器插件的附属功能。核心是 `.yokiframe/` 文件桥：

```text
.yokiframe/
└── engines/
    └── <engineId>/
        ├── engine.json
        ├── status/heartbeat.json
        ├── commands/<requestId>.json
        ├── results/<requestId>-response.json
        ├── snapshots/<kit>/<name>.json
        └── events/*.jsonl
```

### 命令面

AI、Tauri 和脚本都可以写入 engine-scoped 命令文件：

```json
{
  "protocolVersion": 2,
  "requestId": "codex-ping-001",
  "engineId": "unity-editor",
  "source": "codex",
  "kit": "System",
  "action": "ping",
  "payload": {},
  "createdAtUtc": "2026-06-24T00:00:00Z",
  "timeoutMs": 10000
}
```

响应会写入：

```text
.yokiframe/engines/<engineId>/results/<requestId>-response.json
```

协议保证：已接受命令必须产出一个 terminal response。未知 Kit、未知 action、解析失败、超时、策略拒绝都应变成标准 JSON 响应，而不是让调用方静默等待。

### 快照、事件和实时遥测

文件桥不是高频运行时总线。YokiFrame 将通信拆成多条通道：

| 通道 | 用途 |
| --- | --- |
| Command Plane | 请求-响应命令，例如 `System/ping`、`FsmKit/get_workbench_snapshot`。 |
| Snapshot Plane | 当前状态覆盖式快照，例如 `FsmKit/state`、`PoolKit/state`、`ResKit/state`。 |
| Event Plane | 重要离散事件 JSONL，例如 Kit 状态变化、生命周期提醒。 |
| Realtime Telemetry Plane | 共享内存最新帧，用于 Tauri 页面的人类感知实时刷新。 |
| Trace Plane | 显式开启的短期诊断 ring buffer。 |

AI Agent 默认读取 snapshot 或 command/result；Tauri 工作台优先读 telemetry，再读 snapshot，只有用户动作、详情查询或缺失兜底时才走 command/result。这样既能让 AI 稳定访问框架状态，也避免把热路径变成跨进程文件轮询。

### AI Skill

包内包含面向 Agent 的 Skill 文档：

```text
YokiFrame/Core/Editor/Skills/
├── yokiframe/
├── yokiframe-editor/
└── yokiframe-command-bridge/
```

Tauri 工作台也提供 AI Skill 安装入口，方便把项目内的 YokiFrame 使用说明同步到 Codex 等 Agent 的技能目录。AI 不需要直接猜测 Unity 内部对象；它可以先发现 engine registry，再按协议查询 Kit 状态。

---

## Tauri 可视化工作台

YokiFrame Editor 是一个 Tauri + Web 技术栈的桌面工作台。它不是营销页，而是给开发者在调试循环里使用的控制台：连接宿主、查看 Kit 状态、观察运行时变化、触发只读诊断命令、打开代码位置、管理生成器和阅读内置文档。

在 Unity / Godot 编辑器中通常可通过 `Ctrl+E` 打开工作台。

当前工作台页面包括：

| 页面 | 能力 |
| --- | --- |
| System | 引擎连接、heartbeat、engine registry、FileBridge 健康、命令目录和日志。 |
| Architecture | 当前架构实例、注册服务和服务实现诊断。 |
| EventKit | 事件注册关系、最近事件、代码扫描、发送/监听/注销关系图。 |
| FsmKit | 状态机列表、状态流图、当前状态、转换历史和生命周期事件。 |
| PoolKit | 对象池统计、池详情、峰值、缓存数量和当前快照。 |
| ResKit | 资源缓存、引用计数、Provider 状态和资源加载诊断。 |
| LogKit | 运行时日志、日志配置、文件输出和错误定位。 |
| ActionKit | 动作树、执行状态、堆栈追踪开关和当前动作统计。 |
| AudioKit | 活跃声音、音量总线、播放历史、音频 ID / 路径生成器。 |
| SaveKit | 存档槽、自动保存、存储/序列化/加密后端状态。 |
| LocalizationKit | 语言列表、Provider、formatter、缓存和语言切换命令。 |
| SceneKit | 场景加载状态、预加载、卸载诊断和场景后端。 |
| SpatialKit | 空间索引列表、实体统计、查询结构诊断。 |
| InputKit | 当前设备、动作状态、输入缓冲和上下文栈。 |
| UIKit | 面板列表、面板栈、层级、Unity 面板 Prefab 创建和绑定辅助。 |
| TableKit | Luban 环境检测、生成参数、输出目录、执行日志和生成校验。 |
| SingletonKit | Core / Unity / Godot 单例实例和生命周期状态。 |
| Docs | 快速上手、Kit 文档、API 速查和第三方依赖说明。 |

仓库内的前端开发源位于 `YokiFrameTools/TauriEditor/dist`；包内跨引擎运行副本位于：

```text
YokiFrame/TauriRuntime~/dist
```

---

## 安装和接入

<p align="center">
  <img src="Documentation~/images/yokiframe-installer.png" alt="YokiFrame Installer" width="720">
</p>

### 安装器

推荐使用随包发布的轻量安装器统一安装到 Unity 或 Godot 项目：

```text
YokiFrame/Installer~/win-x64/YokiFramePackageTool.exe
```

安装器会自动检测目标项目类型，并按目标引擎生成对应安装计划：

| 目标项目 | 支持方式 | 说明 |
| --- | --- | --- |
| Unity | 本地包 | 复制到目标项目 `Packages/com.hinatayoki.yokiframe`，适合离线使用或本地改源码。 |
| Unity | Git 包 | 写入目标项目 `Packages/manifest.json`，后续可通过 Unity Package Manager 更新。 |
| Godot | 本地包 | 安装到 Godot 4.7 `.NET / C#` 项目的 `addons/yokiframe/package/YokiFrame`，并创建 Godot 插件入口。 |

Unity 本地包模式和 Godot 模式需要选择 YokiFrame 源目录；Unity Git 包模式只需要选择目标 Unity 项目目录和 Git URL。

### Unity Git URL

Unity 项目也可以直接通过 Unity Package Manager 安装：

1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL`
3. 输入 Git URL：

```text
https://github.com/HinataYoki/YokiFrame.git
```

当前 GitHub `main` 的 Unity package 根目录位于仓库根目录，因此默认 URL 不带 `?path=`。如果需要锁定分支、tag 或 commit，可在 URL 末尾追加 `#branch-or-tag`。只有当目标分支或 tag 的 `package.json` 确实位于仓库子目录时，才需要手动追加 `?path=/子目录`。

### Unity 初始化

运行时最小初始化是一句话：

```csharp
using YokiFrame;

YokiFrameKit.Initialize(YokiFrameEngine.Unity);
```

`YokiFrameKit` 会通知当前已存在的 Kit installer 根据 Unity 宿主安装默认后端，例如 ResKit 的 `UnityResourceProvider`、LogKit 的 Unity logger，以及可选 Tool Kit 的 Unity 后端。只使用 EventKit、FsmKit、PoolKit 或纯 C# 单例时不强制初始化；使用 ResKit、AudioKit、InputKit、SceneKit、UIKit 等宿主能力时建议在项目启动阶段先调用。

如果项目切换到 YooAsset 或自定义资源系统，推荐只替换 ResKit Provider：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
```

内置 `UnityResourceProvider` 和 `YooAssetResourceProvider` 同时提供普通资源、raw 文件和场景加载能力。SceneKit 默认跟随当前 ResKit Provider；UIKit 默认 `DefaultPanelLoader` 也通过 `ResKit.LoadAsset<GameObject>()` 加载面板，不再提供 YooAsset 专用初始化入口或专用 PanelLoader。默认面板路径仍是 `Art/UIPrefab/<PanelName>`；如果 YooAsset 使用面板类型名作为可寻址 location，可在启动时设置：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
UIKit.GetPanelLoader().UseAddressableLocation = true;

// 如果还没有创建 UIKit 当前加载池，也可以先设置新建默认池的全局默认值
DefaultPanelLoaderPool.DefaultUseAddressableLocation = true;
```

如果希望继续使用场景生命周期自动驱动，可保留一个轻量 MonoBehaviour 外壳：

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

`UnityBootstrap` 内部同样调用统一入口，并在 `Update` / `OnDestroy` 中转发 `YokiFrameKit.Tick` 和 `YokiFrameKit.Shutdown`。它是可选生命周期外壳，不再是唯一初始化本体。

### Unity 常用适配辅助

Unity Adapter 的公共入口位于 `YokiFrame.Unity`。跨引擎 Runtime 继续使用 `YokiVector2`、`YokiVector3`、`YokiRect` 和 `YokiBounds`，Unity 业务代码可以通过扩展方法在 `Vector2`、`Vector3`、`Rect`、`Bounds` 之间双向转换，不需要在调用点手写字段映射。

```csharp
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

var bounds = new Bounds(Vector3.zero, Vector3.one * 1000f).ToYokiBounds();
var octree = SpatialKit.CreateOctree<MySpatialEntity>(bounds);

var position = transform.position.ToYokiVector3();
mIndex.QueryRadius(position, sensor.Range, mQueryBuffer);
```

Unity Editor 的 UI Toolkit 模板、设计令牌、图标和样式服务也在 `YokiFrame.Unity`。自定义 Inspector 或 EditorWindow 中使用 `YokiStyleService`、`YokiStyleProfile`、`KitIcons` 时导入该命名空间；需要直接写 `Spacing.SM`、`Colors.TextPrimary`、`Radius.LG` 或 `CreateModernToggle()` 时额外静态导入 `YokiFrameUIComponents`。

从 1.0 升级时，优先看 `TauriRuntime~/dist/docs/quick-start.md` 里的迁移速查。那里已经把 `UIPanel.Data`、SceneKit 事件、YooInit、AudioKit、SaveKit 和 Unity UI Toolkit 入口的实际对应关系整理好了。

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

### Godot

安装器会把包安装到 Godot 4.7 `.NET / C#` 项目，并创建项目根插件入口：

```text
addons/yokiframe/plugin.cfg
addons/yokiframe/plugin.gd
addons/yokiframe/package/YokiFrame/
```

Godot 插件启用后会注册 bootstrap、autoload、`.yokiframe` 工作目录和 Godot engine registry。运行时命令通过 `.yokiframe/engines/godot-runtime/commands/*.json` 进入，响应从同 engine 的 `results` 目录读取。

### 第三方增强

| 依赖 | 用途 |
| --- | --- |
| UniTask | Unity 项目中启用 `YOKIFRAME_UNITASK_SUPPORT` 后，ResKit / SaveKit 等异步 API 可切换为 `UniTask<T>`。 |
| YooAsset | Unity ResKit 可选资源后端。 |
| DOTween | ActionKit / UIKit 可选动画集成。 |
| FMOD | AudioKit 可选音频后端。 |
| Luban | TableKit 配置表生成工作流。 |

---

## 常用代码

### EventKit

```csharp
using YokiFrame;

public readonly struct PlayerDiedEvent
{
    public readonly string PlayerName;

    public PlayerDiedEvent(string playerName)
    {
        PlayerName = playerName;
    }
}

EventKit.Type.Register<PlayerDiedEvent>(OnPlayerDied);
EventKit.Type.Send(new PlayerDiedEvent("Player"));
EventKit.Type.UnRegister<PlayerDiedEvent>(OnPlayerDied);
```

### FsmKit

```csharp
using YokiFrame;

var fsm = new FSM<PlayerState>("PlayerFSM");
fsm.Add(PlayerState.Idle, new IdleState(fsm, owner));
fsm.Add(PlayerState.Run, new RunState(fsm, owner));
fsm.Start(PlayerState.Idle);

fsm.Change(PlayerState.Run);
fsm.Update();
```

### ResKit

```csharp
using YokiFrame;

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

### ActionKit

```csharp
using YokiFrame;

IActionController controller = ActionKit.Sequence()
    .Callback(OnStarted)
    .Delay(0.5f)
    .Callback(OnFinished)
    .Start();

controller.Pause();
controller.Resume();
controller.Cancel();
```

---

## 技术约束

- Unity 包声明兼容 Unity 2022.3+；当前仓库开发环境覆盖 Unity 6000.x。
- Godot 接入面向 Godot 4.7 `.NET / C#` 项目。
- C# 代码保持 C# 9.0 兼容，不使用 C# 10+ 语法。
- Core Runtime 不直接依赖 Tauri；跨进程通信通过 `.yokiframe` 协议和 Adapter 层实现。
- 文件桥协议字段使用安全 ASCII 标识符；命令写入应使用临时文件加原子重命名。
- 高频运行时状态不逐次写文件；由 Adapter 缓存、节流、snapshot 和 telemetry 承担可视化刷新。

---

## 文档入口

| 目标 | 位置 |
| --- | --- |
| 快速上手和 Kit 文档 | Tauri 工作台 `Docs` 页面 |
| Tauri 内置文档源 | `TauriRuntime~/dist/docs` |
| AI 命令桥 Skill | `Core/Editor/Skills/yokiframe-command-bridge/SKILL.md` |
| YokiFrame 使用 Skill | `Core/Editor/Skills/yokiframe/SKILL.md` |

---

## License

MIT License
