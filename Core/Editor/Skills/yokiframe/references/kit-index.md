# Kit 能力索引

本文件是 AI 判断当前能力完成度的紧凑事实表。Runtime API、Kit Interaction、Workbench 页面分别判断；文档存在、旧入口或空程序集不构成已实现证据。

| 能力 | Runtime API | Kit Interaction | Workbench | 推荐入口 | 人类主页面 |
|---|---|---|---|---|---|
| Architecture | 已实现 | 已实现 | 不设专页 | `Architecture<T>` | `Api/01-Architecture/Architecture.md` |
| EventKit | 已实现 | 已实现 | 已实现 | `EventKit.Type`、`EventKit.Enum` | `Api/02-Core/EventKit.md` |
| FsmKit | 已实现 | 已实现 | 已实现 | `FSM<TEnum>`、`FSM<TEnum, TArgs>` | `Api/02-Core/FsmKit.md` |
| LogKit | 已实现 | 已实现 | 已实现 | `LogKit` | `Api/02-Core/LogKit.md` |
| PoolKit | 已实现 | 已实现 | 已实现 | `PoolKit`、`PoolKit.Shared` | `Api/02-Core/PoolKit.md` |
| ResKit | 已实现 | 已实现 | 已实现 | `ResKit`、`IResourceProvider`、`IResSceneProvider` | `Api/02-Core/ResKit.md` |
| SingletonKit | 已实现 | 未完成 | 未完成 | `Singleton<T>`、`SingletonKit<T>` | `Api/02-Core/SingletonKit.md` |
| ToolClass | 已实现 | 不适用 | 不适用 | `BindValue<T>`、`FastDictionary<TKey,TValue>`、`PooledLinkedList<T>`、`SpanSplitter` | `Api/02-Core/ToolClass.md` |
| CodeGenKit | 已实现，Editor/Tools | 不适用 | 不适用 | `CodeGenKit` | `Api/02-Core/CodeGenKit.md` |
| InspectorKit | 已实现，Unity Adapter | 不适用 | 不适用 | Inspector 元数据、`InspectorKitEditor`、`InspectorKitUi` | `Api/02-Core/InspectorKit.md` |
| ActionKit | 已实现 | 已实现 | 已实现 | `ActionKit`、`IActionController` | `Api/03-Tool/ActionKit.md` |
| AudioKit | 已实现 | 已实现 | 已实现 | `AudioKit`、`AudioVoiceHandle` | `Api/03-Tool/AudioKit.md` |
| SceneKit | 已实现 | 不规划 | 不规划 | `SceneKit`、`SceneHandler` | `Api/03-Tool/SceneKit.md` |
| LocalizationKit | 已实现 | 未完成 | 已实现，standalone JSON 或 Luban Excel 预览 | `LocalizationKit`、`ILocalizationProvider` | `Api/03-Tool/LocalizationKit.md` |
| SaveKit | 已实现 | 已实现：`state`、`stats`、`get_workbench_snapshot` 均只读 | 已实现：配置、文件元信息与 Runtime 摘要 | `SaveKit`、`SaveTarget`、`SaveData` | `Api/03-Tool/SaveKit.md` |
| SpatialKit | 已实现 | 已实现 | 已实现 | `SpatialKit`、`ISpatialIndex<T>` | `Api/03-Tool/SpatialKit.md` |
| TableKit | 已实现，生成后 | 未完成 | 已实现，Luban 生成 | Workbench TableKit 页面与生成门面 | `Api/03-Tool/TableKit.md` |
| UIKit | 已实现，Unity 专属 | 已实现，Unity Editor | 已实现 | `UIKit`、`UIPanel` | `Api/03-Tool/UIKit.md` |
| BuffKit / InputKit | 已废弃 | 不迁入 | 不迁入 | 无 | 不恢复 |

## 使用约束

- Runtime API 已实现不代表在线 Provider、CLI action 或 Workbench 页面存在
- Runtime state、capability catalog、snapshot、telemetry 和 command 统一使用 `yokiframe-cli` 核实
- 已完成 Workbench 只表示有 Application 强类型 read model 和真实页面，不表示可从 Workbench 修改 Runtime 业务状态
- TableKit 未生成时不向项目或包宣称存在 Runtime 类型
- UIKit 只在 Unity 使用；Godot 不发布 UIKit capability、Provider 或占位状态
- ResKit Workbench 已实现；不要再把它标为未完成

## 关键边界

| 能力 | 不可省略的约束 |
|---|---|
| EventKit | 新代码优先 `Type`/`Enum`；订阅令牌由业务 owner 注销 |
| ResKit / SceneKit | 每个 handle/Handler 保持创建 Provider/Backend 的所有权；显式注入优先，读取不创建默认后端 |
| ActionKit | Start、Tick、暂停和恢复在同一宿主线程；跨线程仅允许 `Cancel()` |
| AudioKit | 保留完整 `AudioVoiceHandle`；自定义 Bus 优先显式注册；Workbench/Interaction 只读，后端切换不复用旧 generation |
| SaveKit | `Slot` 与 `Global` 用 `SaveTarget` 区分；Interaction 只读已存在后端和容器头，绝不读取 payload 或创建默认后端 |
| SpatialKit | 实体 `SpatialId` 稳定唯一；写入与 Snapshot 创建不并发 |
| UIKit | 每种 Panel 类型一个实例；Root 创建后不可替换；项目定制使用 Prefab Variant + `UIKit.SetRootPrefab` |

## 核实顺序

1. 用本表选取已实现入口
2. 阅读对应人类主页面和公开源码确认签名与约束
3. 需要运行态证据时转入 `yokiframe-cli`
4. 需要页面或安装流程时转入 `yokiframe-workbench`
