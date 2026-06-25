using System.Collections.Generic;

namespace YokiFrame
{
    internal sealed class SceneKitDiagnosticsSnapshot
    {
        internal SceneKitDiagnosticsSnapshot(
            string backendName,
            string backendType,
            string activeSceneName,
            bool isTransitioning,
            List<SceneKitSceneDiagnosticsSnapshot> scenes)
        {
            BackendName = backendName ?? string.Empty;
            BackendType = backendType ?? string.Empty;
            ActiveSceneName = activeSceneName ?? string.Empty;
            IsTransitioning = isTransitioning;
            Scenes = scenes ?? new List<SceneKitSceneDiagnosticsSnapshot>();
        }

        internal string BackendName { get; }

        internal string BackendType { get; }

        internal string ActiveSceneName { get; }

        internal bool IsTransitioning { get; }

        internal List<SceneKitSceneDiagnosticsSnapshot> Scenes { get; }
    }
}
