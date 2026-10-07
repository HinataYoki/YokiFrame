---
name: yokiframe
description: Use for YokiFrame game code, live inspection, Unity/Godot editor control, trusted in-memory C# automation and LiveCode recovery, and Workbench or Installer tasks.
---

# YokiFrame

已安装 YokiFrame 的 Unity 或 Godot 项目用它写跨宿主游戏逻辑、查看运行态、驱动宿主，以及安装和更新框架。Kit 有没有 Runtime、命令或 Workbench 页面，以包根 `Documentation~/Api/00-GettingStarted/FrameworkOverview.md` 为准。

实现功能时直接改项目里的正式代码并落盘。不要用 RoslynKit 或 LiveCode 代替实现。只有用户明确点名，或要求不退出 Play Mode 就改变代码行为时，才打开 RoslynKit。

## 能做什么

| 要做的事 | 去哪里 | 怎么做 |
|---|---|---|
| 写服务、事件、状态、资源、场景、对象池、日志、单例、动作、音频、存档、本地化、空间查询、配表或 Unity UI | 包根 `Documentation~/Api/00-GettingStarted/FrameworkOverview.md` 选择 Kit，再打开该 Kit 主页面 | 按主页面的公开类型写。常用写法见 [usage-patterns.md](references/usage-patterns.md) |
| 查看项目、Kit 运行态和诊断 | Workbench | Unity 按 `Ctrl+E` 打开当前项目的 Workbench。各页能看和能改的内容见 [workbench-pages.md](references/workbench-pages.md) |
| 脚本化读取状态，或执行已经声明的操作 | 项目 `.yokiframe/runtime/` 里的 `yoki` | 用 `current.json` 和 `tool-manifest.json` 定位可执行文件。命令见 [cli-commands.md](references/cli-commands.md) |
| 用户明确要求操作宿主：进/退播放、查场景、调用已经在跑的服务、截图或断言 | RoslynKit | 先确认执行开关，再用 `yoki`。见 [roslyn-kit.md](references/roslyn-kit.md)。这不是实现新功能的入口 |
| 用户明确要求不退出 Play 就改行为、调参，或点名 LiveCode | LiveCode | 用受信任 `yoki script` 里的 `engine.LiveCode`。见 [livecode.md](references/livecode.md)。满意后的正式结果仍应落成普通代码 |
| 安装、更新、回滚 | Installer | 先出计划，用户确认后执行。见 [installer.md](references/installer.md) |
| 安装或更新本 Skill，以及核对自动化依赖 | Workbench 框架页 | Skill 文档、CLI、宿主和编译器包分别验收，不因文档更新就开启执行权限。见 [installer.md](references/installer.md) |
| 加表、改表、填 Excel、排查 Luban 生成 | Workbench 的 TableKit 页面，以及官方 Luban Skill | 读取项目里的 TableKit 配置，再按任务打开官方 Skill。见 [tablekit-luban.md](references/tablekit-luban.md) |

## 包和文档在哪里

| 安装方式 | 包根 |
|---|---|
| Unity 本地包 | `Packages/com.hinatayoki.yokiframe` |
| Unity Git URL | `Packages/manifest.json` 解析出的目录 |
| 源码开发项目 | `Assets/YokiFrame`，以实际 `package.json` 所在目录为准 |
| Godot | `addons/yokiframe/package/YokiFrame` |

API 签名和示例在包根 `Documentation~/Api/`。Core 在 `Api/02-Core`，游戏功能在 `Api/03-Tool`。RoslynKit 的用户说明是 `Documentation~/Api/03-Tool/RoslynKit.md`；具体命令仍按上面的 reference 执行。

Skill 可以装在项目的 `.codex/skills/`、`.agents/skills/` 或自定义目录。不要从 Skill 的位置推算包根。运行中的数值以 CLI 返回为准。
