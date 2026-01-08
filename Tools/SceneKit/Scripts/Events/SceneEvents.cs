using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 场景开始加载事件
    /// </summary>
    public struct SceneLoadStartEvent
    {
        /// <summary>
        /// 场景名称
        /// </summary>
        public string SceneName;
        
        /// <summary>
        /// 加载模式
        /// </summary>
        public SceneLoadMode Mode;
    }

    /// <summary>
    /// 场景加载进度事件
    /// </summary>
    public struct SceneLoadProgressEvent
    {
        /// <summary>
        /// 场景名称
        /// </summary>
        public string SceneName;
        
        /// <summary>
        /// 加载进度（0-1）
        /// </summary>
        public float Progress;
    }

    /// <summary>
    /// 场景加载完成事件
    /// </summary>
    public struct SceneLoadCompleteEvent
    {
        /// <summary>
        /// 场景名称
        /// </summary>
        public string SceneName;
        
        /// <summary>
        /// Unity 场景引用
        /// </summary>
        public Scene Scene;
        
        /// <summary>
        /// 场景句柄
        /// </summary>
        public SceneHandler Handler;
    }

    /// <summary>
    /// 场景卸载事件
    /// </summary>
    public struct SceneUnloadEvent
    {
        /// <summary>
        /// 场景名称
        /// </summary>
        public string SceneName;
    }

    /// <summary>
    /// 活动场景切换事件
    /// </summary>
    public struct ActiveSceneChangedEvent
    {
        /// <summary>
        /// 之前的活动场景
        /// </summary>
        public Scene PreviousScene;
        
        /// <summary>
        /// 新的活动场景
        /// </summary>
        public Scene NewScene;
    }
}
