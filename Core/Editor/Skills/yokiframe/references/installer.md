# 安装、更新、回滚

查看将要发生的变化时运行 `installer plan`。用户确认安装、更新、接管或回滚后，用相同参数运行 `installer apply`。

| 方式 | 装到哪里 | 怎么选 |
|---|---|---|
| `unity-local` | `Packages/com.hinatayoki.yokiframe` | 项目内一份本地包，与 Git URL 二选一 |
| `unity-git` | `Packages/manifest.json` | `--git-url` 使用绝对的 `file:`、`https:` 或 `git:` 地址 |
| `godot-local` | `addons/yokiframe` | 备份现有 add-on 后整份替换 |

Unity 包由 Installer 维护。计划列出用户改过的受管文件时，先把覆盖内容和用户确认下来。接管旧目录时，计划给出冲突文件、行号和 Kit。

不要把源码包所在的开发项目选为目标。源目录与目标项目相同，或其中一个位于另一个内部时，Installer 会拒绝计划，避免覆盖开发源码。打开安装器时也不会再默认选中这个开发项目。

执行后阅读返回的 rollback、conflicts、logs 和 evidence。需要重做时，先使用这次事务的回滚结果，再重新 plan。

## Skill 安装与更新

Skill 的唯一包内来源是 `<packageRoot>/Core/Editor/Skills/yokiframe/`。它是使用说明，不是引擎插件、CLI 或编译器；更新 Skill 不会自动安装这三者。

- Workbench 框架页提供目标列表及安装、更新、卸载；当前没有 Skill 安装 CLI 命令，不要编造 `skill install` 子命令。
- 安装到选定项目内的目标根下的 `yokiframe/`：Codex 为 `.codex/skills`，Claude Code 为 `.claude/skills`，Cursor 为 `.cursor/skills`，Windsurf 为 `.windsurf/skills`，Copilot 为 `.github/skills`，Agents 为 `.agents/skills`。自定义目录也必须位于该项目内。
- 当前服务整目录替换旧版本。更新前查看差异，保留用户自定义内容；不因为用户只要求更新文档，就删除整个目标目录或更新全局 Skill。
- 仅在用户要求安装或更新时操作。允许按既定目标手工同步已核对的文档，排除 `.meta` 和占位文件；不要修改其他 Skill。
- 同步 `SKILL.md` 和有关 `references/*.md`（含 `references/livecode.md`）；检查文件内容一致、内部相对链接可打开。引用框架设计/API 时先定位包根，不能沿安装目录向上猜路径。

完整框架安装流程见包根 `Documentation~/Guides/AI-Install.md`。在源码开发项目中只更新 Skill 不需要、也不能对本项目运行覆盖源码的框架安装事务。

## Roslyn 编译器

做法在 [roslyn-kit.md](roslyn-kit.md)。更新 Skill 不更新 `yoki`，也不打开执行开关。

编译器入口是 `YokiFrame.RoslynKit.Compiler.dll`。先看 `script_status.installed`。

- Unity：包内 `Tools/RoslynKit/Adapters/Unity/Editor/Dependencies~/roslyn-4.8.0/`。本地包和 Git URL 会带上。`installed=true` 时不要再发布。
- Godot：安装投影不含 `Adapters/Unity`，所以 add-on 里没有这份编译器。只有 `installed=false` 时，才把 bundle 放到 `.yokiframe/automation/compiler/roslyn-4.8.0/`。

`installed` 只表示主 DLL 在。不要下载依赖，也不要为了跑脚本启动外部编译器。缺包时在包根执行：

```powershell
dotnet publish "YokiFrameWorkbench~/src/YokiFrame.RoslynKit.Compiler/YokiFrame.RoslynKit.Compiler.csproj" `
  -c Release -o "<projectRoot>/.yokiframe/automation/compiler/roslyn-4.8.0"
```

已有可用 CLI 和编译器包后，运行任务不需要外部 .NET SDK、Node 或 csc；宿主直接加载编译器。不要覆盖正在使用的编译器 DLL，也不要把它们放进 Assets 让 Unity 自动导入。

安装或更新文档**不授权执行任意 C#**。`operations.enabled` 与 `scripts.trustedCSharp` 均默认 fail-closed，需用户明确授权；设置位置和命令见 [roslyn-kit.md](roslyn-kit.md)。验收结束恢复原设置、播放状态；不保存用户脏场景。
