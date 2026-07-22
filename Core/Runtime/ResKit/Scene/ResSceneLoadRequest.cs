namespace YokiFrame
{
    /// <summary>表示一次由 ResKit Provider 执行的场景加载请求。</summary>
    public readonly struct ResSceneLoadRequest
    {
        /// <summary>创建场景加载请求。</summary>
        public ResSceneLoadRequest(
            string sceneName,
            int buildIndex,
            ResSceneLoadMode mode,
            float suspendAtProgress,
            IResSceneData data,
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

        /// <summary>获取场景构建索引；按名称加载时为负数。</summary>
        public int BuildIndex { get; }

        /// <summary>获取加载模式。</summary>
        public ResSceneLoadMode Mode { get; }

        /// <summary>获取请求方希望触发挂起通知的进度阈值。</summary>
        public float SuspendAtProgress { get; }

        /// <summary>获取随请求传递的业务数据。</summary>
        public IResSceneData Data { get; }

        /// <summary>获取是否为预加载请求。</summary>
        public bool IsPreload { get; }
    }
}
