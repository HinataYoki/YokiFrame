namespace YokiFrame
{
    /// <summary>
    /// 场景开始加载事件。
    /// </summary>
    public sealed class SceneLoadStartEvent
    {
        public string SceneName { get; set; }
        public SceneLoadMode Mode { get; set; }
    }

    /// <summary>
    /// 场景加载进度事件。
    /// </summary>
    public sealed class SceneLoadProgressEvent
    {
        public string SceneName { get; set; }
        public float Progress { get; set; }
    }

    /// <summary>
    /// 场景加载完成事件。
    /// </summary>
    public sealed class SceneLoadCompleteEvent
    {
        public string SceneName { get; set; }
        public SceneHandle Scene { get; set; }
        public SceneHandler Handler { get; set; }
    }

    /// <summary>
    /// 场景卸载事件。
    /// </summary>
    public sealed class SceneUnloadEvent
    {
        public string SceneName { get; set; }
    }

    /// <summary>
    /// 激活场景切换事件。
    /// </summary>
    public sealed class ActiveSceneChangedEvent
    {
        public SceneHandle PreviousScene { get; set; }
        public SceneHandle NewScene { get; set; }
    }
}
