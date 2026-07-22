#if UNITY_EDITOR
using System;
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>保存当前 Unity Editor 会话中的 SpatialKit Gizmo 开关和索引筛选。</summary>
    internal static class UnitySpatialKitGizmoState
    {
        internal const string OVERLAY_ID = "YokiFrame/SpatialKit/SceneGizmoOverlay";
        private const string ENABLED_KEY = "YokiFrame.SpatialKit.Gizmos.Enabled";
        private const string SELECTED_INDEX_KEY = "YokiFrame.SpatialKit.Gizmos.SelectedIndex";

        /// <summary>在 Gizmo 开关或索引筛选变化时通知 Toolbar 与 Scene View。</summary>
        internal static event Action Changed;

        /// <summary>获取或设置当前 Editor 会话是否绘制 SpatialKit Gizmo。</summary>
        internal static bool Enabled
        {
            get { return SessionState.GetBool(ENABLED_KEY, false); }
            set
            {
                if (Enabled == value)
                {
                    return;
                }

                SessionState.SetBool(ENABLED_KEY, value);
                NotifyChanged();
            }
        }

        /// <summary>获取当前筛选的 diagnosticsId；空字符串表示显示全部索引。</summary>
        internal static string SelectedDiagnosticsId
        {
            get { return SessionState.GetString(SELECTED_INDEX_KEY, string.Empty); }
        }

        /// <summary>选择单个索引，传入空字符串时恢复显示全部。</summary>
        /// <param name="diagnosticsId">目标索引诊断编号。</param>
        internal static void SelectIndex(string diagnosticsId)
        {
            string normalized = diagnosticsId ?? string.Empty;
            if (string.Equals(SelectedDiagnosticsId, normalized, StringComparison.Ordinal))
            {
                return;
            }

            SessionState.SetString(SELECTED_INDEX_KEY, normalized);
            NotifyChanged();
        }

        /// <summary>刷新 Scene View 和已创建的 Toolbar 元素。</summary>
        private static void NotifyChanged()
        {
            SceneView.RepaintAll();
            Changed?.Invoke();
        }
    }
}
#endif
