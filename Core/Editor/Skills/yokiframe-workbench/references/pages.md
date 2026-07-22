# Workbench 页面参考

本文件记录当前真实导航和 AI 可据此说明的页面边界。页面可见不等于能修改 Runtime 业务状态；wire JSON 由 `Tooling.Application` 解析，Avalonia 不直接读取 `.yokiframe`。

## 当前导航

| 分组 | 页面 | 可见事实 | 显式写入或 action |
|---|---|---|---|
| 工作台 | 框架 | 项目、engine、heartbeat、Doctor、命令桥、Skill 与运行日志 | 命令桥只用于真实链路验证 |
| 工作台 | 文档 | 包内离线 Markdown、目录、关键词全文搜索的摘要、正文高亮与首个命中定位 | 无 |
| Core | EventKit | 静态事件关系、Runtime 监听数和时间线 | 无事件触发或监控开关 |
| Core | FsmKit | 实例、当前状态、已观测转换和历史 | 只读 |
| Core | LogKit | 项目 Runtime 配置、会话状态、内存历史和按需文件尾读 | 显式保存项目配置；可对当前会话发送已声明设置 action |
| Core | PoolKit | 池压力、对象明细、事件和借出候选 | 跟踪、堆栈、历史和泄漏检查均为显式操作 |
| Core | ResKit | Provider、资源、Lease 来源和卸载历史 | 只允许已声明跟踪/历史 action；不清缓存、不释放资源、不切换 Provider |
| Tools | ActionKit | 活动根、动作树、终态和调用帧 | 仅显式开关或清空堆栈 |
| Tools | AudioKit | 按 Bus 查看 active voice、播放进度、播放历史和稳定音频索引 | Runtime 只读；仅索引生成写项目代码与 manifest |
| Tools | SpatialKit | 索引、分区、投影密度、热点和分析 | 只读；不修改实体 |
| Tools | UIKit | Unity Runtime 诊断、Panel Prefab、Bind 和代码生成工具 | 仅 Unity Editor 用户 action；不远程控制 Runtime UI |
| Tools | TableKit | Luban 配置校验、生成和临时预览 | 显式生成项目代码 |
| Tools | LocalizationKit | standalone JSON 或 Luban 单表 Excel 的目录、搜索、语言和缺失项 | 配置项目内 Luban 工作目录、打开 Excel 作者目录、显式创建 XML/Excel 模板；预览只写项目 Temp |
| Tools | SaveKit | 存档路径/扩展名、文件元信息与 Runtime 后端/自动保存/容器头摘要 | 保存配置；不读取或发布真实 payload |

Architecture 没有独立 Workbench 页面；Runtime API、Interaction 和 CLI 只读诊断仍保留。未完成 Kit 不显示通用 missing 占位页。

## 页面通用规则

- 先选择唯一或显式目标 engine；Godot 配置/连接通常使用 `godot-editor`，游戏 Runtime 观察使用 `godot-runtime`
- 周期刷新只读取 registry、heartbeat、telemetry 和 snapshot；不周期发送 command
- telemetry 无法接受时回落 snapshot；详情查询和 UserAction 必须是用户显式操作
- 页面必须保留 loading、empty、offline、stale、error 和 truncated 状态；不要把截断结果当完整目录
- Doctor 是框架总览的诊断详情，不代替外部 Unity 自动化验证
- 离线 Docs 只读取包内 `Documentation~/Api` 与 `Documentation~/Guides`

## 页面特例

| 页面 | 不可误报的事实 |
|---|---|
| FsmKit | 同名 FSM 以 `instanceId` 区分；图仅表示已观测转换 |
| EventKit | 静态关系扫描与 Runtime 时间线可独立存在；页面不发送事件 |
| LogKit | 项目配置保存与当前 Runtime 应用是两个结果；文件正文只按需读取 |
| PoolKit | 仍有借出对象只是候选，不是内存泄漏结论 |
| ResKit | 来源详情需显式查询；页面不提供远程资源释放或缓存清理 |
| ActionKit | 空活动根合法；堆栈默认关闭且只影响后续根动作 |
| AudioKit | 总线目录可能裁剪；播放历史只投影 `play_started`，页面没有 Runtime 操作入口 |
| SpatialKit | Octree 密度是 XZ 投影并沿 Y 聚合，不是三维节点图 |
| UIKit | 只在 Unity engine 下可用；Root 配置由 Prefab Variant 与 `UIKit.SetRootPrefab` 管理，不在 Workbench 设置 |
| TableKit | 未生成项目没有 Runtime TableKit 类型；页面是离线生成入口 |
| LocalizationKit | 页面消费 Application 强类型 catalog，不解析 wire JSON；发现已注册 XML 后 Luban 失败不得伪装为 standalone JSON |
| SaveKit | Runtime state 只显示已存在后端、自动保存和有界容器头；`${persistentDataPath}` 和 `${userDataDir}` 可能只能显示运行时解析状态 |

## 新页面门禁

1. Kit Runtime API 已迁入并通过测试
2. Interaction Provider、capability、snapshot/telemetry/command 与宿主身份规则已落地，或该页面明确不依赖 Interaction
3. `Tooling.Application` 有强类型 read model；Avalonia 不解析 wire JSON
4. 页面有真实用户领域信息、空状态、错误状态和测试
5. 同一变更更新本文件、Kit 索引、人类 API 文档和相关 Skill
