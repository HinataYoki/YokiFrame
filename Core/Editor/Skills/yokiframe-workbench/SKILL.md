---
name: yokiframe-workbench
description: Use when Codex needs to guide or diagnose the YokiFrame Avalonia Workbench, select a completed Workbench page, plan or apply a Unity/Godot installation transaction, or manage the three YokiFrame package Skills. Route Runtime API work to yokiframe and CLI/evidence commands to yokiframe-cli.
---

# YokiFrame Workbench And Installer

## 职责与非目标

- 负责 Avalonia Workbench 页面、Installer 事务和包内 Skill 管理的任务路由
- 不把页面标题、旧 UI 或空程序集视为已完成 Workbench 能力
- 不用 Workbench 代替 Runtime API、协议文件编辑、Unity 自动化或 CLI evidence 查询
- 不替用户确认安装、更新、接管 legacy 内容或覆盖项目文件

## 前置核实

1. 定位当前项目实际使用的包根、`.yokiframe/runtime/com.hinatayoki.yokiframe/current.json` 和当前平台 profile
2. 绑定项目时传入规范化项目根；未绑定项目才进入 Installer 模式
3. 先确认 engine：零个在线 engine 时不要读取模糊状态；多个在线 engine 时显式选择目标
4. 需要页面能力时读取 [pages.md](references/pages.md)，需要安装事务时读取 [installer.md](references/installer.md)
5. Runtime state、catalog、terminal response 或 command 证据转入 `yokiframe-cli`

## 执行步骤

1. 人工使用 Workbench 时先从框架总览确认项目、engine、heartbeat、Doctor 和运行日志
2. 只选择 `pages.md` 列出的真实页面；未完成 Kit 不推荐用占位页、旧文档或旧 Tauri 页面代替
3. 周期读取保持 telemetry -> snapshot；只有用户显式点击的操作才发送 command 或提交项目配置
4. Installer 必须先 plan，报告来源、目标、warning 和 rollback 条件，确认后才 apply；Godot apply 会完整替换 `addons/yokiframe`
5. 安装 YokiFrame 自有 Skill 时仅从包根 `Core/Editor/Skills` 复制三个正式身份，目标在项目根内且排除 Unity `.meta`
6. Unity 的 `Ctrl+E` 会优先激活同一项目已打开的 Workbench；源码更新后需要新 Runtime 时，先关闭旧窗口，再显式按一次 `Ctrl+E`。

## 副作用边界

| 操作 | 必须满足 |
|---|---|
| Workbench Kit UserAction | 当前 engine/session/generation 有效，操作属于当前页面真实声明的 action |
| 项目配置保存 | 经过 Application Settings Store；不由 Avalonia 直接覆盖物理文件 |
| Installer apply | 已审阅同一输入的 plan，用户明确确认，冲突未被静默绕过 |
| Godot legacy take-over | 已报告冲突文件、行号和 Kit，并取得明确确认 |
| Skill 安装或刷新 | 只处理 `yokiframe`、`yokiframe-cli`、`yokiframe-workbench`；不恢复旧身份 |

## 引用路由

| 需要的信息 | 读取位置 |
|---|---|
| 当前 Workbench 页面和可见边界 | [pages.md](references/pages.md) |
| Installer mode、plan、apply、rollback | [installer.md](references/installer.md) |
| Runtime API / Kit 能力 | `yokiframe` |
| CLI / catalog / terminal evidence | `yokiframe-cli` |
| 人类工具入口 | `Documentation~/Guides/Tooling.md` |

## 维护触发条件

- 增删 Workbench 导航页、页面功能、读写边界或 engine 选择规则
- 改变 Installer 来源、投影过滤、冲突处理、staging、提交或 rollback
- 改变包内 Skill 身份、安装位置或刷新规则
- Application read model、页面或对应测试完成度改变
