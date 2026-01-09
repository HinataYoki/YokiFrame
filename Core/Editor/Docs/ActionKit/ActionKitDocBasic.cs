#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 基础动作文档
    /// </summary>
    internal static class ActionKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基础动作",
                Description = "ActionKit 提供多种基础动作类型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "延时动作",
                        Code = @"// 延时执行
ActionKit.Delay(2f, () => Debug.Log(""2秒后执行""))
    .Start(this);

// 延时帧数
ActionKit.DelayFrame(10, () => Debug.Log(""10帧后执行""))
    .Start(this);

// 下一帧执行
ActionKit.NextFrame(() => Debug.Log(""下一帧执行""))
    .Start(this);",
                        Explanation = "Start 方法需要传入 MonoBehaviour 作为执行载体。"
                    },
                    new()
                    {
                        Title = "回调与插值",
                        Code = @"// 立即执行回调
ActionKit.Callback(() => Debug.Log(""立即执行""))
    .Start(this);

// 数值插值
ActionKit.Lerp(0f, 100f, 1f, 
    value => slider.value = value,
    () => Debug.Log(""插值完成""))
    .Start(this);",
                        Explanation = "Lerp 动作在指定时间内从 a 插值到 b，每帧调用 onLerp 回调。"
                    }
                }
            };
        }
    }
}
#endif
