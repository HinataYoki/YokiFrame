namespace YokiFrame
{
    /// <summary>
    /// 表示一次 ResKit 场景加载请求。
    /// </summary>
    public struct ResSceneLoadRequest
    {
        public readonly string SceneName;
        public readonly int BuildIndex;
        public readonly ResSceneLoadMode Mode;
        public readonly float SuspendAtProgress;
        public readonly object Data;
        public readonly bool IsPreload;

        public ResSceneLoadRequest(
            string sceneName,
            int buildIndex,
            ResSceneLoadMode mode,
            float suspendAtProgress,
            object data,
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
