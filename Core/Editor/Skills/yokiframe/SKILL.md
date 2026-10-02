---
name: yokiframe
description: Use in a Unity or Godot project with YokiFrame installed. Covers writing game code with the Runtime APIs (architecture, events, FSM, pooling, resources, actions, singleton, save, audio, localization, spatial, tables), reading runtime state through the yoki CLI, and driving the Avalonia Workbench or Installer.
---

# YokiFrame

已安装 YokiFrame 的 Unity 或 Godot 项目用它做三件事：写跨宿主游戏逻辑、查看运行态、安装和更新框架。

## 能做什么

| 要做的事 | 去哪里 | 怎么做 |
|---|---|---|
| 写服务、事件、状态、资源、场景、对象池、日志、单例、动作、音频、存档、本地化、空间查询、配表或 Unity UI | 包根 `Documentation~/Api/00-GettingStarted/FrameworkOverview.md` 选择 Kit，再打开该 Kit 主页面 | 按主页面的公开类型写。常用写法见 [usage-patterns.md](references/usage-patterns.md) |
| 查看项目、Kit 运行态和诊断 | Workbench | Unity 按 `Ctrl+E` 打开当前项目的 Workbench。各页能看和能改的内容见 [workbench-pages.md](references/workbench-pages.md) |
| 脚本化读取状态，或执行已经声明的操作 | 项目 `.yokiframe/runtime/` 里的 `yoki` | 用 `current.json` 和 `tool-manifest.json` 定位可执行文件。命令见 [cli-commands.md](references/cli-commands.md) |
| 安装、更新、回滚 | Installer | 先出计划，用户确认后执行。见 [installer.md](references/installer.md) |
| 加表、改表、填 Excel、排查 Luban 生成 | Workbench 的 TableKit 页面，以及官方 Luban Skill | 读取项目里的 TableKit 配置，再按任务打开官方 Skill。见 [tablekit-luban.md](references/tablekit-luban.md) |

## 包和文档在哪里

| 安装方式 | 包根 |
|---|---|
| Unity 本地包 | `Packages/com.hinatayoki.yokiframe` |
| Unity Git URL | `Packages/manifest.json` 解析出的目录 |
| Godot | `addons/yokiframe/package/YokiFrame` |

API 签名、生命周期和示例都在包根 `Documentation~/Api/`。Core 在 `Api/02-Core`，游戏功能在 `Api/03-Tool`。
