# LiveCode 长期迭代交付清单

用户目标：继续直到做完。此前 Snapshot/Restore v1 已完成，本清单的六项增量已交付。
完成范围是长期迭代通道，不代表全部 EngineKit、双引擎功能对等或正式安装分发完成。

| 项目 | 状态 | 完成证据要求 |
|---|---|---|
| AttachMany | Unity / Godot 已实现并验收 | `2b10c4622e154492b38230a8f1c255be`：两行为批量和 Awake 跨行为调用；Godot 两进程验收；单测覆盖替换回滚/预检 |
| 主动预算预警 | 已实现并单测 | status/handle.Budget/result.Budget，剩余 3 个/1 MiB 预警，不自动重载 |
| 独立零编译调参 | Unity / Godot 已实现并验收 | Unity 加载数 3、字节 51612 前后不变；Godot `cli-1791290238615-f4d03bef` 加载数 2、字节 18748 前后不变 |
| TuningBinder 文件热载 | Unity / Godot 已实现并验收 | Unity 值 75→42、重挂 stopped；Godot 值 62→42、applyCount=2，未增加加载 |
| Godot 恢复适配 | 已实现并真机验收 | 正式 Editor/Runtime 接线；显式映射；快照 9/9、独立新进程恢复 14/14；不含内建 Build 热重载自动恢复 |
| 文档与 Skill 同步 | 已完成 | 设计/契约/安装文档与 Skill 更新；5 文件 SHA-256 一致，源与安装副本校验通过 |

禁止自动域重载或自动重放未知结果；不改用户预算/权限/Play 设置，不保存用户场景。
Godot 不以 mock 测试冒充真机验证；若缺环境先核实并记录，保留未完成状态。

2026-10-06 最终回归：EngineKit 240/240、Roslyn 7/7、Godot Editor 31/31、Runtime 96/96；
Unity 编译门与 Godot 验收工程构建均为 0 警告/0 错误。测试项目保留已有 nullable 警告。
`da53afbfb5af4cae9620e1427a4ff692` 5 条断言验证错误批次不写、float/color、换版迁移；
`9488c55419f4412e940eadf7aba83ac0` 验证清理与场景未改，之后退出 Play。
Godot 采用匹配 SDK 的官方 4.7 .NET 便携版，位于项目 `.yokiframe/tools/godot-4.7/`，
没有覆盖本机旧 4.5.1。隔离工程与可复用 C# 验收脚本在 `scripts/engine/godot-live-tests/`。

最终真机证据：

- Unity `b0b8d90f702547f7884651927c79991a`：6/6，批量、真实等帧、字段、快照、预算、场景脏状态不变。
- Godot Editor `02fb11707fb644c8a11b03c35242140e`：8/8，生命周期、非有限字段拒绝、快照/恢复。
- Godot Runtime `8b126e1db88b459ebf4bf309ed604ec0`：5/5，CLI 批量、等帧、暂停屏障、快照。
- `0860c3bf96e94c45a85d30872bd7bb53`：2/2，包括重建自动化对象不清零预算。
- `9e688d399c8c422ba13f417cdb694001`：4/4，文件实际值 42、Godot.Color、Capture 明确不支持、清理。
- 最新跨进程快照 `4334c2e0a88f4373a9eb088c52134d93`：snapshot 9/9、restore 14/14。

最终已退出 Unity Play、清理测试 handle/文件绑定并关闭测试 Godot 进程，未保存用户场景、
更改相机、预算常量、执行授权或 Play 设置。

本轮范围外：正式 Installer 编译器分发与 Native AOT 发布回归；Godot Capture/Patch/
Export/Bind；Godot 构建中状态接线、内建 Build 热重载自动恢复与 GC 卸载证明。
Godot 恢复是显式进程重启加 Restore，isCompiling=false 不作构建守卫，不能并行构建
游戏 DLL 与提交脚本。框架不会自动重载，也不会自动重放结果未知的写操作。

## 后续增量：版本化导出（2026-10-06）

已实现 ExportMany 暂存、统一 CommitExport、EditMode Reexport 和逐目标 Bind；
同类多个实例共享源文件，更新保留 .meta、组件与当前字段，旧记录不被覆盖。
live_export_commit/status 已接入 Unity 命令目录，Godot 不发布这两项。

本增量回归：EngineKit 263/263，Roslyn 7/7，Godot Editor 31/31、Runtime 96/96，
Unity CompileGate 0 警告/0 错误。真实 Unity 完成批量、共享目标、原位更新和拒绝路径验证；
仅保存本次新建的隔离测试场景。具体 runId、兼容流程、ID 复用与字段迁移边界见
[版本化导出契约](Engine-LiveCode-Export-Contract.md)，不改写上文历史测试计数。
