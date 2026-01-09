#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 重复与条件文档
    /// </summary>
    internal static class ActionKitDocRepeat
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "重复与条件",
                Description = "循环执行动作或根据条件控制执行。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "重复执行",
                        Code = @"// 重复指定次数
ActionKit.Repeat(3)
    .Append(ActionKit.Delay(1f, () => Debug.Log(""重复执行"")))
    .Start(this);

// 无限重复
ActionKit.Repeat(-1)
    .Append(ActionKit.Delay(0.5f, () => Debug.Log(""每0.5秒执行"")))
    .Start(this);

// 条件重复
int count = 0;
ActionKit.Repeat(condition: () => count < 5)
    .Append(ActionKit.Callback(() => count++))
    .Append(ActionKit.Delay(0.5f, null))
    .Start(this);",
                        Explanation = "Repeat(-1) 表示无限重复，直到手动停止。"
                    }
                }
            };
        }
    }
}
#endif
