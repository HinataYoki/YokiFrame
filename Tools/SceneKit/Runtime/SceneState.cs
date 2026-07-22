namespace YokiFrame
{
    /// <summary>表示 SceneKit 场景 Handler 的生命周期状态。</summary>
    public enum SceneState
    {
        /// <summary>尚未开始加载。</summary>
        None = 0,

        /// <summary>正在加载。</summary>
        Loading = 1,

        /// <summary>已经加载完成。</summary>
        Loaded = 2,

        /// <summary>正在卸载。</summary>
        Unloading = 3,

        /// <summary>已经卸载。</summary>
        Unloaded = 4,

        /// <summary>加载失败，等待调用方清理。</summary>
        Failed = 5
    }
}
