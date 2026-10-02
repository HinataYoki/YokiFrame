# 业务代码怎么写

先在包根 `Documentation~/Api/00-GettingStarted/FrameworkOverview.md` 确认该 Kit 的 Runtime API 可用，再打开对应主页面使用其中的签名和示例。Runtime 代码使用 C# 9.0。Unity 对象用 `== default` / `!= default` 判断是否存在。

事件订阅、资源 handle、状态机、动作 controller 和异步工作都交给明确的 owner。owner 退出时注销、释放或取消。显式注入的 Provider 或 Backend 直接使用；没有注入时，默认实现在第一次业务调用时创建。

| 要做的事 | 怎么做 | 主页面 |
|---|---|---|
| 加载资源 | 第一次资源调用前注入 Provider，之后使用返回的 handle，并由 owner 释放 | `Api/02-Core/ResKit.md` |
| 切换场景 | 用 SceneKit 编排加载、切换和卸载 | `Api/03-Tool/SceneKit.md` |
| 切换状态 | 先 `Add` 目标并启动状态机，目标 `Condition()` 为 true 后再 `Change`。业务在宿主生命周期里 Tick | `Api/02-Core/FsmKit.md` |
| 复用对象 | 从一个池借出，还给同一个池。池的 owner 调用 `Dispose` | `Api/02-Core/PoolKit.md` |
| 编排一段流程 | 在宿主线程上 Start、Tick、暂停和恢复。其它线程结束流程时调用 `Cancel()` | `Api/03-Tool/ActionKit.md` |
| 订阅事件 | 保存 `Register` 返回的 link，owner 停用时注销 | `Api/02-Core/EventKit.md` |
| 读写存档 | 玩家进度用 `SaveTarget.Slot(n)`，全局设置用 `SaveTarget.Global`。区分空档和损坏时用 `TryLoad` | `Api/03-Tool/SaveKit.md` |
| 播放音频 | 保存完整的 `AudioVoiceHandle`。自定义 Bus 先注册再播放 | `Api/03-Tool/AudioKit.md` |
| 打开 Unity 面板 | 每种 Panel 类型保留一个实例。定制根节点时做 Prefab Variant，并在第一次打开前调用 `UIKit.SetRootPrefab` | `Api/03-Tool/UIKit.md` |

UIKit 的面板、绑定和代码生成在 Unity 里完成。Godot 项目的界面使用 Godot 自己的 UI。
