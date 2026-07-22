namespace YokiFrame
{
    /// <summary>表示一次 SceneKit 场景加载请求。</summary>
    public readonly struct SceneLoadRequest
    {
        /// <summary>创建场景加载请求。</summary>
        public SceneLoadRequest(
            string sceneName,
            int buildIndex,
            SceneLoadMode mode,
            float suspendAtProgress,
            ISceneData data,
            bool isPreload)
        {
            SceneName = sceneName ?? string.Empty;
            BuildIndex = buildIndex;
            Mode = mode;
            SuspendAtProgress = suspendAtProgress;
            Data = data;
            IsPreload = isPreload;
        }

        /// <summary>获取场景名称或 Provider 路径。</summary>
        public string SceneName { get; }

        /// <summary>获取场景构建索引。</summary>
        public int BuildIndex { get; }

        /// <summary>获取加载模式。</summary>
        public SceneLoadMode Mode { get; }

        /// <summary>获取挂起进度阈值。</summary>
        public float SuspendAtProgress { get; }

        /// <summary>获取场景附加数据。</summary>
        public ISceneData Data { get; }

        /// <summary>获取是否为预加载请求。</summary>
        public bool IsPreload { get; }
    }
}
