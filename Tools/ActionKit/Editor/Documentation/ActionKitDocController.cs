#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 控制器文档
    /// </summary>
    internal static class ActionKitDocController
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "控制器",
                Description = "通过控制器管理动作的暂停、恢复、取消和停止。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础控制",
                        Code = @"// 获取控制器
var controller = ActionKit.Sequence()
    .Append(ActionKit.Delay(5f, null))
    .Start();

// 暂停
controller.Paused = true;
// 或使用扩展方法
controller.Pause();

// 恢复
controller.Paused = false;
// 或使用扩展方法
controller.Resume();

// 切换暂停状态
controller.TogglePause();",
                        Explanation = "控制器提供对动作执行过程的完整控制，支持暂停和恢复。"
                    },
                    new()
                    {
                        Title = "取消动作",
                        Code = @"// 启动一个长时间动作
var controller = ActionKit.Delay(10f, () => 
    Debug.Log(""完成""))
    .Start();

// 提前取消（不会触发完成回调）
controller.Cancel();

// 检查是否已取消
if (!controller.IsCancelled)
{
    Debug.Log(""动作仍在执行"");
}",
                        Explanation = "Cancel() 会立即终止动作并回收资源，不会触发完成回调。"
                    },
                    new()
                    {
                        Title = "超时取消",
                        Code = @"// 模拟网络请求（5秒完成）
var requestController = ActionKit.Delay(5f, () =>
{
    Debug.Log(""请求成功"");
}).Start();

// 设置 3 秒超时
ActionKit.Delay(3f, () =>
{
    if (!requestController.IsCancelled)
    {
        Debug.LogWarning(""请求超时，取消请求"");
        requestController.Cancel();
    }
}).Start();",
                        Explanation = "常用于网络请求、资源加载等需要超时控制的场景。"
                    },
                    new()
                    {
                        Title = "条件取消",
                        Code = @"// 启动无限循环
var loopController = ActionKit.Repeat(-1)
    .Append(ActionKit.Delay(0.5f, () => 
        Debug.Log(""循环中..."")))
    .Start();

// 监控取消条件
ActionKit.Repeat(-1)
    .Append(ActionKit.Delay(0.1f, () =>
    {
        if (ShouldStop())
        {
            loopController.Cancel();
        }
    }))
    .Start();",
                        Explanation = "通过条件判断动态取消动作，适用于游戏逻辑控制。"
                    },
                    new()
                    {
                        Title = "完成回调",
                        Code = @"// 完成回调（正常完成时触发）
ActionKit.Delay(1f, null)
    .Start(ctrl => Debug.Log(""动作完成""));

// 取消不会触发完成回调
var controller = ActionKit.Delay(5f, null)
    .Start(ctrl => Debug.Log(""不会输出""));
    
controller.Cancel(); // 立即取消",
                        Explanation = "Start() 的回调参数仅在动作正常完成时触发，取消时不会调用。"
                    }
                }
            };
        }
    }
}
#endif
