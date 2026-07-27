# 框架概览

这是 Workbench 的唯一入门页。先用本页了解 YokiFrame 能解决什么问题，再打开对应 Kit 的详细文档。安装和 AI 自动化安装请从仓库 README 进入专门的安装指引。

## 适用场景

YokiFrame 是面向 Unity 2022.3+ 和 Godot .NET 的跨宿主 C# 游戏框架。跨引擎的业务规则放在纯 C# Core，引擎 API、生命周期和默认后端由匹配的 Adapter 提供。

它适合需要复用以下能力的游戏项目：

- 组织服务、模型和系统
- 解耦模块通知，管理状态和业务流程
- 管理资源、场景、对象池、单例和日志
- 编排动作、等待、并行和异步流程
- 处理音频、存档、本地化、空间索引和数据表
- 在 Unity 中搭建 UI、Inspector 和编辑器生成流程

YokiFrame 不替代 Unity 或 Godot 编辑器，也不负责通用 Scene、Prefab、Asset、Play Mode、截图或输入自动化。这些工作仍由对应引擎或专用工具完成。

## 能做什么

| 目标 | 主要入口 |
|---|---|
| 组合服务、模型和系统 | `Architecture<T>` |
| 解耦模块之间的通知 | `EventKit.Type`、`EventKit.Enum` |
| 管理状态和状态转换 | `FSM<TEnum>`、`FSM<TEnum,TArgs>` |
| 编排顺序、并行、条件、延迟和异步流程 | `ActionKit` |
| 记录日志、复用对象、管理单例 | `LogKit`、`PoolKit`、`SingletonKit` |
| 加载资源、管理 handle 和场景生命周期 | `ResKit`、`SceneKit` |
| 播放音频、保存数据和本地化 | `AudioKit`、`SaveKit`、`LocalizationKit` |
| 查询实体空间位置 | `SpatialKit` |
| 从 Luban 数据表生成 C# 类型并在运行时读取 | `TableKit` |
| 搭建 Unity 面板、绑定和 Inspector | `UIKit`、`InspectorKit` |
| 生成编辑器侧 C# 代码 | `CodeGenKit` |

## 选择入口

不同入口解决不同问题，不要把 Workbench 或 CLI 当成 Runtime API 使用。

| 你要做什么 | 从哪里开始 | 说明 |
|---|---|---|
| 编写游戏业务和跨宿主规则 | 对应 Runtime Kit | 业务代码调用公开 API；宿主差异由 Adapter 处理 |
| 查看当前项目、运行态和 Kit 证据 | Workbench | 只显示已有真实数据链路的页面，默认用于观察和诊断 |
| 脚本化读取、诊断或执行已声明操作 | `yoki` CLI | 默认只读；改变项目或宿主状态的命令需要明确触发 |
| 安装、更新或回滚 YokiFrame | Installer | 安装流程由专门的 AI 安装指引说明 |
| 操作 Scene、Prefab、Asset、Play Mode、截图或输入 | Unity/Godot 或外部工具 | 不属于 YokiFrame Runtime API |

## 三层状态

一个 Kit 的三个状态分别回答不同问题，不能相互推导：

- **Runtime API**：业务代码能否在游戏运行时调用公开 API。
- **Kit Interaction**：Editor/Tools 条件下是否能读取运行态，或执行已声明的受控操作。
- **Workbench**：是否已有强类型数据模型和真实页面可供人工查看。

Workbench 页面存在不代表可以修改 Runtime；Runtime API 已实现也不代表一定有 CLI action 或 Workbench 页面。

## Kit 状态

状态按当前公开 API、运行态接入和 Workbench 页面分别记录。具体签名、示例和生命周期约束以 Kit 主页面为准。

| Kit | Runtime API | Kit Interaction | Workbench | 详细文档 |
|---|---|---|---|---|
| Architecture | 已实现 | 已实现 | 无专页 | [Architecture](../01-Architecture/Architecture.md) |
| EventKit | 已实现 | 已实现 | 已实现 | [EventKit](../02-Core/EventKit.md) |
| FsmKit | 已实现 | 已实现 | 已实现 | [FsmKit](../02-Core/FsmKit.md) |
| LogKit | 已实现 | 已实现 | 已实现 | [LogKit](../02-Core/LogKit.md) |
| PoolKit | 已实现 | 已实现 | 已实现 | [PoolKit](../02-Core/PoolKit.md) |
| ResKit | 已实现 | 已实现 | 已实现 | [ResKit](../02-Core/ResKit.md) |
| SingletonKit | 已实现 | 未完成 | 未完成 | [SingletonKit](../02-Core/SingletonKit.md) |
| ToolClass | 已实现 | 不适用 | 不适用 | [ToolClass](../02-Core/ToolClass.md) |
| CodeGenKit | 已实现（Editor/Tools） | 不适用 | 不适用 | [CodeGenKit](../02-Core/CodeGenKit.md) |
| InspectorKit | 已实现（Unity Adapter） | 不适用 | 不适用 | [InspectorKit](../02-Core/InspectorKit.md) |
| ActionKit | 已实现 | 已实现 | 已实现 | [ActionKit](../03-Tool/ActionKit.md) |
| AudioKit | 已实现 | 已实现（只读） | 已实现（观察与索引） | [AudioKit](../03-Tool/AudioKit.md) |
| SceneKit | 已实现 | 不提供 | 不提供 | [SceneKit](../03-Tool/SceneKit.md) |
| LocalizationKit | 已实现 | 未完成 | 已实现（项目源与预览） | [LocalizationKit](../03-Tool/LocalizationKit.md) |
| SaveKit | 已实现 | 已实现（只读） | 已实现（摘要与配置） | [SaveKit](../03-Tool/SaveKit.md) |
| SpatialKit | 已实现 | 已实现 | 已实现 | [SpatialKit](../03-Tool/SpatialKit.md) |
| TableKit | 已实现（生成后） | 未完成 | 已实现（Luban 生成） | [TableKit](../03-Tool/TableKit.md) |
| UIKit | 已实现（Unity 专属） | 已实现（Unity Editor） | 已实现 | [UIKit](../03-Tool/UIKit.md) |

## 关键边界

- Core 不引用 Unity、Godot、Avalonia 或可选第三方库；宿主 Adapter 只负责 API 映射、生命周期和组合，并单向依赖 Core。
- 事件订阅、资源 handle、状态机、动作 controller 和异步工作都需要明确 owner；owner 退出时必须注销、释放或取消。
- 业务代码应在宿主生命周期中主动驱动状态机和动作 tick；框架不会替项目猜测业务生命周期。
- 显式注入的 Provider 或 Backend 始终优先；默认实现只在第一次真实业务调用时按需创建，读取和诊断不应隐式创建业务后端。
- SceneKit 只有 Runtime API，不提供 Interaction、CLI action 或 Workbench 页面。
- TableKit 是离线生成入口；项目尚未生成代码时，不存在对应的 Runtime 类型。
- AudioKit 和 SaveKit 的 Interaction 以只读观察为主，不提供会改变运行时业务状态的控制操作。
- UIKit 只支持 Unity，不为 Godot 提供兼容壳或占位能力。

## 从这里继续

先按上表打开要使用的 Kit 主页面。Core Kit 位于 `Api/02-Core`，游戏功能 Kit 位于 `Api/03-Tool`，第三方依赖建议位于 [Reference](../04-Reference/01-ThirdPartyRecommendations.md)。
