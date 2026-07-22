# 状态与入口

> 面向读者：需要确认某个 Kit 当前是否可用于 Runtime、运行态观察或 Workbench 的开发者
>
> 主要入口：对应 Kit 的 API 主页面
>
> 运行边界：状态按 Runtime API、Kit Interaction、Workbench 三层分别判断
>
> 核实依据：当前公开源码、Provider/页面实现和测试

## 如何阅读状态

`Runtime API` 表示业务代码可调用的实现；`Kit Interaction` 表示 Editor/Tools 条件下可发布观察或受控 action；`Workbench` 表示已有 Application 强类型 read model 和真实 Avalonia 页面。三层互不推导。

| 能力 | Runtime API | Kit Interaction | Workbench | 主页面 |
|---|---|---|---|---|
| Architecture | 已实现 | 已实现 | 不设专页 | [Architecture](../01-Architecture/Architecture.md) |
| EventKit | 已实现 | 已实现 | 已实现 | [EventKit](../02-Core/EventKit.md) |
| FsmKit | 已实现 | 已实现 | 已实现 | [FsmKit](../02-Core/FsmKit.md) |
| LogKit | 已实现 | 已实现 | 已实现 | [LogKit](../02-Core/LogKit.md) |
| PoolKit | 已实现 | 已实现 | 已实现 | [PoolKit](../02-Core/PoolKit.md) |
| ResKit | 已实现 | 已实现 | 已实现 | [ResKit](../02-Core/ResKit.md) |
| SingletonKit | 已实现 | 未完成 | 未完成 | [SingletonKit](../02-Core/SingletonKit.md) |
| ToolClass | 已实现 | 不适用 | 不适用 | [ToolClass](../02-Core/ToolClass.md) |
| CodeGenKit | 已实现，Editor/Tools | 不适用 | 不适用 | [CodeGenKit](../02-Core/CodeGenKit.md) |
| InspectorKit | 已实现，Unity Adapter | 不适用 | 不适用 | [InspectorKit](../02-Core/InspectorKit.md) |
| ActionKit | 已实现 | 已实现 | 已实现 | [ActionKit](../03-Tool/ActionKit.md) |
| AudioKit | 已实现 | 已实现：只读 `state`、`stats`、`get_workbench_snapshot` | 已实现：Bus/播放观察与索引生成 | [AudioKit](../03-Tool/AudioKit.md) |
| SceneKit | 已实现 | 不提供 | 不提供 | [SceneKit](../03-Tool/SceneKit.md) |
| LocalizationKit | 已实现 | 未完成 | 已实现，项目源目录 | [LocalizationKit](../03-Tool/LocalizationKit.md) |
| SaveKit | 已实现 | 已实现：只读 `state`、`stats`、`get_workbench_snapshot` | 已实现：配置、文件元信息和 Runtime 摘要 | [SaveKit](../03-Tool/SaveKit.md) |
| SpatialKit | 已实现 | 已实现 | 已实现 | [SpatialKit](../03-Tool/SpatialKit.md) |
| TableKit | 已实现，生成后 | 未完成 | 已实现，Luban 生成 | [TableKit](../03-Tool/TableKit.md) |
| UIKit | 已实现，Unity 专属 | 已实现，Unity Editor | 已实现 | [UIKit](../03-Tool/UIKit.md) |

## 当前边界

- ResKit 已完成 Workbench 资源、Lease 来源和卸载历史页面
- SceneKit 有 Runtime API 与宿主场景后端，但明确不提供 Interaction、CLI action 或 Workbench 页面
- TableKit 是离线生成入口；项目未生成时没有 TableKit Runtime 类型
- LocalizationKit 的 Workbench 页面不代表它已发布 Runtime Interaction Provider；SaveKit Interaction 只发布安全摘要，不读取 payload 或创建默认后端
- AudioKit Workbench 与 Interaction 不提供停止、音量、静音或清历史 action；索引生成只写项目代码与 manifest，不修改 Runtime
- UIKit 只支持 Unity，不提供 Godot capability、Adapter 或占位状态

## 相关资料

- 选择 Runtime、CLI、Workbench 或 Installer，参见 [新版入口总览](Entrypoints.md)
- 使用 Workbench、`yoki` 和 Installer，参见 [Workbench、CLI 与 Installer](../../Guides/Tooling.md)
- API 细节、生命周期和宿主约束以各 Kit 主页面为准
