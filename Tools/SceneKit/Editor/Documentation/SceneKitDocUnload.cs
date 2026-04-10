#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 场景卸载文档
    /// </summary>
    internal static class SceneKitDocUnload
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "场景卸载",
                Description = "卸载已加载的场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "卸载场景",
                        Code = @"// 通过场景名卸载
SceneKit.UnloadSceneAsync(""UIScene"", () =>
{
    Debug.Log(""场景已卸载"");
});

// 通过句柄卸载
var handler = SceneKit.GetSceneHandler(""UIScene"");
SceneKit.UnloadSceneAsync(handler);

// 清理所有附加场景（保留活动场景）
SceneKit.ClearAllScenes(preserveActive: true, () =>
{
    Debug.Log(""所有附加场景已清理"");
});

// 卸载未使用的资源
SceneKit.UnloadUnusedAssets();"
                    }
                }
            };
        }
    }
}
#endif
