---
name: yokiframe-workbench
description: Use in a Unity or Godot game project when guiding or diagnosing the YokiFrame Avalonia Workbench, selecting a completed Workbench page, planning or applying an installation transaction, or installing the three YokiFrame package Skills. Route Runtime API usage to yokiframe and CLI/evidence commands to yokiframe-cli.
---

# YokiFrame Workbench And Installer

## 职责与非目标

本 Skill 面向**用户游戏项目中的 AI 助手**：指导人工使用 Workbench、诊断页面边界、执行安装事务，而不是开发 Workbench 本身。

- 负责 Avalonia Workbench 页面、Installer 事务和包内 Skill 管理的任务路由
- 不把页面标题、旧 UI 或空程序集视为已完成 Workbench 能力
- 不用 Workbench 代替 Runtime API、协议文件编辑、Unity 自动化或 CLI evidence 查询
- 不替用户确认安装、更新、接管 legacy 内容或覆盖项目文件
- 不修改 Workbench 源码或受管包内文件；发现页面行为与本文不符时向用户报告

## 前置核实

1. 定位当前游戏项目根，再读取 `.yokiframe/runtime/com.hinatayoki.yokiframe/current.json` 和当前平台 profile。已安装包根：Unity 本地嵌入为 `Packages/com.hinatayoki.yokiframe`；Unity Git URL 以 manifest 解析目录为准（通常 `Library/PackageCache`）；Godot 为 `addons/yokiframe/package/YokiFrame`
2. 绑定项目时传入规范化项目根；未绑定项目才进入 Installer 模式
3. 先确认 engine：零个在线 engine 时不要读取模糊状态；多个在线 engine 时显式选择目标
4. 需要页面能力时读取 [pages.md](references/pages.md)，需要安装事务时读取 [installer.md](references/installer.md)
5. Runtime state、catalog、terminal response 或 command 证据转入 `yokiframe-cli`

## 执行步骤

1. 人工使用 Workbench 时先从框架总览确认项目、engine、heartbeat、Doctor 和运行日志
2. 只选择 `pages.md` 列出的真实页面；未完成 Kit 不推荐用占位页、旧文档或旧 Tauri 页面代替
3. 周期读取保持 telemetry -> snapshot；只有用户显式点击的操作才发送 command 或提交项目配置
4. Installer 必须先 plan，报告来源、目标、warning 和 rollback 条件，确认后才 apply；Godot apply 会完整替换 `addons/yokiframe`
5. 安装 YokiFrame 自有 Skill 时仅从包根 `Core/Editor/Skills` 复制三个正式身份，目标在项目根内且排除 Unity `.meta`。已安装目标显示“更新”，会删除旧 Skill 目录后整份替换
6. Unity 的 `Ctrl+E` 会优先激活同一项目已打开的 Workbench；已有可用 Runtime 时直接打开。Workbench 会后台检查源码指纹，发现新版后通过页头“有新版可编译”按钮（Tooltip：点击构建新的 Workbench Runtime）显式构建；窗口关闭必须取消检查和构建，旧进程占用的 Runtime 目录延迟清理。
7. TableKit 以 `luban.conf` 与 `Luban.dll` 作为主工具发现和生成前提；新版 Luban 的 `Luban.Agent`、`Luban.Mcp`、Skill 目录只做可选发现和路径校验。缺失这些伴随能力不得阻断旧版 Luban。Workbench 不复制提示词、不托管 MCP、不重复实现官方 Agent；配表需求由 `yokiframe` Skill 读取这些路径并导入官方 Skill。

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
| 源码编译、Runtime bootstrap、AI 安装 | `Documentation~/Guides/AI-Install.md` |
| Runtime API / Kit 能力 | `yokiframe` |
| CLI / catalog / terminal evidence | `yokiframe-cli` |
| 人类使用入口 | 包根 `README.md`；Workbench 只提供已安装项目的操作界面 |

## 维护触发条件（仅 YokiFrame 包开发者适用）

以下条目供框架开发者维护本 Skill 时使用；用户项目 AI 只读本 Skill，不执行维护。

- 增删 Workbench 导航页、页面功能、读写边界或 engine 选择规则
- 改变 Installer 来源、投影过滤、冲突处理、staging、提交或 rollback
- 改变包内 Skill 身份、安装位置或刷新规则
- Application read model、页面或对应测试完成度改变
