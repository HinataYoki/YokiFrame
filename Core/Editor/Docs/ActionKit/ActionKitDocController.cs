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
                Description = "通过控制器管理动作的暂停、恢复和停止。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "动作控制",
                        Code = @"// 获取控制器
var controller = ActionKit.Sequence()
    .Append(ActionKit.Delay(5f, null))
    .Start(this);

// 暂停
controller.Pause();

// 恢复
controller.Resume();

// 停止（会触发回收）
controller.Stop();

// 完成回调
ActionKit.Delay(1f, null)
    .Start(this, ctrl => Debug.Log(""动作完成""));",
                        Explanation = "控制器提供对动作执行过程的完整控制。"
                    }
                }
            };
        }
    }
}
#endif
