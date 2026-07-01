---
name: yokiframe-editor
description: >-
  YokiFrame 编辑器和工作台使用指南。Use when Codex 需要帮助用户打开 YokiFrame 工作台、
  查看 Kit 调试页面、安装 YokiFrame Skill、读取日志、使用快捷命令、查看 EventKit/FsmKit/PoolKit/ResKit/
  SingletonKit/AudioKit/SaveKit/LocalizationKit/SceneKit/SpatialKit/UIKit 状态，
  使用 TableKit/Luban 配置表生成、GraphKit 图编辑和 runtime contract 预览，
  或通过工作台和命令桥完成项目诊断。
---

# YokiFrame 编辑器使用指南

## 快速入口

- Unity 菜单：`YokiFrame/Editor UI/Launch` 打开工作台。
- 快捷键：`Ctrl+E` 打开工作台。
- 关闭窗口：`YokiFrame/Editor UI/Close`。
- 重启窗口：`YokiFrame/Editor UI/Restart`。
- 打包窗口：`YokiFrame/Editor UI/Package Binary (Release)`。
- 启动时预热：默认关闭；需要极致首开速度时可手动开启，升级或移除包前请关闭工作台。
- 工作台系统页：查看宿主连接、命令桥、快捷命令、运行日志和“安装Skill”卡片。

## 推荐使用顺序

1. 先打开工作台，确认宿主连接为在线。
2. 在对应 Kit 页面查看当前状态；高频状态优先依赖 telemetry 或 snapshot。
3. 需要一次性详情、历史或显式动作时，再使用快捷命令或 `yokiframe-command-bridge` Skill。
4. 需要配置表或节点图工具时，进入 TableKit 或 GraphKit 页面；它们不是 runtime snapshot 页面。
5. 命令超时或页面无数据时，先看系统页的桥状态和运行日志。
6. 需要给 AI 安装使用说明时，打开系统页的“安装Skill”卡片，把包内 Skill 安装到目标助手目录。

## 系统页

系统页用于确认工作台本身是否可用：

- `Host Connection`：宿主、engineId、心跳和连接时间。
- `Kit Diagnostic`：Kit 命令桥健康概览。
- `Quick Commands`：发送 `System/ping`、`System/status`、`System/bridge_status` 和常用 Kit snapshot 命令。
- `安装Skill`：把 `Assets/YokiFrame/Core/Editor/Skills` 中的 Skill 安装到 `.codex/skills`、`.claude/skills`、`.cursor/skills`、`.windsurf/skills`、`.github/skills`、`.agents/skills` 或项目内自定义目录。
- `Runtime Log`：查看工作台和命令响应日志。

安装 Skill 前确认：

- 包内源显示为 `Assets/YokiFrame/Core/Editor/Skills`。
- 当前 Skill 状态为“已随包提供”。
- 目标卡片显示“未安装”时点“安装”；需要刷新状态时点“刷新”。
- 自定义目录必须在项目根目录内，例如 `.my-ai/skills`。

## Kit 页面

### EventKit

- 查看运行时事件注册、最近事件流和诊断信息。
- 使用“扫描代码”分析发送方、接收方和注销路径。
- 需要减少编辑器代码干扰时启用“排除 Editor”。
- 点击代码位置时由宿主默认代码编辑器打开。

### FsmKit

- 查看活动状态机、当前状态、状态列表、转换历史和状态事件。
- 当前状态优先读 `FsmKit/state` snapshot。
- 详情缺失时使用 `FsmKit/get_workbench_snapshot`。

### PoolKit

- 查看对象池统计、池列表、活动对象、回收对象、事件历史和疑似泄漏候选。
- 定位对象未回收时可临时开启 tracking 或 stack trace。
- `check_leak` 只表示“当前仍借出对象”，不直接等同于真实内存泄漏。

### ResKit

- 查看资源后端、缓存资源、引用计数、卸载历史和加载定位状态。
- 需要排查某个资源时使用 `diagnose_resource`，payload 包含 `path` 和可选 `typeName`。
- Load 位置采集默认关闭，只在定位问题时开启。

### SingletonKit

- 查看已登记单例、存活状态、创建来源和释放状态。
- 列表为空只说明当前没有实例登记，不代表项目中不存在单例类型。

### LogKit

- 查看日志设置、日志历史和日志文件入口。
- 修改日志设置、清空历史、打开日志文件夹、读取或解密日志文件都属于显式操作。

### AudioKit

- 查看总线、声音列表、活跃 voice、播放历史和音量状态。
- 停止声音、停止总线、设置音量和静音会改变运行状态，只在用户明确要求时执行。

### SaveKit

- 查看存档后端、槽位列表和自动保存状态。
- `delete_slot` 和 `disable_auto_save` 是维护命令，只在用户明确要求或隔离测试环境中执行。

### LocalizationKit

- 查看当前语言、默认语言、语言列表、格式化器和缓存状态。
- 切换语言使用 `set_language`，payload 可为 `{"language":"English"}` 或 `{"languageId":2}`。

### SceneKit

- 查看当前场景、已加载场景和切换状态。
- 卸载场景使用 `unload_scene`，只在用户明确要求时执行。

### SpatialKit

- 查看空间索引类型、实体数量、分区数量、平面和边界。
- 命令桥只读；实体插入、更新、删除和查询留在运行时代码中完成。

### UIKit

- 查看面板缓存、面板状态、层级和面板栈。
- 运行时命令桥只读，不通过 `.yokiframe` 打开、关闭、显示、隐藏、压栈或弹栈。
- Unity Editor 工具命令依赖当前 Selection，只在用户明确要求时执行。

## 工具页面

### TableKit

- TableKit 是 Luban 配置表生成工作台，不是 runtime Kit 状态页。
- 先看 `环境与路径`：Luban 工作目录、`Luban.dll`、代码输出目录、数据输出目录、运行时路径模式。
- 推荐顺序：配置路径 -> 验证 -> 查看数据预览和控制台 -> 生成。
- 生成产物属于用户项目，通常包括 `Assets/Scripts/TableKit/Luban/`、`TableKit.cs` 和表数据目录。
- 运行时找不到表时，优先检查生成产物、数据输出目录、`RuntimePathPattern` 和 ResKit Provider。

### GraphKit

- GraphKit 是节点图编辑和数据建模页面，不是 runtime Kit 状态页，也不是 CommandBridge handler。
- 可编辑 graph project、node types、ports、fields、blackboard、placemats、notes、edges、subgraph 和 portal。
- 可预览/复制 graph JSON、Luban Definition XML、Luban Data XML 和 GraphRuntime contract。
- 可导出 Luban XML 到 TableKit/Luban 工作目录；之后进入 TableKit 页面验证和生成。
- 运行时执行器、handler 语义和 graph 解释逻辑由用户项目接入；YokiFrame 工作台只提供编辑、导出和契约预览。

## 快捷命令

优先使用这些只读命令确认状态：

- `System/ping`：确认 command/result 通路。
- `System/status`：查看引擎基础状态。
- `System/bridge_status`：查看队列、deadletter、lastError 和 backpressure。
- `<Kit>/get_workbench_snapshot`：获取对应 Kit 的一次性诊断快照。

TableKit 和 GraphKit 不在快捷命令范围内；它们通过 Tauri 页面和 Tauri 后端工具执行，不通过 `.yokiframe` runtime command。

变更型命令必须谨慎：

- PoolKit：`set_tracking`、`clear_history`。
- LogKit：`set_settings`、`reset_settings`、`clear_history`。
- AudioKit：`stop_voice`、`stop_all`、`set_master_volume`、`mute_master`。
- SaveKit：`delete_slot`、`disable_auto_save`。
- LocalizationKit：`set_language`。
- SceneKit：`unload_scene`。
- UIKit：Editor 工具命令依赖当前 Selection。
- 不存在 `TableKit/*` 或 `GraphKit/*` runtime 诊断命令。

## 常见排查

### 工作台打不开

1. 使用 `YokiFrame/Editor UI/Restart`。
2. 检查 Unity Console 是否有 Tauri 启动错误。
3. 若二进制缺失，使用 `YokiFrame/Editor UI/Package Binary (Release)` 重新打包。
4. 若已经存在窗口进程，工作台会通过 `.yokiframe/panel/show-window.json` 聚焦旧窗口；新窗口启动时会先等前端恢复窗口尺寸与位置，再写入 `.yokiframe/panel/show-window-ready.json` 放行显示，避免左上角闪动。
5. Windows 发布包会从项目 `Temp/YokiFrame/TauriRuntime/bin` 的临时副本启动 exe，避免长期锁住包内 `TauriRuntime~` 二进制；若 UPM 升级或移除仍提示占用，先执行 `YokiFrame/Editor UI/Close`。
6. 默认发布包不输出冷启动阶段打点；若需要继续排查首开耗时，临时开启专项诊断后再采集 Unity Console 和工作台运行日志。

### 页面没有 Kit 数据

1. 确认 Unity Editor 或运行时宿主正在写 heartbeat。
2. 查看对应 snapshot 是否存在，例如 `.yokiframe/engines/<engineId>/snapshots/FsmKit/state.json`。
3. 发送 `<Kit>/get_workbench_snapshot`。
4. 再发送 `System/bridge_status` 检查 lastError、pending、processing 和 deadletter。

### TableKit 或 GraphKit 页面异常

1. TableKit 先检查环境与路径、Luban 可用状态、输出目录和控制台日志。
2. GraphKit 先检查页面 issues、graph project JSON、Luban XML 和 GraphRuntime contract。
3. GraphKit 导出到 TableKit 后，回到 TableKit 执行验证和生成。
4. 不要用 `TableKit/get_workbench_snapshot` 或 `GraphKit/get_workbench_snapshot` 排查；它们不是 runtime command handler。

### AI Skill 安装失败

1. 确认包内源目录存在：`Assets/YokiFrame/Core/Editor/Skills`。
2. 确认目标路径在项目根目录内。
3. 目标目录已存在时安装器会先替换旧目录；失败时查看运行日志。
4. 自定义目录不要使用绝对路径、盘符或 `..` 跳出项目根。

## 相关 Skill

- `yokiframe`：Kit 选择、业务代码使用、工作台使用速查和框架状态查询入口。
- `yokiframe-command-bridge`：`.yokiframe` 文件命令桥、命令目录、payload 示例和压力验证。
