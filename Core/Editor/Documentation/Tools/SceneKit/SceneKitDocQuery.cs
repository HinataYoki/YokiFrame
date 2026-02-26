#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 场景查询文档
    /// </summary>
    internal static class SceneKitDocQuery
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "场景查询",
                Description = "查询场景状态和信息。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "查询场景",
                        Code = @"// 获取当前活动场景
var activeScene = SceneKit.GetActiveScene();
Debug.Log($""活动场景: {activeScene.name}"");

// 获取活动场景句柄
var handler = SceneKit.GetActiveSceneHandler();

// 检查场景是否已加载
if (SceneKit.IsSceneLoaded(""GameScene""))
{
    Debug.Log(""GameScene 已加载"");
}

// 获取指定场景的句柄
var gameHandler = SceneKit.GetSceneHandler(""GameScene"");
Debug.Log($""状态: {gameHandler.State}, 进度: {gameHandler.Progress}"");

// 获取所有已加载场景
var loadedScenes = SceneKit.GetLoadedScenes();
foreach (var h in loadedScenes)
{
    Debug.Log($""场景: {h.SceneName}, 状态: {h.State}"");
}

// 检查是否正在过渡
if (SceneKit.IsTransitioning)
{
    Debug.Log(""场景切换进行中..."");
}"
                    }
                }
            };
        }
    }
}
#endif
