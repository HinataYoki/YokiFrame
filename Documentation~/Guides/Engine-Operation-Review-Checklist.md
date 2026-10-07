# RoslynKit v5.2 / Roslyn 内存 C# 审查与验证清单

> 对应 [当前设计](Engine-Operation-Glue-Design.md)。**Unity Roslyn 最小纵切已实现并真机验证；不是 v5 整体迁移完成。** 勾选项仅代表下面明确列出的验证范围，未勾选项仍待完成或扩大覆盖。
> 目标是宿主使用 Roslyn 内存编译 C#，CLI 负责传输与查询；保留完整自动化工作流，不实现 TS 外观或自研解析器，不保留 `[YokiFrameEntry]` 或 `entry_*` 作为目标 API。
> 唯一执行契约见 [Roslyn 自动化](Engine-Roslyn-Automation-Contract.md)；编译组件、依赖分发、宿主接线和真机纵切分别验收，不能相互代替。
> 旧测试总数、命令示例和 P0-P4 记录见 [v4 历史清单](Engine-Operation-Review-Checklist-v4-History.md)，不能当作 v5 验收结果，也不要直接执行旧清单里的场景修改或写操作重试。

## 1. 当前结论

| 项目 | 证据与限制 |
|---|---|
| 旧桥接基础 | Gate、target、FileBridge、运行记录与编辑器操作已有实现，复用部分仍需回归 |
| 本次示例已观察到 | Unity 内两段外部 C# 直接操作相同 Model/System，Health 120 -> 85 -> 80、Attack 18 -> 25 -> 27，真实游戏帧、15 条断言、控制台日志及 PNG 均验收；没有调用旧入口 |
| 已暴露的问题 | 为临时读数/截图新增 C# 入口需要编译；通用 inspect 的 service 根曾未找到已经注册的 Architecture；根因和修复需专门复现验证 |
| 新增能力 | Unity/Godot Tools 内存执行、LiveCode 恢复/批量/调参、run_*、CLI script 与 service 目录已实现；旧入口/Unity 落盘 eval 已退出；Godot Capture/Patch/Export/Bind 与安装器分发未完成 |
| 本轮验证范围 | 入口退出和只读发现的测试及真机结果见设计 §14.0；旧纵切证据单独保留。Skill 更新不能当作运行依赖已安装 |

## 2. 设计准入

- [ ] 正式分发编译器与新 CLI 的 Native AOT 发布回归完成。宿主编译已验证，但 CLI 原 Tooling.Application 本来依赖 Roslyn，不能宣称整个 CLI 项目图无 Roslyn。
- [x] 普通 C# 方法无需测试属性、专用返回值、入口上下文或逐任务包装（Unity）。
- [x] `entry_*` 不是脚本的隐藏后端；新路径直接提交瞬态 work，不生成任务源码文件、不调用旧 T2 eval。
- [x] Skill 区分新 CLI 已实现和已安装旧版本，说明编译器准备及授权，不把尚未实现的 Godot 路径当成可用。
- [ ] 内建函数按实际 capabilities 判断支持范围，Unity/Godot 不支持的能力明确失败。
- [x] object_list/object_describe 不初始化 Architecture、不调用 getter、方法、属性构造器、枚举器或 ToString，有副作用哨兵测试。
- [x] 受信任 C# 执行默认关闭；target、主线程调度和确认门禁有明确实现位置，不把方法白名单宣称为任意 C# 沙箱。

## 3. 自动化验证

以下只勾选本轮实际覆盖的范围；旧测试覆盖不能替代 Godot、发布和迁移验收。

### 3.0 Roslyn 编译与加载

- [x] 真实 CSharpCompilation 编译完整输入，在 MemoryStream 输出 PE/PDB；不使用假编译器。
- [x] 编译成功前零用户代码执行；末尾语法错误同样不能执行前面的业务调用。
- [x] 编译诊断保留 code、severity、message、虚拟路径及行列，模板映射回用户方法体。
- [ ] 显式引用宿主基础库、引擎/框架/业务程序集；缺失/重复/冲突引用可诊断，不扫描磁盘全部 DLL。
- [x] 通过内存加载产物调用已加载的测试服务，包含 Task/await；引用的服务不因脚本变化重新初始化。
- [x] 不提供 #r/#load、外部 shell/compiler 或 analyzer/source generator 执行路径。
- [ ] 输入、产物、诊断和每代加载预算受限；取消编译后不加载。
- [x] 任务 DLL/PDB 不落盘；不替换已有业务程序集，脚本基础设施不调用 Unity Refresh/编译/重载 API。
- [ ] 固定 Roslyn 依赖的 Unity Mono 兼容、分发闭包、许可证和同名 DLL 冲突得到验证。
- [x] 不宣称逐个卸载 Unity 程序集；prune 不重置累计预算，超限拒绝而非自动重载。
- [ ] 脚本编译器不成为 CLI 执行后端，原 Native AOT 发布路径实测通过；当前只验证 managed 开发 CLI，未重跑 AOT。

### 3.1 发现与读取

- [x] RequireService 通过活动 Architecture 取得 Model/System；独立 object_* 已实现 service 根，不依赖私有字段或懒初始化。
- [x] 服务替换/注销、session/generation/target 和宿主显式生命周期失效有测试；其他对象根不在本批范围。
- [x] 公开成员元数据包含精确签名、参数/default、返回、async、拒绝原因；重载 ID 不同，发现不等于授权调用。
- [x] service 目录分页限制、修订检查和载荷边界有测试；响应/文本有硬上限。
- [x] 多 Architecture 同类型服务不误绑定；按架构完整类型名精确选择。
- [ ] 扩展到 service 之外的对象根后，对象销毁、场景替换及跨宿主旧句柄正确失败；不能用本批服务注册测试代替。
- [ ] 后续通用 invoke 按成员 ID 精确绑定重载，不只按名称命中；当前目录已提供签名与 ID，但没有 invoke action。
- [ ] 只读 inspect 不执行 getter、自定义枚举器或 ToString；需要执行的访问通过 invoke。
- [ ] 深度、集合数量、文本及总字节数有界；截断明确，不把缺省值当真实数据。

### 3.2 调用与运行

- [ ] 无属性的实例/静态公开方法可在允许范围内调用；范围外、私有方法和不支持签名在执行前拒绝。
- [ ] 无效参数不进入业务方法；重载、数值溢出、空值和 DTO 绑定有确定行为。
- [ ] `void` / 值 / `Task` / `Task<T>` 覆盖；异步观察不阻塞主线程，异常保留栈。
- [x] accepted 只表示入队；宿主 `Succeeded` 不自动等于整个脚本测试 `Passed`。
- [x] 请求记录先于响应；重复 requestId 不重复执行，不同载荷复用 id 应报冲突。
- [x] 脚本 `run_result` / `run_lookup` 查询不推进执行、不认领运行、不调用用户代码；缺索引查询不写入修复。
- [ ] 取消、超时、宽限期、Detached、终态 CAS、迟到完成与失效调用均有测试。
- [ ] CLI 中断后尝试取消宿主运行；不谎报所有 C# 已停止，不强杀同步循环。
- [ ] 响应丢失、宿主重载及过期记录返回可查证的未知结果，不自动重放写操作。

### 3.3 权限与双引擎

- [ ] 关闭、缺失、配置解析失败三态都拒绝新执行，结果查询与取消仍可用。
- [ ] Dangerous 不因标成诊断或取消而豁免；拒绝路径无用户代码副作用。
- [ ] 通用 invoke 拒绝范围外成员；任意 Roslyn C# 另行要求受信任执行授权，不把助手校验当成安全隔离。
- [x] 脚本未显式确认执行时不派发；确认不会自动开启项目设置，CLI 不暗中确认。
- [ ] ReadOnly 路径不跑用户 getter，FastChannel 不在后台直接访问引擎对象。
- [ ] 其他 Kit 权限保持不变；FileBridge 协议版本仍为 2。
- [ ] Unity editor/play 与 Godot editor/runtime 分别绑定；Godot play 意图映射有 capabilities 证据。
- [ ] `editorTick` 和 `gameFrame` 区分；暂停时不伪造帧推进。

### 3.4 Roslyn 接线与产物

- [ ] stdin 和可选 `.csx` 方法体文件均可用；编译失败不执行用户代码，不承诺提交前已由 CLI 编译。
- [x] 修改任务仅内存编译，不生成任务源码/DLL/PDB 文件、不导入资产、不请求 Unity 编译或重载；Unity 事件观察覆盖连续任务。
- [ ] Roslyn 随宿主工具包安装，不探测或下载外部 SDK；首次安装编译与日常任务执行分开测量。
- [ ] 结构化报告保留源码 hash、requestId、runId、宿主身份、编译诊断、断言、异常和产物。
- [x] 直接 C# 返回值遵守原引用语义，前后值用标量/显式快照对比，不伪称自动隔离。
- [x] Unity Game 截图已打开检查，1338x832，三个对象与数值可见；该模式不创建或修改相机。其他截图模式未实现。
- [ ] 路径穿越/链接逃逸、超大图像/日志/返回值被拒绝或有界截断。
- [x] Unity 使用 Debug.Log，Godot 使用 GD.Print，同时留存运行日志。

## 4. Unity 真机纵切

### 4.1 前置与保护

1. 先记录活动场景、是否脏、播放状态、执行设置、会话身份和现有相机。场景若已脏，不自动保存。
2. C# 示例放 `Assets/Scripts/`：Architecture 初始化并注册 Model/System，View 只显示和订阅；准备三个有不同数值的物体。
3. 完成业务脚本与 Roslyn 依赖安装编译后，再开始“外部任务变化不触发 Unity 资产编译/重载”的测量区间。
4. 查询在线 Engine 命令目录和 capabilities；未发现 v5 操作时，应报告尚未实现，不能回退到生成属性入口。
5. 仅在用户确认范围内设置执行开关和调用范围。记录原值，结束时恢复；本清单不授权修改用户配置。

### 4.2 正常路径

1. 不读本地业务源码，通过 CLI 列出活动 Architecture、Model/System、方法签名和当前值。
2. 调用 System 的普通公开方法改变其中一个物体，取得 requestId/runId 后查询真正完成。
3. 等待指定游戏帧，分别读取 Model 与 View，断言目标数值同步、其他物体不受影响。
4. 将本次取回的数据通过 `console_log` 写入 Unity 控制台，再从日志通道读回验证。
5. 通过通用 capture 截图，打开检查标签、数值、血条与物体；不要只验证文件存在。
6. 按主设计 §12 执行完整 C# 工作流，再仅修改任务参数、步骤或断言重跑。验证数据继续变化、没有重新初始化 Model；不能用旧属性入口代替。
7. 比较 sessionId/generation、Unity 编译事件和文件变化：无任务源码文件、无 asset refresh、无 Unity 资产编译或域重载。Roslyn 内存编译单独记录；两次 `isCompiling:false` 不足以证明中间没有 Unity 编译。
8. 恢复最初播放状态；不覆盖已有相机，不保存用户脏场景，只清理本任务创建的临时资源。

必须分别报告业务 C# 初次编译、桥接/依赖安装编译、任务 Roslyn 内存编译以及 Unity 资产编译事件；不宣称“整个开发过程零编译”。

### 4.3 失败路径

- [ ] 错误断言产生 `Failed`；业务异常产生 `Errored`，二者不能只用一个 `ok:false` 代替。
- [ ] 示例未捕获断言异常时不执行末尾日志/截图；保留 actual/expected/message。用户 catch 后已记录的失败仍不能变成 Passed。
- [ ] 错误参数、拒绝调用和无效对象句柄不修改业务状态。
- [ ] 暂停时等待 gameFrame 超时/等待，不能偷偷改成 editorTick。
- [ ] 主动触发 C# 重载后旧对象句柄失效；已完成结果可读，未完成调用不自动重放。
- [ ] 取消配合 token 的方法与忽略 token 的方法，分别得到符合事实的退出状态或 Detached。
- [ ] 截图/日志不支持当前目标或路径不可写时，返回真实失败而不是测试通过。

## 5. Godot 真机纵切

- [x] 编辑器操作在 `godot-editor`；游戏运行对象与 C# 方法在 `godot-runtime`，分别记录身份。
- [ ] 无需新增属性入口或写任务源码文件，内存 C# 发现并调用已编译的 Model/System。
- [ ] 复用同一 C# 工作流，按 capabilities 明确处理截图模式、帧时钟和目标差异。
- [x] Godot .NET 的加载上下文保持类型身份一致；Native AOT 导出明确不支持。
- [ ] 停止/重启游戏后旧 runtime 句柄失效，不能绑定到 editor 或新的同名对象。
- [ ] Model/View 数据同步与日志由真实进程返回，不以 mock 或仅构建通过代替。
- [ ] Godot 不支持的调用签名、捕获模式和 target 返回明确错误。

2026-10-06 已验证 Godot 4.7 .NET/Tools Editor/Runtime 的 LiveCode 恢复子集，见
Engine-LiveCode-Contract.md §7/§9。以上 Model/View 全工作流、截图和构建守卫未因此勾选；
进程重启恢复不代替内建 Build 热重载/GC 卸载验收。

## 6. 旧入口退出门

- [x] 生产 Provider/descriptor/capability.json 中不再发布 `entry_list/run/result/lookup/cancel`，不存在兼容别名。
- [x] 普通调用与 Roslyn 路径不引用入口扫描器、入口属性或 EntryContext。
- [x] Unity T2 生成属性入口的路径同步退出，不留“删入口后 eval 必坏”的依赖。
- [x] Godot GDScript eval 已解耦旧 Entry 类型，Editor/Runtime 测试通过；本轮未做 Godot 真机。
- [x] Workbench 改为对象/能力/通用运行投影，移除入口目录与旧快照字段依赖。
- [x] 活跃 Skill/调用示例/capability 声明与一致性守卫更新；CLI schema 没有旧动作硬编码。正式发布仍单独验收。
- [ ] 升级预检列出用户旧属性引用、CLI 调用方和生成残留；未迁移时阻止退出，不自动删除用户代码。
- [x] 老运行记录只读保留并标注 schema，不重新认领、不自动重放；queued/running/terminal 均有文件不变测试。
- [x] 历史文档中的旧词允许保留，但不能作为当前推荐路径或支持状态。

## 7. 回归与证据格式

继续实施时按受影响范围运行；当前证据见设计 §14，不因更新安装文档就扩大已验证范围：

- Core/RoslynKit：发现、Gate、绑定、调度和状态机测试。
- 编译组件：Roslyn 实编译、引用、诊断、内存产物、取消和边界。
- CLI/Tooling：脚本传输、Native AOT、重载、超时、中断和结构化输出。
- Unity：优先通过项目指定 Unity 工具链实际编译，再运行真机纵切；已有 Unity 编译门作补充。
- Godot：Editor/Runtime 带有效编译守卫的构建，以及真实宿主执行。
- Workbench/Packaging/Skill：旧入口依赖退出、能力版本、分发环境与文档漂移检查。

每次验收记录最少包含：

```text
版本/宿主/目标/起止时间
初始场景与播放状态、配置是否改变
scriptHash / runId
每步 requestId / runId / sessionId / generation
提交状态与最终状态
真实 Model/View 值、断言明细、异常
编译/重载观察证据、源码与产物变化
控制台日志证据、截图路径
取消/恢复/清理结果及仍未验证项
```

不能用“命令返回成功”“截图文件存在”“旧测试全绿”代替上述证据。v5.2 的核心完成标准是：**不新增属性入口、不改业务 C#，外部 C# 经 Roslyn 内存编译重复操作当前对象，完成用户要求的跨帧、断言、日志和截图流程，而不触发 Unity 资产编译/域重载。**
