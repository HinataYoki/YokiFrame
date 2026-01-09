#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 异步加载文档
    /// </summary>
    internal static class ResKitDocAsync
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "异步加载",
                Description = "异步加载资源，避免阻塞主线程。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "回调方式",
                        Code = @"// 异步加载
ResKit.LoadAsync<GameObject>(""Prefabs/Boss"", prefab =>
{
    if (prefab != null)
    {
        Instantiate(prefab, spawnPoint);
    }
});

// 异步实例化
ResKit.InstantiateAsync(""Prefabs/Effect"", effect =>
{
    effect.transform.position = targetPos;
}, parent);"
                    },
                    new()
                    {
                        Title = "UniTask 方式",
                        Code = @"#if YOKIFRAME_UNITASK_SUPPORT
// 使用 UniTask 异步加载
var prefab = await ResKit.LoadUniTaskAsync<GameObject>(""Prefabs/Boss"");
var instance = Instantiate(prefab);

// 支持取消
var cts = new CancellationTokenSource();
try
{
    var sprite = await ResKit.LoadUniTaskAsync<Sprite>(""Sprites/Icon"", cts.Token);
}
catch (OperationCanceledException)
{
    Debug.Log(""加载已取消"");
}

// 异步实例化
var player = await ResKit.InstantiateUniTaskAsync(""Prefabs/Player"", parent);
#endif",
                        Explanation = "需要定义 YOKIFRAME_UNITASK_SUPPORT 宏启用 UniTask 支持。"
                    }
                }
            };
        }
    }
}
#endif
