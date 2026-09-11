# Kit 能力索引

本文件供用户游戏项目中的 AI 选择 Kit 并判断完成度。Runtime API、Kit Interaction、Workbench 三层分别判断；文档存在、旧入口或空程序集不构成已实现证据。

| 能力 | Runtime API | Kit Interaction | Workbench | 如何自证 | 典型用途与入口 | 人类主页面 |
|---|---|---|---|---|---|---|
| Architecture | 已实现 | 已实现 | 不设专页 | `command send --kit Architecture --action list_architectures` | 组织服务、模型、系统：`Architecture<T>` | `Api/01-Architecture/Architecture.md` |
| EventKit | 已实现 | 已实现 | 已实现 | `command send --kit EventKit --action get_workbench_snapshot` | 跨模块通知：`EventKit.Type`、`EventKit.Enum` | `Api/02-Core/EventKit.md` |
| FsmKit | 已实现 | 已实现 | 已实现 | `command send --kit FsmKit --action list_all` | 业务状态机：`FSM<TEnum>`、`FSM<TEnum, TArgs>` | `Api/02-Core/FsmKit.md` |
| LogKit | 已实现 | 已实现 | 已实现 | `command send --kit LogKit --action get_workbench_snapshot` | 分级日志：`LogKit` | `Api/02-Core/LogKit.md` |
| PoolKit | 已实现 | 已实现 | 已实现 | `command send --kit PoolKit --action check_leak` | 对象复用：`PoolKit`、`PoolKit.Shared` | `Api/02-Core/PoolKit.md` |
| ResKit | 已实现 | 已实现 | 已实现 | `command send --kit ResKit --action stats` | 资源加载与所有权：`ResKit`、`IResourceProvider`、`IResSceneProvider` | `Api/02-Core/ResKit.md` |
| SingletonKit | 已实现 | 未完成 | 未完成 | 无 Runtime 状态；编译 + `Core/Tests` 单测 | 纯 C# 单例：`Singleton<T>`、`SingletonKit<T>` | `Api/02-Core/SingletonKit.md` |
| ToolClass | 已实现 | 不适用 | 不适用 | 无 Runtime 状态；`Core/Tests` 单测 | 基础工具类型：`BindValue<T>`、`FastDictionary<TKey,TValue>`、`PooledLinkedList<T>`、`SpanSplitter` | `Api/02-Core/ToolClass.md` |
| CodeGenKit | 已实现，Editor/Tools | 不适用 | 不适用 | 仅 Editor 编译期：`compile unity` 0 错误 | 编辑器代码生成：`CodeGenKit` | `Api/02-Core/CodeGenKit.md` |
| InspectorKit | 已实现，Unity Adapter | 不适用 | 不适用 | 仅 Unity 编译期：`compile unity` 0 错误 | Inspector 元数据：`InspectorKitEditor`、`InspectorKitUi` | `Api/02-Core/InspectorKit.md` |
| ActionKit | 已实现 | 已实现 | 已实现 | `command send --kit ActionKit --action stats` | 动作与流程编排：`ActionKit`、`IActionController` | `Api/03-Tool/ActionKit.md` |
| AudioKit | 已实现 | 已实现 | 已实现 | `command send --kit AudioKit --action stats`；索引走 `audio index scan` | 音频播放：`AudioKit`、`AudioVoiceHandle` | `Api/03-Tool/AudioKit.md` |
| SceneKit | 已实现 | 不规划 | 不规划 | `kit status --kit SceneKit`（无 Interaction，仅 snapshot） | 场景切换：`SceneKit`、`SceneHandler` | `Api/03-Tool/SceneKit.md` |
| LocalizationKit | 已实现 | 未完成 | 已实现，standalone JSON 或 Luban Excel 预览 | `localization check --source <path>` | 本地化：`LocalizationKit`、`ILocalizationProvider` | `Api/03-Tool/LocalizationKit.md` |
| SaveKit | 已实现 | 已实现：`stats`、`get_workbench_snapshot` 只读 | 已实现：配置、文件元信息与 Runtime 摘要 | `command send --kit SaveKit --action stats` | 存档读写：`SaveKit`、`SaveTarget`、`SaveData` | `Api/03-Tool/SaveKit.md` |
| SpatialKit | 已实现 | 已实现 | 已实现 | `spatialkit stats --engine <engineId>` | 空间查询：`SpatialKit`、`ISpatialIndex<T>` | `Api/03-Tool/SpatialKit.md` |
| TableKit | 已实现，生成后 | 未完成 | 已实现，Luban 生成 | 生成前无 Runtime 类型；以 Workbench TableKit 页面校验通过为准 | 配表读取：TableKit 生成产物；配表 AI 走 [tablekit-luban.md](tablekit-luban.md) | `Api/03-Tool/TableKit.md` |
| UIKit | 已实现，Unity 专属 | 已实现，Unity Editor | 已实现 | `command send --kit UIKit --action stats`（仅 Unity） | Unity UI 面板：`UIKit`、`UIPanel` | `Api/03-Tool/UIKit.md` |
| BuffKit / InputKit | 已废弃 | 不迁入 | 不迁入 | 不适用 | 无 | 不恢复 |

## 使用约束

- Runtime API 已实现不代表在线 Provider、CLI action 或 Workbench 页面存在
- 上表 `command send` 与 `kit status` 都需要宿主在线；每个命令的实际形态以 `harness catalog --refresh-commands` 观察到的 action 为准，本表只给一条可用入口
- Runtime state、capability catalog、snapshot、telemetry 和 command 统一通过 `yoki` CLI 核实，命令见 [cli-commands.md](cli-commands.md)
- 已完成 Workbench 只表示有 Application 强类型 read model 和真实页面，不表示可从 Workbench 修改 Runtime 业务状态
- TableKit 未生成时不向项目或包宣称存在 Runtime 类型
- UIKit 只在 Unity 使用；Godot 不发布 UIKit capability、Provider 或占位状态

## 核实顺序

1. 用本表选取已实现入口
2. 写业务代码前读对应人类主页面取最小骨架与完整签名；通用约束和易错点见 [usage-patterns.md](usage-patterns.md)
3. 改完先用本表「如何自证」列确认能力仍然可用；需要最小状态时用 `command send` 的只读 action 或 `kit status`
4. 需要页面能力或安装事务时读 [workbench-pages.md](workbench-pages.md) 与 [installer.md](installer.md)
