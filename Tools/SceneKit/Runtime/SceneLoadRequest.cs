namespace YokiFrame
{
    /// <summary>
    /// 表示一次场景加载请求。
    /// </summary>
    public struct SceneLoadRequest
    {
        /// <summary>
        /// 场景名称。
        /// </summary>
        public readonly string SceneName;

        /// <summary>
        /// 构建索引。
        /// </summary>
        public readonly int BuildIndex;

        /// <summary>
        /// 加载模式。
        /// </summary>
        public readonly SceneLoadMode Mode;

        /// <summary>
        /// 挂起加载的进度阈值。
        /// </summary>
        public readonly float SuspendAtProgress;

        /// <summary>
        /// 场景附加数据。
        /// </summary>
        public readonly ISceneData Data;

        /// <summary>
        /// 是否为预加载请求。
        /// </summary>
        public readonly bool IsPreload;

        /// <summary>
        /// 创建场景加载请求。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <param name="buildIndex">构建索引。</param>
        /// <param name="mode">加载模式。</param>
        /// <param name="suspendAtProgress">挂起加载的进度阈值。</param>
        /// <param name="data">场景附加数据。</param>
        /// <param name="isPreload">是否为预加载请求。</param>
        public SceneLoadRequest(
            string sceneName,
            int buildIndex,
            SceneLoadMode mode,
            float suspendAtProgress,
            ISceneData data,
            bool isPreload)
        {
            SceneName = sceneName;
            BuildIndex = buildIndex;
            Mode = mode;
            SuspendAtProgress = suspendAtProgress;
            Data = data;
            IsPreload = isPreload;
        }
    }
}
