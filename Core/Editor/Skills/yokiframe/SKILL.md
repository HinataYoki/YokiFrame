---
name: yokiframe
description: Use for YokiFrame game code, live inspection, Unity/Godot editor control, trusted in-memory C# automation and LiveCode recovery, and Workbench or Installer tasks.
---

# YokiFrame

已安装 YokiFrame 的 Unity 或 Godot 项目用它做三件事：写跨宿主游戏逻辑、查看运行态、安装和更新框架。

## 能做什么

| 要做的事 | 去哪里 | 怎么做 |
|---|---|---|
| 写服务、事件、状态、资源、场景、对象池、日志、单例、动作、音频、存档、本地化、空间查询、配表或 Unity UI | 包根 `Documentation~/Api/00-GettingStarted/FrameworkOverview.md` 选择 Kit，再打开该 Kit 主页面 | 按主页面的公开类型写。常用写法见 [usage-patterns.md](references/usage-patterns.md) |
| 查看项目、Kit 运行态和诊断 | Workbench | Unity 按 `Ctrl+E` 打开当前项目的 Workbench。各页能看和能改的内容见 [workbench-pages.md](references/workbench-pages.md) |
| 脚本化读取状态，或执行已经声明的操作 | 项目 `.yokiframe/runtime/` 里的 `yoki` | 用 `current.json` 和 `tool-manifest.json` 定位可执行文件。命令见 [cli-commands.md](references/cli-commands.md) |
| 让宿主动起来：进/退播放、查场景层级、看状态与操作开关 | RoslynKit | 先确认执行开关，再用 `yoki command send --kit RoslynKit`。命令协议名是 RoslynKit。见 [roslyn-kit.md](references/roslyn-kit.md) |
| 外部 C# 调用已有 Model/System、等帧、断言和日志 | Roslyn 自动化 | 先查 `script_status` 与授权；用 `yoki script` 提交方法体。Unity 支持截图，Godot 暂不支持。见 [roslyn-kit.md](references/roslyn-kit.md) |
| 不读业务源码，发现活动 Model/System 和公开方法签名 | Engine 对象目录 | 用 `object_list` / `object_describe`，显式传当前 target；查询只读元数据，不执行 getter。见 [roslyn-kit.md](references/roslyn-kit.md) |
| 在 Play 里热改或新增行为、边跑边调参，再落盘成 MonoBehaviour | LiveCode | 用受信任 `yoki script` 里的 `engine.LiveCode`。成员源码写法、跨原型调用、字段可读性和 Export/Bind 边界见 [livecode.md](references/livecode.md) |
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

API 签名、生命周期和示例都在包根 `Documentation~/Api/`。Core 在 `Api/02-Core`，游戏功能在 `Api/03-Tool`。

Skill 可以安装到项目的 `.codex/skills/`、`.agents/skills/` 或自定义目录；不要从 Skill 的相对位置推算包根。运行数据以 CLI 返回为准，不从本地业务源码推断数值。旧入口与 Unity 落盘 eval 已退出；Roslyn 已验证 Unity editor/play 与 Godot 4.7 .NET/Tools editor/runtime，功能子集按参考页和在线能力区分，正式编译器分发仍需单独准备。
