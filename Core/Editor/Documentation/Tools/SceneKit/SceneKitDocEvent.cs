#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 场景事件文档
    /// </summary>
    internal static class SceneKitDocEvent
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "场景事件",
                Description = "监听场景加载、卸载等事件。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "事件监听",
                        Code = @"// 监听场景加载开始
EventKit.Type.Register<SceneLoadStartEvent>(e =>
{
    Debug.Log($""开始加载: {e.SceneName}, 模式: {e.Mode}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听加载进度
EventKit.Type.Register<SceneLoadProgressEvent>(e =>
{
    Debug.Log($""加载进度: {e.SceneName} - {e.Progress:P0}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听加载完成
EventKit.Type.Register<SceneLoadCompleteEvent>(e =>
{
    Debug.Log($""加载完成: {e.SceneName}"");
    // 可以访问 e.Scene 和 e.Handler
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听场景卸载
EventKit.Type.Register<SceneUnloadEvent>(e =>
{
    Debug.Log($""场景已卸载: {e.SceneName}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听活动场景切换
EventKit.Type.Register<ActiveSceneChangedEvent>(e =>
{
    Debug.Log($""活动场景从 {e.PreviousScene.name} 切换到 {e.NewScene.name}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);"
                    }
                }
            };
        }
    }
}
#endif
