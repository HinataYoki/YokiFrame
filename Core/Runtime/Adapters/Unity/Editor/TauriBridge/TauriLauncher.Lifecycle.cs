#if !GODOT
using System;
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Tauri 启动器的 Unity Editor 生命周期维护逻辑。
    /// </summary>
    public static partial class TauriLauncher
    {
        private static DateTime sLastProcessCheck;
        private static DateTime sLastAssetRefresh;
        private const int PROCESS_CHECK_INTERVAL_SEC = 5;

        private static void OnEditorUpdate()
        {
            var nowUtc = DateTime.UtcNow;

            // 进程引用维护：窗口被用户关闭后只清理引用，不自动重启。
            if ((nowUtc - sLastProcessCheck).TotalSeconds >= PROCESS_CHECK_INTERVAL_SEC)
            {
                sLastProcessCheck = nowUtc;
                if (sTauriProcess is { HasExited: true })
                {
                    if (ShouldAutoRestartExitedProcess(processExited: true))
                    {
                        Close();
                        Launch();
                    }
                    else
                    {
                        DisposeProcessReference();
                    }
                }
            }

            if (ShouldRefreshAssetsForToolWindow(
                    sTauriProcess is { HasExited: false },
                    EditorApplication.isCompiling,
                    EditorApplication.isUpdating,
                    EditorApplication.isPlayingOrWillChangePlaymode,
                    EditorApplication.isPlaying,
                    IsUnityApplicationFocused(),
                    nowUtc,
                    sLastAssetRefresh))
            {
                sLastAssetRefresh = nowUtc;
                AssetDatabase.Refresh();
            }
        }

        private static bool IsUnityApplicationFocused()
        {
            return UnityEditorInternal.InternalEditorUtility.isApplicationActive;
        }

        private static void OnEditorQuitting()
        {
            Close();
        }
    }
}
#endif
