#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit UniTask 支持文档
    /// </summary>
    internal static class SceneKitDocUniTask
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "UniTask 支持",
                Description = "使用 UniTask 进行异步场景操作。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "UniTask 异步加载",
                        Code = @"// 需要安装 UniTask 并定义 YOKIFRAME_UNITASK_SUPPORT

// 异步加载场景
var handler = await SceneKit.LoadSceneUniTaskAsync(""GameScene"");
Debug.Log($""场景加载完成: {handler.SceneName}"");

// 带取消令牌
var cts = new CancellationTokenSource();
try
{
    var handler = await SceneKit.LoadSceneUniTaskAsync(
        ""GameScene"",
        cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Debug.Log(""加载已取消"");
}

// 异步切换场景
await SceneKit.SwitchSceneUniTaskAsync(""GameScene"", new FadeTransition());

// 异步卸载场景
await SceneKit.UnloadSceneUniTaskAsync(""UIScene"");"
                    }
                }
            };
        }
    }
}
#endif
