# Roslyn 内存 C# 自动化契约

> 对应 [Engine Kit v5.2](Engine-Operation-Glue-Design.md)。用户选择 Roslyn，撤销自研 YokiScript。独立编译组件与 Unity/Godot 宿主接线分别验收；本契约不是已发布能力清单。

## 1. 输入与执行位置

- 外部提交 C# 方法体，支持正常的变量、泛型、异步、循环和异常处理；不兼容 TS 的 `const` / `export default`。
- 默认 stdin，无需先创建脚本文件。可选 `.csx` 文件位于项目根 `scripts/engine/`，不进入 Assets。
- CLI 保持 Native AOT，负责输入上限、提交、查询、取消和展示。Roslyn、程序集加载和执行都属于引擎宿主。
- 宿主使用固定模板在内存中包装 `public static async Task Run(...)`，提供 `engine`、`test` 和 `cancellationToken`；不要求用户写入口属性、类或注册入口名。
- 模板不使用 `[YokiFrameEntry]`，不将编译产物交给入口扫描器。游戏 Model/System 不依赖自动化上下文。
- 使用 `CSharpCompilation`，不是 `CSharpScript` 的交互会话；不同运行不共享脚本变量。初版语言版本固定 C# 9，不支持 `#r` / `#load` 或自动下载依赖。

## 2. 编译核心

```text
宿主捕获源码与引用快照
  -> ParseText（完整输入，虚拟源码路径）
  -> CSharpCompilation（显式 MetadataReference）
  -> Emit（MemoryStream PE + portable PDB）
  -> 诊断或内存产物
```

- 编译模块是独立 `netstandard2.0` 程序集，不依赖 Unity、Godot、Entry 或 CLI。只编译，不扫描 AppDomain、不加载、不执行用户代码。
- 引用由宿主明确提供：当前可用的基础库、引擎程序集、框架和选定业务程序集。不把宿主磁盘上所有 DLL 无差别加入引用。
- 首个组件使用固定 Roslyn NuGet 版本；Unity 分发前必须验证其依赖闭包、Mono 加载、已有同名 DLL 冲突与许可证，不能从 Unity 安装目录私自借用内部编译器。
- 元数据引用读取允许访问已有 DLL；“内存编译”指任务源码和编译产物无中间文件，不等于连引用元数据也不能读磁盘。
- 源码最大 128 KiB UTF-8；PE/PDB 各最多 8 MiB；返回最多 100 条诊断并标明是否截断。编译取消通过 CancellationToken；这些限额不是进程级 CPU/内存沙箱。
- 禁止 unsafe 编译，不运行项目 analyzer/source generator，不安装源码解析器或元数据解析器，不启动 csc/dotnet 子进程。
- 编译失败不返回可加载产物。警告单独保留，不能把 Ready/编译成功当成任务通过。
- 每条诊断提供 code、severity、message、虚拟路径、1-based 行列。包装器用 `#line` 将用户方法体映射回输入位置，不生成源码映射文件。
- 唯一程序集名防止同名加载冲突。引用快照变化时不能复用旧编译结果；初版不引入持久 DLL 缓存。

## 3. 宿主加载与生命周期

- 先完成请求准入与持久化索引，返回 accepted/runId，再调度编译。编译失败、取消或会话改变后不加载用户程序集。
- 加载/执行前重新检查会话、代次、目标、执行开关和受信任 C# 授权；不得在错误 target 上运行。
- 使用内存字节加载，不写 DLL/PDB，不调用 AssetDatabase.Refresh、RequestScriptCompilation 或引擎重载 API。
- 单次 `script_run` 编译产物本身就是新的程序集，不自动向已有业务 DLL 注入或替换方法。显式 `engine.LiveCode.Patch` 可使用可选 HarmonyX 后端拦截现有方法，见 [LiveCode 契约](Engine-LiveCode-Contract.md)。加载后引用同一宿主里已加载的框架/业务类型，通过活动服务取得当前实例；不能加载第二份业务程序集造成类型身份分裂。
- Unity Editor/Play 是首个集成目标。Unity Player/IL2CPP 不支持；Godot .NET Editor/Runtime 需独立验证，不将 Native AOT 导出算作支持。
- Unity 默认域加载不能承诺单独卸载程序集。上限与剩余量以 script_status 的 maxAssemblies/maxLoadedBytes、remainingAssemblies/remainingBytes 为准；当前开发源码保留用户设置的 4096 次/64 MiB，不再沿用历史 64 次声明。计数不能在 prune 时归零；达到预算明确拒绝，不自动重载。关闭 Domain Reload 时仅退出重进 Play 不一定清零。
- 删除运行记录、释放委托不等于卸载程序集。Godot 的 collectible AssemblyLoadContext 即使可用，也必须正确共享引擎/游戏类型身份，另行验证回收条件。
- 用户事件订阅、静态字段、原生资源或后台 Task 可能超出运行生命周期；代码作者需成对清理，不承诺框架能撤销任意 C# 副作用。

## 4. 安全与异步

- `script_run` 始终 Dangerous，要求项目允许受信任 C#、Engine 开关有效和请求显式确认。只读源码不自动获得 ReadOnly 或 FastChannel 资格。
- Roslyn 不是沙箱。引用限制、禁 unsafe 和助手 API 的范围校验都不能阻止任意托管代码的反射、IO、网络等行为。
- 第一次调用在宿主主线程开始；正常 await 按宿主同步上下文恢复。助手对自身的线程访问作保障，不把任意 `Task.Run` 或 `ConfigureAwait(false)` 改回主线程。
- Task 返回不等于完成，不同步 `.Wait()` / `.Result` 阻塞 tick。取消/超时是协作请求，不用 Thread.Abort，不声称能抢占同步死循环。
- 编译错误 -> CompileFailed；记录断言失败 -> Failed；非断言异常 -> Errored；无完成证据的域重载 -> Unknown；取消宽限期后仍未结束 -> Detached。
- 结果查询纯读；源码丢失、编辑器重载、CLI 断开后不自动重放任务。requestId + payload hash 防止不确定重试造成重复写入。
- FileBridge 请求归档可能保存源码，运行记录和截图允许落盘。任务源码无中间文件，不代表零磁盘 IO 或源码绝不持久化。

## 5. 当前自动化上下文

以下 API 已接入 Unity editor/play；Godot 4.7 .NET/Tools editor/runtime 已验证内存执行、等帧、日志、行为与恢复子集。Capture/Patch/Export/Bind 仅 Unity；编译核心不包含任何引擎操作：

| 实际 API | 契约 |
|---|---|
| `engine.RequireService<T>(...)` | 唯一活动服务，不调用懒初始化 getter；多个 Architecture 时要求显式身份 |
| 普通 C# 公开业务方法 | 可直接调用，不要求属性或静态包装；返回值遵循原 C# 引用/值语义 |
| `engine.WaitFrames(count, clock)` | Unity gameFrame 从调用时已开始帧数起等待后续 Update/LateUpdate 完成；count=0 立即完成、暂停不推进；不保证恰好经过 count 次更新，不是渲染/物理屏障。editorTick 仅调度，不自动开始播放 |
| `test.Equal(actual, expected, message)` | 记录 actual/expected，失败抛异常并保留失败；即使被捕获也不能报 Passed |
| `engine.ConsoleLog(string)` | 输出到引擎日志与运行记录；最多 256 条，每条最多 16384 字符，不自动序列化对象 |
| `engine.Capture(mode, path, autoNumber=false)` | 返回 Task<string>，等待 PNG 完成后给出实际相对路径；autoNumber=true 选择新的编号文件，默认拒绝覆盖。仅运行且未暂停的 Unity Game view，限 `.yokiframe/automation/evidence/`、单边最大 4096、最大 32 MiB；不改相机或保存场景 |
| `cancellationToken` | 绑定当前运行，用于业务 await 和显式检查 |
| `engine.LiveCode` | Unity 方法 patch、Play 临时行为、字段调参、显式导出与 Edit Mode 绑定；Godot 支持 Node 行为、批量、调参、快照与显式映射恢复，见 LiveCode 契约 §7 |

Godot runtime gameFrame 计已观察的非暂停处理帧，帧号跳跃不计；editor 用 editorTick。
Godot 授权为 project.godot 的 `yokiframe/engine/trusted_csharp`。恢复已验证显式进程重启，
不保证内建 Build 热重载期间自动恢复，isCompiling=false 不是可靠构建守卫。
直接业务返回值不是自动快照。前后比较时保存标量或显式复制 DTO；需要框架快照时仍使用有界 inspect。完整工作流见父设计 §12。

## 6. 分阶段验证

1. 编译核心：真正运行 Roslyn，不使用假编译器。内存编译有效 C#，执行产物调用已加载测试服务；编译错误零用户执行，校验行列、缺失引用、异步方法、取消、限额、无任务文件生成。
2. 依赖与发布：验证 netstandard 组件及编译器依赖的 Unity 2022.3 Mono 兼容，打包并隔离工具依赖；脚本编译器不能成为 CLI 的执行后端。既有 Tooling.Application 因其他工具功能已引用 CodeAnalysis 5.0.0，不能再声称整个 CLI 项目图没有 Roslyn；本功能的宿主组件固定 4.8.0，不复用 CLI 的编译器版本。
3. 宿主接线：复用并解耦既有调度器，而不是套旧入口扫描。验证准入、状态推进、主线程、结果纯读、容量预算、取消和重载不重放。
4. 真实纵切：初始化一次业务示例后连续提交不同 C# 文本，改变已有 Model 的数据、等待游戏帧、断言、日志、截图；记录 Roslyn 编译与 Unity 资产编译的区别、会话和数据连续性。
5. 双引擎和迁移：Godot 独立验收后再声明支持；旧入口与落盘 eval 按依赖清单退出，不先破坏用户旧脚本。

**状态不得跨阶段借用：编译模块通过 .NET 测试，不等于 `yoki script` 已发布或 Unity 不重载验收已通过。**

## 7. 实现与安装状态

- 已实现独立 `YokiFrame.EngineKit.Roslyn` 编译组件、Unity 接线、`script_run/script_status/run_result/run_lookup/run_cancel`、新 CLI 的 `script` 命令与上述上下文。
- 复用运行存储/调度器，脚本直接提交瞬态 work；内部结果改为 YokiFrameRunResult/Status/Assertion。旧属性、扫描器、entry_* 和 Unity 落盘 eval 已移除，无兼容执行别名。历史存储目录不迁动，run_result/run_lookup 可读旧记录但不重放。
- `object_list/object_describe` 已提供 `root=service` 的只读元数据目录，显式 target、会话绑定 ID、有界分页；不执行 getter/方法。完整 payload 与边界见父设计 §5.1；当前强类型 C# 不要求 memberId 调用协议。
- 运行记录保存 `kind=script`、payloadHash、ownerHostId、sessionId/generation 和编译诊断；同宿主换代后无完成证据的任务为 Unknown，其他宿主不能认领、对账或取消。查询纯读，源码不作为可重放任务保存。
- 编译器包位于 `.yokiframe/automation/compiler/roslyn-4.8.0/`，宿主只加载项目本地的固定依赖，不下载、不调用 dotnet。`script_status.installed` 只表示主 DLL 存在；依赖不全/不兼容仍可能在运行时失败。
- 本项目已通过开发构建准备该目录；**正式 Runtime bootstrap / Installer 尚未分发编译器包**，CLI 的新命令也需要新构建。开发准备命令见 Skill `references/installer.md`，不能宣称所有已安装版本自动可用。
- Unity `settings` 数组中需同时启用 `Engine/operations.enabled` 和 `Engine/scripts.trustedCSharp`；每次脚本提交仍需显式确认。文档或依赖安装不自动开启授权。
- 验证与实际 runId 见父设计 §14 和 LiveCode 契约 §9，区分先前纵切与后续恢复回归。Godot Capture/构建守卫、Native AOT 发布回归、同名依赖冲突矩阵和 Installer 升级预检尚未验收。
