namespace YokiFrame
{
    internal readonly struct SceneKitSceneDiagnosticsSnapshot
    {
        internal SceneKitSceneDiagnosticsSnapshot(
            string sceneName,
            int buildIndex,
            SceneState state,
            float progress,
            bool isSuspended,
            bool isPreloaded,
            SceneLoadMode loadMode,
            bool isValid,
            string dataType,
            bool isActive)
        {
            SceneName = sceneName ?? string.Empty;
            BuildIndex = buildIndex;
            State = state;
            Progress = progress;
            IsSuspended = isSuspended;
            IsPreloaded = isPreloaded;
            LoadMode = loadMode;
            IsValid = isValid;
            DataType = dataType ?? string.Empty;
            IsActive = isActive;
        }

        internal string SceneName { get; }

        internal int BuildIndex { get; }

        internal SceneState State { get; }

        internal float Progress { get; }

        internal bool IsSuspended { get; }

        internal bool IsPreloaded { get; }

        internal SceneLoadMode LoadMode { get; }

        internal bool IsValid { get; }

        internal string DataType { get; }

        internal bool IsActive { get; }
    }
}
