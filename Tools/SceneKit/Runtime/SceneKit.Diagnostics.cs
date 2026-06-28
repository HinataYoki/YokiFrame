using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit 诊断快照入口。
    /// </summary>
    public static partial class SceneKit
    {
        internal static void SetTransitioning(bool value)
        {
            if (sIsTransitioning == value)
                return;

            sIsTransitioning = value;
            BumpDiagnosticVersion();
        }

        internal static SceneKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            var scenes = new List<SceneKitSceneDiagnosticsSnapshot>(sLoadedScenes.Count);
            for (int i = 0; i < sLoadedScenes.Count; i++)
            {
                SceneHandler handler = sLoadedScenes[i];
                if (handler == null)
                    continue;

                scenes.Add(new SceneKitSceneDiagnosticsSnapshot(
                    handler.SceneName,
                    handler.BuildIndex,
                    handler.State,
                    handler.Progress,
                    handler.IsSuspended,
                    handler.IsPreloaded,
                    handler.LoadMode,
                    handler.Scene.IsValid,
                    handler.SceneData != null ? handler.SceneData.GetType().Name : string.Empty,
                    ReferenceEquals(handler, sActiveSceneHandler)));
            }

            // 诊断快照只由 CommandBridge/Tauri 读取，不在场景加载热路径写文件。
            return new SceneKitDiagnosticsSnapshot(
                sBackend != null ? sBackend.BackendName : string.Empty,
                sBackend != null ? sBackend.GetType().Name : string.Empty,
                sActiveSceneHandler != null ? sActiveSceneHandler.SceneName : string.Empty,
                sIsTransitioning,
                scenes);
        }
    }
}
