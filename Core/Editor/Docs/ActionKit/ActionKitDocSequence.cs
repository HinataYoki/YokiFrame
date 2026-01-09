#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 序列与并行文档
    /// </summary>
    internal static class ActionKitDocSequence
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "序列与并行",
                Description = "组合多个动作按顺序或同时执行。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "序列执行",
                        Code = @"// 按顺序执行多个动作
ActionKit.Sequence()
    .Append(ActionKit.Delay(1f, () => Debug.Log(""第1秒"")))
    .Append(ActionKit.Delay(1f, () => Debug.Log(""第2秒"")))
    .Append(ActionKit.Callback(() => Debug.Log(""完成"")))
    .Start(this);

// 嵌套序列
ActionKit.Sequence()
    .Append(ActionKit.Delay(1f, null))
    .Sequence(s => s
        .Append(ActionKit.Callback(() => Debug.Log(""嵌套1"")))
        .Append(ActionKit.Callback(() => Debug.Log(""嵌套2""))))
    .Start(this);",
                        Explanation = "Sequence 按顺序执行所有子动作。"
                    },
                    new()
                    {
                        Title = "并行执行",
                        Code = @"// 同时执行多个动作
ActionKit.Parallel()
    .Append(ActionKit.Delay(1f, () => Debug.Log(""动作A完成"")))
    .Append(ActionKit.Delay(2f, () => Debug.Log(""动作B完成"")))
    .Start(this, controller =>
    {
        Debug.Log(""所有动作完成"");
    });

// 不等待全部完成（任一完成即结束）
ActionKit.Parallel(waitAll: false)
    .Append(ActionKit.Delay(1f, null))
    .Append(ActionKit.Delay(2f, null))
    .Start(this);",
                        Explanation = "Parallel 同时执行所有子动作，默认等待全部完成。"
                    }
                }
            };
        }
    }
}
#endif
