---
name: yokiframe-usage
description: YokiFrame 使用文档。Use when Codex 需要说明或操作 YokiFrame Kit API、Tauri 编辑器、FsmKit/EventKit/PoolKit/SingletonKit、UIKit、ActionKit、命令桥、snapshot、telemetry、事件流、代码扫描、Unity/Godot 接入或 AI 运行时查询。
---

# YokiFrame 使用指南

## 选择入口

- 业务代码：使用 `YokiFrame` 命名空间下的各 Kit API。
- 人类调试：在 Unity 菜单使用 `YokiFrame/Editor UI/Launch`，快捷键为 `Ctrl+E`。
- AI/脚本诊断：通过 `.yokiframe/engines/<engineId>` 文件桥读取 snapshot 或发送命令。
- Tauri 页面：FsmKit 当前状态优先 shared memory telemetry，EventKit 关系图结合 runtime snapshot 与代码扫描。
- Godot 项目：使用 Godot adapter 和 `addons/yokiframe` 薄插件入口，命令桥路径同样走 `.yokiframe/engines/<engineId>`。

## 查询顺序

看当前状态时按这个顺序：

1. Tauri 可视高频页面：`read_telemetry`。
2. AI 或脚本：优先读 `snapshots/<kit>/<name>.json`。
3. 需要详情、历史、显式操作或 snapshot 缺失：发送 command/result 请求。
4. 请求超时：先发 `System/bridge_status`，再看 pending、processing、deadletter、lastError。

不要用高频 `send_command` 轮询实时页面；命令桥是可靠控制面，不是运行时事件总线。

## 常用任务

- 查看桥状态：读 `references/command-bridge.md` 的 `System/bridge_status`。
- 查看 FSM：优先 `FsmKit/state` snapshot，再用 `FsmKit/get_workbench_snapshot`。
- 查看 EventKit：优先 `EventKit/state` snapshot，再用 `EventKit/get_workbench_snapshot`；代码关系由扫描器提供。
- 查看 SpatialKit：优先 `SpatialKit/state` snapshot，再用 `SpatialKit/get_workbench_snapshot`；实体插入、更新、删除和查询留在运行时代码的 `ISpatialIndex<T>` 对象上执行。
- 查看 InputKit：优先 `InputKit/state` snapshot，再用 `InputKit/get_workbench_snapshot`；输入模拟、按键注入和重绑定不通过命令桥执行。
- 查看 UIKit：优先 `UIKit/state` snapshot，再用 `UIKit/get_workbench_snapshot`；面板开关、显示/隐藏和压栈不通过命令桥执行。
- 扫描 EventKit 代码：在 Tauri EventKit 页面点击“扫描代码”，需要时勾选“排除 Editor”。
- 打开源码：通过 Tauri 页面或 `System/open_code_location`，由引擎默认代码编辑器处理。
- 使用 Kit API：读 `references/kits.md`。

## 使用约束

- 新业务事件优先 `EventKit.Type` 或 `EventKit.Enum`，`EventKit.String` 只做兼容。
- EventKit 注册和注销成对出现；注销路径缺失时扫描器会标记泄漏风险。
- FsmKit 状态机由业务每帧驱动；编辑器监控缓存只在 Adapter 层维护，不增加运行时热路径文件 I/O。
- PoolKit/ActionKit 热路径避免额外分配和闭包；需要频繁对象时优先复用框架池。
- AI 需要框架状态时优先使用 `yokiframe-command-bridge` skill 和 `.yokiframe` 协议，不把 Unity MCP 当作唯一状态来源。

## 参考资料

- `references/command-bridge.md`：通用 AI 文件桥请求、响应和示例。
- `references/kits.md`：FsmKit、EventKit、PoolKit、SingletonKit、SpatialKit、InputKit、ActionKit 常用 API。
- `yokiframe-command-bridge/SKILL.md`：YokiFrame 包内命令桥 Skill 源，用于安装到各类 AI 工具。
- `yokiframe-command-bridge/references/command-catalog.md`：当前 Kit 命令目录、payload 示例、压力测试和错误码。
- `yokiframe-editor/SKILL.md`：工作台、安装 Skill、Kit 页面和日志诊断使用说明。
