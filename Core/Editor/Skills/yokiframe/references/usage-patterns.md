# YokiFrame 通用约束与跨 Kit 坑

本文件只记录各 Kit API 主页面没有重复强调的通用约束和易错点。最小骨架和完整签名一律读 `Documentation~/Api/` 对应主页面，不要凭记忆扩展 API。

## 通用约束（写任何 Kit 代码前先过一遍）

- 业务代码只依赖 Core 或当前 Tool 的公开 API，不引用宿主类型；宿主差异交给 Adapter。
- 每个事件订阅、资源 handle、状态机、动作 controller、异步工作都要有明确 owner；owner 退出时注销、释放或取消。
- Runtime 代码保持 C# 9.0 兼容语法。
- Unity 对象判空用 `== default` / `!= default`，不用 `?.` / `??`；纯 C# 对象可正常使用。
- 热路径（`Update`、Tick、池循环、协议轮询）禁 LINQ、闭包分配、装箱和临时集合。
- 显式注入的 Provider/Backend 始终优先；默认后端只在第一次真实业务调用时惰性创建，读取和诊断调用不得隐式创建业务后端。
- TableKit 未生成前项目不存在对应 Runtime 类型；不要提前引用。

## 跨 Kit 高频坑

| 现象 | 处理 | 详见 |
|---|---|---|
| 资源/场景拿不到 | Provider 必须在第一次资源调用前显式注入；场景流程由 SceneKit 编排，不走 ResKit 场景入口 | `02-Core/ResKit.md`、`03-Tool/SceneKit.md` |
| `Change` 切状态无效 | 目标已 `Add`、状态机已启动、目标 `Condition()` 为 true；FSM 不会自动 Tick | `02-Core/FsmKit.md` |
| 池释放后仍被借还 | 池 owner 负责 `Dispose`；同一对象不要交给两个池 | `02-Core/PoolKit.md` |
| 动作跨线程异常 | Start、Tick、暂停、恢复必须在同一宿主线程；跨线程只允许 `Cancel()` | `03-Tool/ActionKit.md` |
| 事件订阅泄漏 | 订阅方 owner 停用时注销；不要依赖 `Clear()` | `02-Core/EventKit.md` |
| 存档无法区分空档与损坏 | 需要区分时用 `TryLoad`；玩家存档用 `SaveTarget.Slot(n)`，全局设置用 `SaveTarget.Global` | `03-Tool/SaveKit.md` |
| 音频句柄失效 | 保留完整 `AudioVoiceHandle`；自定义 Bus 优先显式注册 | `03-Tool/AudioKit.md` |
| UIKit 面板重复或 Root 无效 | 每种 Panel 类型最多一个实例；Root 创建后不可替换，定制用 Prefab Variant + `UIKit.SetRootPrefab` | `03-Tool/UIKit.md` |
| Godot 引入 UIKit | UIKit 仅 Unity；Godot 不创建 UIKit Adapter、Backend 或占位状态 | `03-Tool/UIKit.md` |

## 完成后验证

1. Unity：让宿主完成编译并确认 Console 无 Error；Godot：编译对应 .NET 工程确认无错误。
2. 需要运行态证据时先用 `yoki kit status` 取最小状态，需要指定通道时才用 `telemetry read` / `snapshot read`；命令面见 [cli-commands.md](cli-commands.md)。
3. 发现框架行为与文档不一致时向用户报告差异；修改包内文档属于 YokiFrame 框架开发者职责。
