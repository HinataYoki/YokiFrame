# Changelog

本文件记录 YokiFrame 当前发布线的重要变化。

YokiFrame 2.0 是一次跨引擎架构重启，不再沿用 1.x 的 Unity 单宿主演进记录。因此当前包内 changelog 从 `2.0.0-pre` 重新开始；1.x 历史更新记录已从本文件移除。

格式参考 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)，版本号遵循 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)。

## [2.0.0-pre] - 2026-06-24

### Added

- 新增 YokiFrame 2.0 跨引擎 Kit 框架定位。
  - Runtime API 围绕 `YokiFrame` 命名空间下的统一 Kit 入口组织。
  - Unity、Godot 和未来宿主通过 Adapter 接入，不再扩散平行公开 API。
  - Core Runtime 覆盖 Architecture、EventKit、FsmKit、PoolKit、ResKit、SingletonKit、LogKit、ToolClass、FluentApi、Settings 和 CommandBridge。
- 新增 `.yokiframe/` AI 原生文件通信桥。
  - 通过 `.yokiframe/engines/<engineId>/engine.json` 发现引擎实例。
  - 命令使用 engine-scoped `commands/<requestId>.json` 与 `results/<requestId>-response.json`。
  - 当前状态通过 `snapshots/<kit>/<name>.json` 覆盖式发布。
  - 重要离散事件通过 JSONL event stream 发布。
  - 共享内存 telemetry 支持 Tauri 工作台实时刷新，并保留 snapshot / command 兜底。
- 新增 FileBridge v2 协议能力。
  - 命令 envelope 包含 `protocolVersion`、`requestId`、`engineId`、`source`、`kit`、`action`、`payload`、`createdAtUtc` 和可选 timeout 字段。
  - 新命令优先走 engine-scoped 路由；legacy root 路径在迁移期保留兼容。
  - 协议包含安全标识符校验、根目录约束、标准终态响应、命令认领、archive / deadletter、重复响应复用和存储清理模型。
- 新增 Tauri 版 YokiFrame Editor 工作台。
  - System 页面展示 engine registry、heartbeat、FileBridge 健康、命令目录和日志。
  - Kit 页面覆盖 Architecture、EventKit、FsmKit、PoolKit、ResKit、LogKit、ActionKit、AudioKit、SaveKit、LocalizationKit、SceneKit、SpatialKit、InputKit、UIKit、TableKit、SingletonKit 和 Docs。
  - 工作台页面优先读 telemetry，其次读 snapshot，缺失或用户显式操作时才走 command/result。
  - Docs 页面承载快速上手、Kit 文档、API 速查、第三方依赖建议和前端结构说明。
- 新增包内 AI Skill 入口。
  - `Core/Editor/Skills/yokiframe`
  - `Core/Editor/Skills/yokiframe-editor`
  - `Core/Editor/Skills/yokiframe-command-bridge`
  - Tauri 工作台提供 AI Skill 安装和状态查看能力。
- 新增 Unity Adapter 能力。
  - `UnityBootstrap`、Unity logger / time / object / serialization / resource provider、`MonoSingleton<T>`、runtime settings bridge 和 Runtime Kit installer。
  - Unity Editor CommandBridge host、heartbeat、engine registry、Tauri launcher、Tauri packager、event writer、snapshot publisher 和 shared-memory telemetry publisher。
  - 可选集成 YooAsset、DOTween、FMOD、Unity Input、SceneManager 和 Unity UI 后端。
- 新增 Godot Adapter 能力。
  - `GodotBootstrap`、`GodotAutoBootstrap`、Godot resource / time / object / logger / serialization provider、`GodotSingleton<T>` 和 Runtime Kit installer。
  - Godot CommandBridge host、event writer、EventKit bridge、FsmKit bridge、Kit snapshot publisher 和 Godot editor plugin 入口。
  - Godot `.NET / C#` 项目通过 `addons/yokiframe/package/YokiFrame` 安装布局接入。
- 新增 Tool Kit 工作台与命令桥覆盖。
  - ActionKit、AudioKit、SaveKit、InputKit、LocalizationKit、SceneKit、SpatialKit、UIKit 以及核心 Kit 诊断已提供 command handler 或 snapshot 工作流。
  - TableKit 作为 Tauri 内 Luban 配置表生成工作流呈现，不作为运行时 Kit 发布。
- 新增 2.0 预发布包元数据。
  - `package.json` 版本更新为 `2.0.0-pre`。
  - 包描述和关键词已改为跨引擎、AI 原生 FileBridge、Tauri 工作台、Unity / Godot Adapter、诊断与当前 Kit 集合。

### Changed

- 将 YokiFrame 从 Unity 单宿主轻量框架重新定位为跨引擎工具集框架。
- 重写包内 README。
  - README 现在从跨引擎架构、AI 原生通信和可视化编辑器工具开始介绍。
  - 移除旧截图、阶段草稿和过时目录引用。
  - 安装与接入说明覆盖 Unity 和 Godot 两条路径。
- 更新包文档中的当前目录口径。
  - 当前包文档指向 `Core/Runtime`、`Core/Runtime/Adapters`、`Core/Editor/Skills`、`Tools` 和 `Installer~`。
  - Tauri 前端开发源与包内跨引擎运行副本分开说明。
- 统一 FileBridge v2 文档表述。
  - AI 和脚本应先发现 engine registry，再优先读取 snapshot，需要请求-响应语义时再发送 command。
  - 高频运行时状态不逐次写文件，由 Adapter 聚合到 telemetry、snapshot 和采样 event。
- 更新 package keywords。
  - 新关键词突出 `cross-engine`、`ai`、`filebridge`、`command-bridge`、`tauri`、`workbench`、`diagnostics`、`unity`、`godot` 和当前 Kit 名称。

### Removed

- 移除当前 changelog 中所有 1.x 版本更新记录。
  - 1.x 仅作为历史背景，不再在当前包内 changelog 维护。
  - 本文件从 `2.0.0-pre` 开始记录 2.0 发布线。
- 移除旧 changelog 中以 Unity 单宿主包为中心的叙述方式。

### Notes

- `2.0.0-pre` 是预发布架构重启版本。
- `package.json` 仍声明 Unity 2021.3+ 兼容；当前仓库开发环境同时覆盖 Unity 6000.x。
- Godot 支持面向 Godot `.NET / C#` 项目，通过安装器和 Adapter 入口接入。
- FileBridge legacy root 目录仍在迁移期保留；新工具应优先使用 `.yokiframe/engines/<engineId>/` 下的 engine-scoped 路径。
