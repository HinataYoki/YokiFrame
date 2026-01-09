#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit Task 动作文档
    /// </summary>
    internal static class ActionKitDocTask
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Task 动作",
                Description = "支持原生 Task 和条件等待的动作类型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Task 包装",
                        Code = @"// 包装 Task 为 ActionKit 动作
ActionKit.Task(() => LoadDataAsync())
    .Start(this);

async Task LoadDataAsync()
{
    await Task.Delay(1000);
    Debug.Log(""数据加载完成"");
}",
                        Explanation = "Task 动作将 async/await 任务包装为 ActionKit 动作，适合不使用 UniTask 的项目。"
                    },
                    new()
                    {
                        Title = "WaitWhile 条件等待",
                        Code = @"// 等待条件为假时继续（与 WaitUntil 相反）
ActionKit.WaitWhile(
    () => isLoading,  // 当 isLoading 为 false 时继续
    () => Debug.Log(""加载完成"")
).Start(this);

// 实际应用：等待动画播放完成
ActionKit.WaitWhile(
    () => animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f,
    () => OnAnimationComplete()
).Start(this);",
                        Explanation = "WaitWhile 在条件为 true 时持续等待，条件变为 false 时执行回调。"
                    },
                    new()
                    {
                        Title = "Lerp 完整参数",
                        Code = @"// Lerp 基础用法
ActionKit.Lerp(0f, 100f, 2f, 
    value => healthBar.fillAmount = value / 100f
).Start(this);

// 带完成回调
ActionKit.Lerp(0f, 1f, 0.5f,
    onLerp: t => canvasGroup.alpha = t,
    onLerpFinish: () => Debug.Log(""淡入完成"")
).Start(this);

// 在序列中使用
ActionKit.Sequence()
    .Lerp(0f, 1f, 0.3f, t => transform.localScale = Vector3.one * t)
    .Callback(() => Debug.Log(""缩放完成""))
    .Start(this);",
                        Explanation = "Lerp 动作支持 onLerp（每帧回调）和 onLerpFinish（完成回调）两个参数。"
                    }
                }
            };
        }
    }
}
#endif
