namespace YokiFrame
{
    /// <summary>
    /// 表示场景加载生命周期状态。
    /// </summary>
    public enum SceneState
    {
        /// <summary>
        /// 未指定状态。
        /// </summary>
        None = 0,

        /// <summary>
        /// 正在加载。
        /// </summary>
        Loading = 1,

        /// <summary>
        /// 已加载。
        /// </summary>
        Loaded = 2,

        /// <summary>
        /// 正在卸载。
        /// </summary>
        Unloading = 3,

        /// <summary>
        /// 已卸载。
        /// </summary>
        Unloaded = 4
    }
}
