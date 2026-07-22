namespace YokiFrame
{
    /// <summary>场景开始加载事件。</summary>
    public sealed class SceneLoadStartEvent
    {
        /// <summary>获取或设置场景名称。</summary>
        public string SceneName { get; set; }

        /// <summary>获取或设置加载模式。</summary>
        public SceneLoadMode Mode { get; set; }
    }

    /// <summary>场景加载进度事件。</summary>
    public sealed class SceneLoadProgressEvent
    {
        /// <summary>获取或设置场景名称。</summary>
        public string SceneName { get; set; }

        /// <summary>获取或设置当前进度。</summary>
        public float Progress { get; set; }
    }

    /// <summary>场景加载完成事件。</summary>
    public sealed class SceneLoadCompleteEvent
    {
        /// <summary>获取或设置场景名称。</summary>
        public string SceneName { get; set; }

        /// <summary>获取或设置场景句柄。</summary>
        public SceneHandle Scene { get; set; }

        /// <summary>获取或设置场景 Handler。</summary>
        public SceneHandler Handler { get; set; }
    }

    /// <summary>场景加载失败事件。</summary>
    public sealed class SceneLoadFailedEvent
    {
        /// <summary>获取或设置场景名称。</summary>
        public string SceneName { get; set; }

        /// <summary>获取或设置失败 Handler。</summary>
        public SceneHandler Handler { get; set; }
    }

    /// <summary>场景卸载事件。</summary>
    public sealed class SceneUnloadEvent
    {
        /// <summary>获取或设置场景名称。</summary>
        public string SceneName { get; set; }
    }

    /// <summary>激活场景切换事件。</summary>
    public sealed class ActiveSceneChangedEvent
    {
        /// <summary>获取或设置旧场景。</summary>
        public SceneHandle PreviousScene { get; set; }

        /// <summary>获取或设置新场景。</summary>
        public SceneHandle NewScene { get; set; }
    }
}
