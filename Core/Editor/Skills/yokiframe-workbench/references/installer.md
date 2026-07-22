# Installer 参考

本文件面向 AI 的 Installer 事务路由。只在用户明确安装、更新、接管或回滚 YokiFrame 时进入 apply；一般状态查询只做 detect 或 plan。

## 安装模式

| 模式 | 目标 | 关键约束 |
|---|---|---|
| Unity local | `Packages/com.hinatayoki.yokiframe` | 与 Git URL 来源互斥 |
| Unity Git URL | `Packages/manifest.json` | 只接受显式绝对 `file:`、`https:` 或 `git:` URI |
| Godot local | `addons/yokiframe` | 先验证项目 Runtime 缓存，再完整替换受控 add-on |

## 事务流程

1. 生成稳定 install plan 和目标 manifest/hash 清单。
2. Unity 检测受管文件用户修改；Godot 扫描 legacy Kit 引用和来源前置条件。
3. 将候选内容写入 staging，验证路径、内容、版本和 hash；Unity embedded 更新在此阶段保留旧包目录可见。
4. 备份当前安装并原子替换正式目标；Unity 仅在依赖或程序集图发生变化时刷新 manifest，普通脚本更新不强制全量 UPM resolve。
5. 从正式目标重新读取并执行 post-verify，外部 manifest 验证失败时仍使用同一事务 backup 回滚。
6. 失败时恢复备份，保留 rollback 和 evidence 结论。

## 所有权

- Unity Installer 管理的包目录是不可编辑交付物；发现修改时停止，不静默覆盖。仅 `YokiFrameWorkbench~/.artifacts*` 是 `Ctrl+E` 的可再生构建缓存，不计为用户修改且不会投影到下一版受管包。
- Godot `addons/yokiframe` 由 Installer 全权拥有；更新时不做文件级 diff、merge 或用户修改冲突阻断，而是备份后完整替换。
- Installer 是 Godot `plugin.cfg`、薄 `YokiFrameGodotEditorPlugin.cs`、`YokiFrameGodotBootstrap.cs`、YokiFrame `.uid` 与 `project.godot` 安装项的唯一写入 owner。
- 更新旧安装时，plan 以完整 add-on 替换表达 `plugin.gd`/UID 清理；apply 将新 C# EditorPlugin、旧入口删除和失败恢复放在同一事务。
- Runtime/Editor Adapter 不修复安装状态。
- Godot 投影默认排除 `YokiFrameWorkbench~`、Tests、`.git`、Unity `.meta`、构建缓存和任何包内 Runtime 残留。

## 操作规则

- 查看或讨论安装状态时只读 detect/plan，不自动 apply。
- 用户明确要求安装、更新或接管后才能执行 apply。
- legacy takeover 必须先报告冲突文件、行号和 Kit，再由用户确认。
- `BuffKit` 与 `InputKit` 始终不可接管。
- 失败后先检查 rollbackSucceeded、conflicts、logs 和 evidence，不重复覆盖目标目录。
