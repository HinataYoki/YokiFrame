# YokiFrame 框架概览

YokiFrame 是面向 Unity / Godot 游戏项目的 C# Kit 框架。它提供统一的运行时 API、跨引擎适配层、AI 可读的诊断通信，以及用于查看 Kit 状态的工作台。

## 组成

| 部分 | 作用 |
|---|---|
| Kit API | 游戏代码直接调用的能力入口，例如 `EventKit`、`FsmKit`、`PoolKit`、`ResKit`、`UIKit`。 |
| Adapter / Backend | 连接 Unity、Godot 或项目自定义系统，把引擎差异挡在业务代码外。 |
| AI 通信层 | 让 AI 读取引擎和 Kit 状态，发送只读诊断请求，拿到结构化结果。 |
| 工作台 | 给开发者查看连接状态、Kit 状态、事件关系、资源引用、对象池、UI 栈等信息。 |

## Kit 分组

| 分组 | Kit | 主要用途 |
|---|---|---|
| Core | Architecture | 项目级服务和模型容器。 |
| Core | EventKit | 强类型事件、枚举事件、模块解耦。 |
| Core | FsmKit | 普通状态机、带参状态机、层级状态机。 |
| Core | PoolKit | 普通对象池、可回收对象池、临时集合池。 |
| Core | ResKit | 资源加载、raw 文件、引用计数、Provider 替换。 |
| Core | SingletonKit | 纯 C#、Unity、Godot 单例生命周期。 |
| Tool | ActionKit | Delay、Callback、Sequence、Parallel、Repeat。 |
| Tool | AudioKit | 音效、音乐、总线音量、活跃 voice。 |
| Tool | UIKit | 面板打开关闭、层级、缓存、返回栈。 |
| Tool | SaveKit | 多槽位存档、版本、加密、自动保存。 |
| Tool | LocalizationKit | 语言 Provider、文本格式化、Binder。 |
| Tool | SceneKit | 场景加载、预加载、激活、卸载。 |
| Tool | SpatialKit | HashGrid、Quadtree、Octree 空间查询。 |
| Tool | TableKit | Luban 配置表生成和运行时读取约定。 |

## 跨引擎方式

业务层只引用 `YokiFrame` 命名空间下的 Kit API。Unity / Godot 差异放在 Adapter、Provider 或 Backend 中。

```text
Game Code
  -> YokiFrame Kit API
  -> Interfaces / Provider / Backend
  -> Unity Adapter or Godot Adapter
```

常见替换点：

| 能力 | 替换接口 |
|---|---|
| 资源加载 | `IResourceProvider` / `IRawResourceProvider` |
| 音频 | `IAudioBackend` |
| 场景 | `ISceneBackend` |
| UI | `IUIBackend` |
| 存档 | `ISaveStorage` |

## AI 诊断通信

YokiFrame 会把引擎状态、Kit 快照和诊断结果写到 `.yokiframe/engines/<engineId>`。AI 不需要猜 Unity 或 Godot 内部对象，可以直接按 Kit 查询状态。

| AI 可以做什么 | 例子 |
|---|---|
| 确认引擎是否在线 | 查看 engine registry、heartbeat。 |
| 读取 Kit 当前状态 | 查看 FsmKit 当前状态、PoolKit 借出数量、ResKit 引用计数。 |
| 请求一次性诊断 | 让对应 Kit 返回工作台快照。 |
| 辅助 Debug | 根据事件注册、状态机转换、资源引用、对象池活跃对象给出排查路径。 |

AI 默认用于只读诊断。删除存档、停止音频、切换语言、卸载场景等会改变运行状态的操作，需要明确用户意图。

## 工作台能看什么

| 页面 | 适合排查 |
|---|---|
| System | 引擎连接、框架通信、运行日志、AI Skill 安装。 |
| EventKit | 事件注册、最近事件、发送/监听/注销代码位置。 |
| FsmKit | 状态机列表、当前状态、转换历史。 |
| PoolKit | 池容量、借出对象、峰值、疑似泄漏。 |
| ResKit | Provider、已加载资源、引用计数、卸载历史。 |
| UIKit | 面板缓存、面板状态、层级、面板栈。 |
| 其它 Kit 页面 | 对应后端、状态、历史和健康信息。 |

## 阅读顺序

| 目标 | 文档 |
|---|---|
| 第一次接入 | `快速上手` |
| 管理项目级服务 | `Architecture` |
| 事件通信 | `EventKit` |
| 状态机 | `FsmKit` |
| 资源后端 | `ResKit` |
| UI 面板 | `UIKit` |
| 配置表 | `TableKit` |
