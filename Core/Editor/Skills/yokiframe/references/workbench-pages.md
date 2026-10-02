# Workbench 怎么用

Unity 项目按 `Ctrl+E` 打开当前项目的 Workbench。已经打开的窗口会切到前台。页头出现「有新版可编译」时，用这个按钮构建新的 Runtime。

Godot 的编辑器连接选择 `godot-editor`，游戏运行态选择 `godot-runtime`。页面通过 telemetry 和 snapshot 刷新。列表带有 truncated 标记时，用更具体的查询条件继续查看。

| 要看或要改的内容 | 打开 | 怎么做 |
|---|---|---|
| 项目、engine、心跳、诊断、命令桥、Skill | 框架 | 命令桥用来核对一条真实命令。Skill 在这里安装、整目录更新或卸载；来源是包根 `Core/Editor/Skills/yokiframe`，目标放在项目根内，并排除 `.meta` |
| API 和指南 | 文档 | 搜索包内 `Documentation~/Api` 与 `Documentation~/Guides` |
| 事件关系和监听 | EventKit | 分别查看静态扫描和 Runtime 时间线 |
| 状态机实例 | FsmKit | 用 `instanceId` 区分同名实例。图上显示的是已经发生的转换 |
| 日志配置和当前会话 | LogKit | 保存项目配置，或把已声明设置发到当前会话。文件正文在需要时读取 |
| 对象池压力和借出 | PoolKit | 查看池、对象和借出候选。需要跟踪、堆栈、历史或泄漏检查时执行对应操作 |
| 资源 Provider 和 handle | ResKit | 查看 Provider、资源和卸载历史。跟踪和历史使用页面上的已声明操作 |
| 正在运行的动作 | ActionKit | 查看活动根、动作树和终态。堆栈开关作用于之后启动的根动作 |
| 正在播放的音频和音频 ID | AudioKit | 按 Bus 查看 voice、进度和 `play_started` 历史。生成稳定 ID 时使用索引生成，写入项目代码和 manifest |
| 空间索引 | SpatialKit | 查看索引、分区和热点。Octree 密度按 XZ 投影显示 |
| Unity 面板和绑定 | UIKit | 只在 Unity engine 下显示。查看诊断、Prefab、Bind 和代码生成。定制 Root 时，在游戏代码里对 Prefab Variant 调用 `UIKit.SetRootPrefab`。Godot 连接时导航不显示这一页 |
| Luban 表 | TableKit | 校验配置、预览并生成项目代码。Agent、MCP 和 Skill 路径也在这里选择；旧版 Luban 只完成校验和生成 |
| 本地化文本 | LocalizationKit | 搜索 standalone JSON 或 Luban 单表，打开 Excel，创建模板。预览写入项目 Temp。已注册 XML 时，以 Luban 结果为准 |
| 存档位置和槽位摘要 | SaveKit | 选择持久化目录、项目目录或自定义路径并保存配置，同时查看已有后端和容器头。宿主目录在 Editor 连上后解析 |

Architecture、SingletonKit、ToolClass、CodeGenKit、InspectorKit 和 SceneKit 的用法在对应 API 主页面。
