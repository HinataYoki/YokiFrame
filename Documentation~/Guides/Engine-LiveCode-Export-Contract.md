# LiveCode 版本化导出与多目标绑定

2026-10-06：已实现并完成 Unity 2022.3.16f1 真机纵切。本契约补齐原型落盘后的迭代闭环，
不扩大 Godot 的支持范围。验证证据见本文末尾。

## API

- `Export(id, path)` 保留旧签名；单项暂存后安排提交，返回 exportId。批量不循环调用它。
- `await ExportMany(requests, batchId = null)`：请求为 `YokiFrameLiveExportRequest(id, path)`。
  相同路径、类名和源码合并成一份脚本，但各目标保留独立字段快照；返回 Prepared 批次。
- `await Reexport(previousExportId, members, batchId = null)`：无需 Play handle，可在 EditMode
  更新旧记录的同一路径/类名，返回 Prepared 批次，不删除脚本或组件。
- `CommitExport(batchId)` 或 Dangerous `live_export_commit`：显式提交暂存批次；
  payload 为 batchId/target/confirmed。全部重新核验后同步写入并统一请求导入，不跨 await 持锁。
- ReadOnly `live_export_status`：传 batchId 或 exportId，读取持久记录、文件匹配和编译版本，
  不修复、不重放。调用方可预先指定 32 位 Guid batchId，响应丢失后按它对账。
- `Bind(exportId, target = null)`：EditMode 对指定目标绑定；一个源码版本可服务多个对象。
  已捕获目标使用各自字段；额外目标使用组件默认值，不复制另一对象的引用。
  已有本工具绑定的组件保留当前字段，只更新版本记录，不重复添加或重置参数。

## 调用顺序

Play 中一次暂存多个原型；同一个类只输出一个 `.cs`，不再为每个实例起新类名：

```csharp
var batch = await engine.LiveCode.ExportMany(new[] {
    new YokiFrame.YokiFrameLiveExportRequest("Target01", "Assets/Weapons/TargetPatrol.cs"),
    new YokiFrame.YokiFrameLiveExportRequest("Target02", "Assets/Weapons/TargetPatrol.cs"),
    new YokiFrame.YokiFrameLiveExportRequest("Target03", "Assets/Weapons/TargetPatrol.cs")
});
engine.ConsoleLog(batch.BatchId);
```

三者须已 Attach，className 都为 TargetPatrol，成员源码一致；调参导致字段值不同不影响合并。
首次暂存仍需要活动 Play handle；后续更新不需要重新 Attach。文件名必须与类名一致，
但不是一个实例一个文件。一次最多 64 个请求、16 份不同脚本，每份脚本最多 64 份目标快照。
源码最多 128 KiB、每目标字段最多 64 KiB、单记录最多 4 MiB；source/scene 路径最多 1024 UTF-8 字节。

```bash
yoki command send --kit Engine --action live_export_commit --payload '{"batchId":"<batch>","target":"play","confirmed":true}'
yoki command send --kit Engine --action live_export_status --payload '{"batchId":"<batch>"}'
```

提交需要执行/trusted C# 双开关及确认；查询是诊断只读，关闭执行开关后仍可用。
等 `state=Committed` 且各版本 `sourceMatches=true, compiled=true`，再退出 Play，
等 EditMode 空闲后提交 `engine.LiveCode.Bind(exportId, target)`。从查询的 versions 按
sourcePath 取得 exportId；多目标版本必须显式传 target，未捕获的额外目标也可绑定。

后续 EditMode 更新：`await engine.LiveCode.Reexport(exportId, newMembers)` 返回新 Prepared
批次，再独立提交，target 改为 editor。newMembers 仍是类成员文本，不是完整类。
现有组件无需删除/重加；绑定新 exportId 只更新账本，不把旧快照写回正在使用的组件。
单项旧 `await Export(id,path)` 仍会安排提交；不要在同一次脚本里循环 await Export 来做批量。

## 数据与安全边界

源码版本记录、批次提交记录、逐目标绑定记录分开保存。旧版本不可被新版本覆盖；
更新要求磁盘源码 hash 仍等于指定旧版本，并保留原 `.meta`。手工修改或旧版本冲突拒绝。
旧 schema=1 导出记录仍可绑定原对象；迁移更新时保留原文件，不篡改旧记录。
旧 bound=true 记录应先在 EditMode 调用 Bind(oldExportId, originalTarget)，为原目标唯一的
同类型组件建立新版账本，再 Reexport。旧格式没有组件 ID，首次迁移只能延续其旧保证；
binding=true 的中断旧记录明确拒绝自动认领。旧源文本在 Reexport 前另存 `.json.source`。

暂存只写 `.yokiframe/engine/live-exports/`，不改 Assets。提交状态为
Prepared/Queued/Committing/Committed/Failed/Partial；Committed 只表示文件写入完成，
不表示 Unity 编译成功。新版脚本带唯一程序集元数据标记，Bind 必须看到该版本确实已加载。
批次异常尝试只回滚本批写入且未被外部修改的文件；进程中断留下 Committing 时不自动重放。
导入和编译仍可能域重载，旧 Task 不负责串起后续 Bind。

绑定需要干净、已保存且已加载的场景，保存只涉及显式目标的场景。
逐目标记录保存组件 GlobalObjectId；中断时只认精确组件身份，不认“恰好有个同类型组件”。
每份脚本最多记录 256 个绑定目标。身份检查不是防篡改证明：实测删除后立即新增可复用
GlobalObjectId，这种相同身份的替换无法仅凭当前账本识别；不得声称能检测任意替换。
组件任意生命周期副作用不可回滚；保存失败和记录写入失败必须保留可对账状态。
v1 不自动迁移字段重命名/类型变化、不批量复制对象引用、不改 prefab 资产、不自动重载或保存用户脏场景。
修改成员字段结构时，现有序列化数据的保留仍取决于 Unity 自身规则；框架不保证无损迁移。

Unity 字段捕获/绑定支持一维数组、`List<T>`、`[Serializable]` 数据 class/struct，
嵌套使用与调参相同的字段白名单。集合最多 256 元素、深度 8、每目标展开最多 1024 节点，
64 KiB 字段记录限制继续生效。集合中所有 Unity 对象引用也必须已保存且可持久化；
运行时引用、临时 host、循环/共享托管对象、多态、字典、SerializeReference、直接嵌套集合
均拒绝。同一 Unity 对象可被多处引用。数据类不能有自定义基类。
恢复先解码全体字段再写回，嵌套字段的类型/结构变化直接失败；不自动迁移。
LiveCode 状态可表达 null class/集合；绑定到正式组件并保存/重开后，仍遵循 Unity
内联序列化规则，不保证保留 null 与空实例的区别，也不保留托管对象共享身份。
单份脚本先做 Roslyn 编译校验，但这不是 Unity asmdef 引用/整项目编译成功的证明；
导入后须查 compiled，不因 Committed 就继续绑定。不同暂存批次不提供全项目符号预留。

## 验收

1. 三个靶子共享一个类，各自参数与引用不串。
2. 四项一次暂存、一次提交导入；重载后能按批次查询。
3. EditMode Reexport 原位改代码，GUID 不变，现有组件和字段保留。
4. 重复 Bind 幂等；旧 hash、手工文件修改、身份不同的替代组件、脏场景拒绝。
5. 无效批次不写 Assets，部分提交/中断查询不执行用户代码、不自动重放。

## 验证证据

- EngineKit 263/263，Roslyn 7/7，Godot Editor 31/31、Runtime 96/96；Unity CompileGate
  0 警告/0 错误。测试工程保留原有 nullable 警告，未扩展 Godot 导出能力。
- `43cbab366490408b9b632ca18024b47f`：6 个原型合并成 4 份脚本，暂存不写 Assets、场景不变脏。
  批次 `3d6e9fff805346aba28d96e7255226eb` 一次提交后四份均 compiled=true。
- `84e032e3780a43069d4cfd86a27ec087`：12/12，共享类三目标各自数值/引用、额外目标默认值及幂等绑定。
- `8cfc3b91b7204940ae19696198ca1238`：2/2，无活动 handle 的 EditMode Reexport。
- `59d84477c75648dba95e2823db96d7e7`：21/21，新代码生效，脚本 GUID、四个组件 ID、
  原数值 10/20/30/7 和对象引用均保留；新源码默认值 999 未覆盖现有参数。
  最终代码复测 `32435e0ab76347e5a0c842f81da94349` 同样为 21/21。
- `b4878dcabddd43df8385adfd497420d3`：11/11，旧版本/脏场景/不同 ID 替代组件拒绝；
  测试 Undo 后原组件仍可绑定。`98600f0ceba94638a56e00f3131c7f68`：7/7，旧记录兼容且不改写历史。
- 连续三次 live_export_status 查询后，22 份导出/账本文件内容 hash 不变。单测另覆盖源码/meta
  冲突整批拒绝、写入失败回滚、Committing 中断不重放、损坏记录和取消/会话失效。

验收脚本位于项目 `scripts/engine/export-v2-*.csx`，测试场景位于
`Assets/EngineKitExportValidation_20261006A/ExportValidation.unity`；未保存用户原有场景。
收尾 `257526c2d0c24115917d168ca13417f7`：4/4，EditMode、无 Live handle，
只关闭干净的隔离测试场景，磁盘证据保留。文档与安装 Skill 两份 reference 的 SHA-256
一致，两侧 Skill 校验均通过；任务文件空白检查通过，全仓 diff-check 仍有原有 `.meta` 尾空格。
初版空引用断言曾误用 object 判空，已改为 Unity 对象判空。一次替换拒绝测试暴露了上述 ID
复用限制，保留为已知边界，不计为任意替换检测能力。
