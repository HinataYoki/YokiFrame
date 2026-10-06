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

## Roslyn 自动化的安装边界

Unity editor/play 与 Godot 4.7 .NET/Tools editor/runtime 的内存 C# 已实现并真机验证；正式安装器分发仍未完成。Godot 的 Capture/Patch/Export/Bind 未实现，不把宿主接线当成功能完全相同。按四层分别检查：

| 层 | 检查方式 | 缺失时 |
|---|---|---|
| Skill | `SKILL.md`、`references/engine-kit.md`、`references/cli-commands.md` 为同版 | 更新文档，不修改运行权限 |
| CLI | 新构建支持 `yoki script`；已有 CLI 可发送 `Engine/script_run` | 报告 CLI 版本不匹配；不要假称复制 Skill 会升级可执行文件 |
| Unity 宿主 | 在线 `engine_capabilities` 有 `script_run` / `script_status` / `run_*` / `object_list` / `object_describe`，没有 `entry_*` 或 Unity eval | 先更新桥接并完成一次正常 Unity 编译，不回退到落盘 eval |
| Godot 宿主 | .NET/Tools 正式 Editor 插件及 Runtime Bootstrap 分别发布上述操作，target=editor/runtime | 先正常构建并重启宿主；不把 GDScript eval 当成 C# 回退 |
| 编译器包 | `script_status` 的 `installed=true`；实际脚本还须通过编译/加载验证 | 返回 `ScriptCompilerUnavailable`，不临时下载、不启动外部编译器 |

编译器固定在项目 `.yokiframe/automation/compiler/roslyn-4.8.0/`，包含 `YokiFrame.EngineKit.Roslyn.dll`、完整依赖及许可证。`installed` 只检查主 DLL 存在，不代表所有依赖兼容已验证。**当前 Runtime bootstrap / Installer 尚未自动分发此目录**，不能把“框架安装成功”说成“Roslyn 已就绪”。

旧属性/入口 API 已删除，是破坏性变更。升级前检查业务脚本中的 `YokiFrameEntry` / `EntryContext`、旧 CLI 调用方及 `Assets/YokiFrame.Eval/Editor/` 残留并迁移；Installer 尚无自动阻断此类引用的预检，不自动删除用户脚本。历史运行文件保持原位，可用 `run_result/run_lookup` 只读查询。

开发者从源码准备编译器包时可在包根执行以下命令；这是安装/构建步骤，不是每次自动化任务的依赖：

```powershell
dotnet publish "YokiFrameWorkbench~/src/YokiFrame.EngineKit.Roslyn/YokiFrame.EngineKit.Roslyn.csproj" `
  -c Release -o "<projectRoot>/.yokiframe/automation/compiler/roslyn-4.8.0"
```

已有可用 CLI 和编译器包后，运行任务不需要外部 .NET SDK、Node 或 csc；宿主直接加载编译器。不要覆盖正在使用的编译器 DLL，也不要把它们放进 Assets 让 Unity 自动导入。

安装或更新文档**不授权执行任意 C#**。`operations.enabled` 与 `scripts.trustedCSharp` 均默认 fail-closed，需用户明确授权；设置位置和命令见 [engine-kit.md](engine-kit.md)。验收结束恢复原设置、播放状态；不保存用户脏场景。
