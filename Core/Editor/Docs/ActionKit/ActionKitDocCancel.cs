#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 取消功能文档
    /// </summary>
    internal static class ActionKitDocCancel
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "取消与生命周期",
                Description = "ActionKit 支持提前取消动作，适用于超时控制、条件中断等场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础取消",
                        Code = @"// 启动动作并保存控制器
var controller = ActionKit.Delay(5f, () => 
    Debug.Log(""完成""))
    .Start();

// 2 秒后取消
ActionKit.Delay(2f, () =>
{
    controller.Cancel();
    Debug.Log(""已取消"");
}).Start();",
                        Explanation = "Cancel() 立即终止动作并回收资源，不触发完成回调。"
                    },
                    new()
                    {
                        Title = "取消状态检查",
                        Code = @"var controller = ActionKit.Delay(3f, null).Start();

// 检查是否已取消
if (controller.IsCancelled)
{
    Debug.Log(""动作已取消"");
}
else
{
    Debug.Log(""动作仍在执行"");
    controller.Cancel();
}",
                        Explanation = "IsCancelled 属性用于检查动作是否已被取消。"
                    },
                    new()
                    {
                        Title = "序列取消",
                        Code = @"// 创建多步序列
var controller = ActionKit.Sequence()
    .Append(ActionKit.Delay(1f, () => Debug.Log(""步骤1"")))
    .Append(ActionKit.Delay(1f, () => Debug.Log(""步骤2"")))
    .Append(ActionKit.Delay(1f, () => Debug.Log(""步骤3"")))
    .Start();

// 1.5 秒后取消（步骤2执行中）
ActionKit.Delay(1.5f, () =>
{
    controller.Cancel();
    Debug.Log(""序列已取消，步骤3不会执行"");
}).Start();",
                        Explanation = "取消序列会立即终止当前步骤，后续步骤不会执行。"
                    },
                    new()
                    {
                        Title = "无限循环取消",
                        Code = @"var loopCount = 0;

// 启动无限循环
var controller = ActionKit.Repeat(-1)
    .Append(ActionKit.Delay(0.5f, () =>
    {
        loopCount++;
        Debug.Log($""循环第 {loopCount} 次"");
    }))
    .Start();

// 3 秒后取消
ActionKit.Delay(3f, () =>
{
    controller.Cancel();
    Debug.Log($""循环已取消，共执行 {loopCount} 次"");
}).Start();",
                        Explanation = "无限循环必须通过 Cancel() 手动终止。"
                    },
                    new()
                    {
                        Title = "超时模式",
                        Code = @"// 模拟异步操作（可能很慢）
var operationController = ActionKit.Delay(10f, () =>
{
    Debug.Log(""操作完成"");
}).Start();

// 设置 3 秒超时
var timeoutController = ActionKit.Delay(3f, () =>
{
    if (!operationController.IsCancelled)
    {
        Debug.LogWarning(""操作超时"");
        operationController.Cancel();
        // 执行超时处理逻辑
        HandleTimeout();
    }
}).Start();

// 如果操作提前完成，取消超时检测
void OnOperationComplete()
{
    timeoutController.Cancel();
}",
                        Explanation = "超时模式常用于网络请求、资源加载等需要时间限制的场景。"
                    },
                    new()
                    {
                        Title = "条件取消",
                        Code = @"var shouldStop = false;

// 启动长时间任务
var taskController = ActionKit.Sequence()
    .Append(ActionKit.Delay(10f, null))
    .Start();

// 监控取消条件
ActionKit.Repeat(-1)
    .Append(ActionKit.Delay(0.1f, () =>
    {
        if (shouldStop || Input.GetKeyDown(KeyCode.Escape))
        {
            taskController.Cancel();
            Debug.Log(""任务已取消"");
        }
    }))
    .Start();

// 外部设置取消标记
public void StopTask()
{
    shouldStop = true;
}",
                        Explanation = "通过条件判断动态取消，适用于用户交互、游戏状态变化等场景。"
                    },
                    new()
                    {
                        Title = "生命周期管理",
                        Code = @"public class MyComponent : MonoBehaviour
{
    private IActionController mCurrentAction;

    private void Start()
    {
        // 启动长时间动作
        mCurrentAction = ActionKit.Repeat(-1)
            .Append(ActionKit.Delay(1f, () => 
                Debug.Log(""循环中..."")))
            .Start();
    }

    private void OnDisable()
    {
        // 组件禁用时取消动作
        mCurrentAction?.Cancel();
    }

    private void OnDestroy()
    {
        // 组件销毁时确保清理
        mCurrentAction?.Cancel();
    }
}",
                        Explanation = "在 OnDisable/OnDestroy 中取消动作，避免内存泄漏和空引用。"
                    },
                    new()
                    {
                        Title = "批量取消",
                        Code = @"public class ActionManager : MonoBehaviour
{
    private readonly List<IActionController> mActiveActions = new();

    public void StartAction(IAction action)
    {
        var controller = action.Start();
        mActiveActions.Add(controller);
    }

    public void CancelAll()
    {
        foreach (var controller in mActiveActions)
        {
            controller?.Cancel();
        }
        mActiveActions.Clear();
        Debug.Log(""所有动作已取消"");
    }

    private void OnDestroy()
    {
        CancelAll();
    }
}",
                        Explanation = "使用列表管理多个动作，支持批量取消。"
                    }
                }
            };
        }
    }
}
#endif
