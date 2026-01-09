#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 异步动作文档
    /// </summary>
    internal static class ActionKitDocAsync
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "异步动作",
                Description = "支持协程、Task 和 UniTask 的异步动作。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "协程动作",
                        Code = @"// 包装协程
ActionKit.Coroutine(() => LoadResourceCoroutine())
    .Start(this);

IEnumerator LoadResourceCoroutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log(""资源加载完成"");
}",
                        Explanation = "Coroutine 动作将协程包装为 ActionKit 动作。"
                    },
                    new()
                    {
                        Title = "UniTask 动作",
                        Code = @"// 包装 UniTask
ActionKit.UniTask(() => LoadResourceAsync())
    .Start(this);

// 支持取消
ActionKit.UniTask(async ct =>
{
    await UniTask.Delay(1000, cancellationToken: ct);
    Debug.Log(""完成"");
}).Start(this);

// UniTask 延时（推荐）
ActionKit.DelayUniTask(2f, () => Debug.Log(""2秒后""))
    .Start(this);

// 等待条件
ActionKit.WaitUntil(() => isReady, () => Debug.Log(""条件满足""))
    .Start(this);",
                        Explanation = "UniTask 版本性能更好，推荐在支持 UniTask 的项目中使用。"
                    }
                }
            };
        }
    }
}
#endif
