#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 预加载与暂停/恢复文档
    /// </summary>
    internal static class SceneKitDocPreload
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "预加载与暂停/恢复",
                Description = "支持场景预加载和加载暂停/恢复，用于优化加载体验。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "预加载场景",
                        Code = @"// 预加载场景（加载到 90% 后暂停）
var handler = SceneKit.PreloadSceneAsync(""NextLevel"",
    onComplete: h => Debug.Log(""预加载完成，等待激活""),
    onProgress: p => Debug.Log($""预加载进度: {p:P0}""),
    suspendAtProgress: 0.9f);

// 稍后激活预加载的场景
SceneKit.ActivatePreloadedScene(handler);",
                        Explanation = "预加载默认在 90% 进度暂停，兼容 YooAsset 的加载机制。"
                    },
                    new()
                    {
                        Title = "手动暂停/恢复",
                        Code = @"// 加载时指定暂停阈值
var handler = SceneKit.LoadSceneAsync(""GameScene"",
    suspendAtProgress: 0.9f);

// 手动暂停加载
SceneKit.SuspendLoad(handler);

// 恢复加载
SceneKit.ResumeLoad(handler);

// 检查暂停状态
if (handler.IsSuspended)
{
    Debug.Log(""场景加载已暂停"");
}"
                    }
                }
            };
        }
    }
}
#endif
