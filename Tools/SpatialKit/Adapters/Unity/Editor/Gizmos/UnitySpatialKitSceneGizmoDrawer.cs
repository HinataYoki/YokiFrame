#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using global::YokiFrame;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>在 Unity Scene View 绘制 SpatialKit 有界只读几何快照。</summary>
    [InitializeOnLoad]
    internal static class UnitySpatialKitSceneGizmoDrawer
    {
        private const string MENU_PATH = "YokiFrame/SpatialKit/Open Overlay Menu";
        private const int MAX_NODES_PER_INDEX = 2048;
        private const int MAX_ENTITIES_PER_INDEX = 4096;
        private const double REFRESH_INTERVAL_SECONDS = 0.2d;
        private const float ENTITY_SIZE_SCALE = 0.045f;
        private const float LABEL_OFFSET = 0.4f;
        private static readonly Color sEntityColor = new(1.0f, 0.82f, 0.22f, 0.95f);
        private static SpatialGizmoDiagnosticsFrame sFrame;
        private static double sNextRefreshTime;
        private static readonly Dictionary<string, string> sIndexLabels = new Dictionary<string, string>();

        /// <summary>注册 Scene View 绘制回调与 Gizmo 状态变化监听。</summary>
        static UnitySpatialKitSceneGizmoDrawer()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            UnitySpatialKitGizmoState.Changed -= OnStateChanged;
            UnitySpatialKitGizmoState.Changed += OnStateChanged;
            EditorApplication.update -= HideOverlayAfterReload;
            EditorApplication.update += HideOverlayAfterReload;
        }

        /// <summary>显示并展开当前 Scene View 中默认隐藏的 SpatialKit Overlay。</summary>
        [MenuItem(MENU_PATH, false, 200)]
        private static void OpenOverlayMenu()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = EditorWindow.GetWindow<SceneView>();
            }

            if (!sceneView.TryGetOverlay(UnitySpatialKitGizmoState.OVERLAY_ID, out Overlay overlay))
            {
                Debug.LogWarning("SpatialKit Overlay 尚未注册，请等待 Unity 完成脚本编译后重试。");
                return;
            }

            overlay.displayed = true;
            overlay.collapsed = false;
            sceneView.Focus();
            sceneView.Repaint();
        }

        /// <summary>等待 Unity 恢复 Scene View 布局后隐藏全部 SpatialKit Overlay，并随即解除轮询。</summary>
        private static void HideOverlayAfterReload()
        {
            SceneView[] sceneViews = Resources.FindObjectsOfTypeAll<SceneView>();
            bool foundOverlay = false;
            for (int index = 0; index < sceneViews.Length; index++)
            {
                SceneView sceneView = sceneViews[index];
                if (sceneView == null
                    || !sceneView.TryGetOverlay(UnitySpatialKitGizmoState.OVERLAY_ID, out Overlay overlay))
                {
                    continue;
                }

                overlay.displayed = false;
                foundOverlay = true;
            }

            if (foundOverlay)
            {
                EditorApplication.update -= HideOverlayAfterReload;
            }
        }

        /// <summary>为 Toolbar 下拉菜单返回最新索引快照。</summary>
        /// <returns>当前有界 Gizmo 诊断帧。</returns>
        internal static SpatialGizmoDiagnosticsFrame GetCurrentFrame()
        {
            RefreshFrame(true);
            return sFrame;
        }

        /// <summary>在 Scene View Repaint 阶段刷新并绘制启用的索引。</summary>
        /// <param name="sceneView">当前请求绘制的 Scene View。</param>
        private static void OnSceneGui(SceneView sceneView)
        {
            if (!UnitySpatialKitGizmoState.Enabled || Event.current.type != EventType.Repaint)
            {
                return;
            }

            RefreshFrame(false);
            if (sFrame == null)
            {
                return;
            }

            DrawFrame(sFrame);
        }

        /// <summary>按固定间隔和诊断版本重建有界快照。</summary>
        /// <param name="force">是否忽略刷新间隔立即采样。</param>
        private static void RefreshFrame(bool force)
        {
            double now = EditorApplication.timeSinceStartup;
            long version = SpatialKit.GetDiagnosticsVersion();
            if (!force && sFrame != null && sFrame.Version == version)
            {
                return;
            }

            if (!force && now < sNextRefreshTime)
            {
                return;
            }

            sFrame = SpatialKit.CreateGizmoDiagnosticsFrame(MAX_NODES_PER_INDEX, MAX_ENTITIES_PER_INDEX);
            sNextRefreshTime = now + REFRESH_INTERVAL_SECONDS;
            ValidateSelectedIndex();
            RebuildLabelCache();
        }

        /// <summary>当已选索引结束生命周期时恢复"全部索引"，避免空白筛选。</summary>
        private static void ValidateSelectedIndex()
        {
            string selectedId = UnitySpatialKitGizmoState.SelectedDiagnosticsId;
            if (string.IsNullOrEmpty(selectedId) || sFrame == null)
            {
                return;
            }

            for (int index = 0; index < sFrame.Indexes.Count; index++)
            {
                if (string.Equals(sFrame.Indexes[index].DiagnosticsId, selectedId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            UnitySpatialKitGizmoState.SelectIndex(string.Empty);
        }

        /// <summary>在帧刷新时预构建标签文本缓存，避免 Repaint 路径上的字符串分配。</summary>
        private static void RebuildLabelCache()
        {
            sIndexLabels.Clear();
            if (sFrame == null) return;
            for (int i = 0; i < sFrame.Indexes.Count; i++)
            {
                SpatialGizmoIndexSnapshot s = sFrame.Indexes[i];
                string suffix = s.NodesTruncated || s.EntitiesTruncated
                    ? " · truncated" : string.Empty;
                sIndexLabels[s.DiagnosticsId] =
                    s.IndexKind + " · " + s.DiagnosticsId + suffix;
            }
        }

        /// <summary>按当前 diagnosticsId 筛选绘制全部或单个索引。</summary>
        /// <param name="frame">待绘制的有界诊断帧。</param>
        private static void DrawFrame(SpatialGizmoDiagnosticsFrame frame)
        {
            string selectedId = UnitySpatialKitGizmoState.SelectedDiagnosticsId;
            int visibleIndex = 0;
            for (int index = 0; index < frame.Indexes.Count; index++)
            {
                SpatialGizmoIndexSnapshot snapshot = frame.Indexes[index];
                if (!string.IsNullOrEmpty(selectedId)
                    && !string.Equals(snapshot.DiagnosticsId, selectedId, StringComparison.Ordinal))
                {
                    continue;
                }

                DrawIndex(snapshot, visibleIndex++);
            }
        }

        /// <summary>绘制单个索引的节点、实体点和身份标签。</summary>
        /// <param name="snapshot">单索引几何快照。</param>
        /// <param name="visibleIndex">当前可见索引序号，用于错开标签。</param>
        private static void DrawIndex(SpatialGizmoIndexSnapshot snapshot, int visibleIndex)
        {
            for (int index = 0; index < snapshot.Nodes.Count; index++)
            {
                DrawNode(snapshot.Nodes[index]);
            }

            Handles.color = sEntityColor;
            for (int index = 0; index < snapshot.Entities.Count; index++)
            {
                DrawEntity(snapshot, snapshot.Entities[index]);
            }

            DrawIndexLabel(snapshot, visibleIndex);
        }

        /// <summary>根据节点维度选择三维 AABB 或二维投影矩形。</summary>
        /// <param name="node">待绘制节点。</param>
        private static void DrawNode(SpatialGizmoNodeSnapshot node)
        {
            Handles.color = ResolveNodeColor(node);
            if (node.IsVolume)
            {
                Handles.DrawWireCube(ToUnity(node.Bounds3D.Center), ToUnity(node.Bounds3D.Size));
                return;
            }

            DrawPlanarRect(node.Bounds2D, node.Plane);
        }

        /// <summary>使用四条无分配线段绘制 XY 或 XZ 投影矩形。</summary>
        /// <param name="bounds">二维节点边界。</param>
        /// <param name="plane">节点投影平面。</param>
        private static void DrawPlanarRect(YokiRect bounds, SpatialPlane plane)
        {
            Vector3 bottomLeft = ToUnity(bounds.XMin, bounds.YMin, plane);
            Vector3 bottomRight = ToUnity(bounds.XMax, bounds.YMin, plane);
            Vector3 topRight = ToUnity(bounds.XMax, bounds.YMax, plane);
            Vector3 topLeft = ToUnity(bounds.XMin, bounds.YMax, plane);
            Handles.DrawLine(bottomLeft, bottomRight);
            Handles.DrawLine(bottomRight, topRight);
            Handles.DrawLine(topRight, topLeft);
            Handles.DrawLine(topLeft, bottomLeft);
        }

        /// <summary>绘制三维实体位置或二维索引中的投影位置。</summary>
        /// <param name="snapshot">实体所属索引快照。</param>
        /// <param name="entity">实体位置快照。</param>
        private static void DrawEntity(
            SpatialGizmoIndexSnapshot snapshot,
            SpatialGizmoEntitySnapshot entity)
        {
            Vector3 position = snapshot.IsVolume
                ? ToUnity(entity.Position)
                : ProjectToUnity(entity.Position, snapshot.Plane);
            float size = HandleUtility.GetHandleSize(position) * ENTITY_SIZE_SCALE;
            Handles.DotHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
        }

        /// <summary>在首个节点附近绘制索引身份和预算裁剪提示。</summary>
        /// <param name="snapshot">待标记索引。</param>
        /// <param name="visibleIndex">当前可见索引序号。</param>
        private static void DrawIndexLabel(SpatialGizmoIndexSnapshot snapshot, int visibleIndex)
        {
            if (snapshot.Nodes.Count == 0)
            {
                return;
            }

            Vector3 position = ResolveLabelPosition(snapshot.Nodes[0]);
            position += Vector3.up * (LABEL_OFFSET * (visibleIndex + 1));
            if (!sIndexLabels.TryGetValue(snapshot.DiagnosticsId, out string label))
                label = snapshot.DiagnosticsId;
            Handles.Label(position, label);
        }

        /// <summary>根据节点深度、叶状态和占用生成稳定颜色。</summary>
        /// <param name="node">待着色节点。</param>
        /// <returns>适用于线框的半透明颜色。</returns>
        private static Color ResolveNodeColor(SpatialGizmoNodeSnapshot node)
        {
            float hue = Mathf.Repeat(0.52f + node.Depth * 0.075f, 1.0f);
            float value = node.EntityCount > 0 ? 1.0f : 0.78f;
            Color color = Color.HSVToRGB(hue, 0.72f, value);
            color.a = node.IsLeaf ? 0.78f : 0.32f;
            return color;
        }

        /// <summary>把跨引擎三维向量转换为 Unity Vector3。</summary>
        private static Vector3 ToUnity(YokiVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        /// <summary>把二维投影坐标放置到 Unity 原点平面。</summary>
        private static Vector3 ToUnity(float coordinateA, float coordinateB, SpatialPlane plane)
        {
            return plane == SpatialPlane.XY
                ? new Vector3(coordinateA, coordinateB, 0.0f)
                : new Vector3(coordinateA, 0.0f, coordinateB);
        }

        /// <summary>把实体实际位置压到对应二维索引平面。</summary>
        private static Vector3 ProjectToUnity(YokiVector3 position, SpatialPlane plane)
        {
            return plane == SpatialPlane.XY
                ? new Vector3(position.X, position.Y, 0.0f)
                : new Vector3(position.X, 0.0f, position.Z);
        }

        /// <summary>获取索引首节点的标签基准位置。</summary>
        private static Vector3 ResolveLabelPosition(SpatialGizmoNodeSnapshot node)
        {
            if (node.IsVolume)
            {
                YokiBounds bounds = node.Bounds3D;
                return ToUnity(bounds.Center + new YokiVector3(0.0f, bounds.Extents.Y, 0.0f));
            }

            YokiRect rect = node.Bounds2D;
            return ToUnity(rect.Center.X, rect.YMax, node.Plane);
        }

        /// <summary>响应 Toolbar 状态变化并让下一帧立即刷新缓存。</summary>
        private static void OnStateChanged()
        {
            sNextRefreshTime = 0.0d;
        }
    }
}
#endif
