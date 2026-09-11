---
name: yokiframe
description: Use in a Unity or Godot project with YokiFrame installed. Covers writing game code with the Runtime APIs (architecture, events, FSM, pooling, resources, actions, singleton, save, audio, localization, spatial, tables), reading runtime state through the yoki CLI, and driving the Avalonia Workbench or Installer.
---

# YokiFrame

面向**已安装 YokiFrame 的用户项目 AI**。修改框架源码、迁移 Kit 和维护包内文档属于框架开发者职责。

## 按任务选入口

| 任务 | 读这里 |
|---|---|
| 用 Runtime API 写业务代码、选 Kit、确认能力边界 | [kit-index.md](references/kit-index.md)，再读 `Documentation~/Api` 对应主页面 |
| 通用约束与跨 Kit 易错点 | [usage-patterns.md](references/usage-patterns.md) |
| 读运行态、Project Model、能力目录、受控命令、Godot 导出 | [cli-commands.md](references/cli-commands.md) |
| Workbench 页面边界、安装事务 | [workbench-pages.md](references/workbench-pages.md)、[installer.md](references/installer.md) |
| 配表、Luban schema、Excel 填表、生成失败 | [tablekit-luban.md](references/tablekit-luban.md) |

## 前置核实

1. 定位项目已安装的包根：Unity 本地嵌入为 `Packages/com.hinatayoki.yokiframe`；Unity Git URL 以 `Packages/manifest.json` 解析到的实际目录为准（通常在 `Library/PackageCache`），不要手改；Godot 为 `addons/yokiframe/package/YokiFrame`
2. 确认三层完成度（Runtime API / Kit Interaction / Workbench）再选入口，见 [kit-index.md](references/kit-index.md)；文档存在、旧入口或空程序集都不算已实现
3. 需要签名、生命周期和失败语义时读 `Documentation~/Api` 主页面；不要凭记忆编造 API
4. CLI 的真实可执行文件在项目 `.yokiframe/runtime/` 缓存里，从 `current.json` 和 `tool-manifest.json` 解析，不要假设开发机上有 `yoki.exe`

## 硬约束

- 不直接创建、修改或删除 `.yokiframe` 协议文件
- 不修改已安装包内的源码或文档：Unity 包目录与 Godot `addons/yokiframe` 是 Installer 的受管交付物；发现行为与文档不一致时报告差异并建议升级包版本
- 不把 CLI 用作 Unity 编译、Scene/Prefab/Asset、Play Mode、截图或输入自动化接口；这些属于当前环境中的外部工具
- 业务代码只依赖 Core 或当前 Tool 的公开 API；宿主类型、生命周期和第三方实现留给既有 Adapter、Provider、Backend 或 Integration
- 不为宿主新建平行对象池、日志、资源加载、事件或状态机基础设施；不恢复全局 `YokiFrameKit.Initialize`
- 每个事件订阅、资源 handle、状态机、动作 controller 和异步工作都要有明确 owner 和注销、释放或取消路径
- 显式注入的 Provider/Backend 始终优先；读取和诊断调用不得隐式创建业务后端
- Interaction 只在 Editor/Tools 编译；未完成 Provider 的 Kit 不伪造在线状态
- UIKit 仅 Unity；不创建 Godot Adapter、`IUIBackend` 或 `UIKit.SetBackend`
- 有副作用的 CLI 写入命令先加 `--dry-run`；`state` 表示数据通道可用性，不是 Kit 业务健康
