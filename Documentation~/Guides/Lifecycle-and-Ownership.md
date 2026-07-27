# 生命周期与资源所有权

YokiFrame 的 Kit 可以独立组合，但每个长期对象都必须有明确的 owner。把 owner 写进业务模块的停止、禁用或销毁路径，能避免重复释放和后台回调。

## 统一规则

| 对象 | 谁负责 | 何时处理 |
| --- | --- | --- |
| 事件订阅令牌 | 注册事件的模块 | 模块停止或销毁时 `UnRegister()` |
| 状态机和状态 | 创建 FSM 的业务流程 | 流程结束时 `Dispose()`；状态由 FSM 统一释放 |
| Action controller | 启动动作树的模块 | 对象销毁、流程切换或用户跳过时 `Cancel()` |
| 资源 handle/lease | 请求资源的业务 owner | 不再使用时释放；不要跨 Provider 复用旧 handle |
| Scene handler | 发起场景操作的流程 | 使用完成后由创建它的后端卸载 |
| 异步任务 | 创建任务的业务代码 | 使用取消令牌绑定业务生命周期 |
| Workbench/CLI 操作 | 操作发起者 | 写入型操作先确认目标和结果，失败时按提示恢复 |

## 宿主生命周期

- Core API 不会自动接管 Unity `MonoBehaviour` 或 Godot `Node` 的销毁。
- Unity 中让组件只负责生命周期、输入和调用编排；状态、资源策略和业务规则放在普通 C# 类型。
- Godot 中把 `_Ready`、`_Process` 和 `_ExitTree` 映射到业务 owner，不在 `_Process` 中重复创建 Provider 或 scheduler。
- `Update`、`FixedUpdate`、ActionKit tick 等入口应由当前宿主只接入一次。

## 释放顺序

流程停止时建议按“停止新工作 -> 取消异步 -> 解除事件 -> 释放资源 -> 释放容器”的顺序执行。回调中不要对同一状态机或动作树发起嵌套变更；异常应交给调用方处理并记录日志。

## 常见错误

- 只保存 `AudioVoiceHandle.VoiceId`，丢失后端代次；应保存完整句柄。
- 用全局 `Clear()` 清理一个模块，误删其它模块的订阅或缓存。
- `Cancel()` 后仍期待普通 `Task` 自动停止；普通 Task 的取消权仍归业务自己的 `CancellationTokenSource`。
- 把资源 handle、Scene handler 或动作 controller 交给另一个后端继续使用。

各 Kit 的具体生命周期约束以其 API 主页面为准。
