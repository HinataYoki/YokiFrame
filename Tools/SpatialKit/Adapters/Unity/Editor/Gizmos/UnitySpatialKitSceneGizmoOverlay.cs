#if UNITY_EDITOR
using System;
using global::YokiFrame;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity
{
    /// <summary>把 SpatialKit Gizmo 开关和索引筛选放入 Unity Scene View Toolbar。</summary>
    [Overlay(typeof(SceneView), UnitySpatialKitGizmoState.OVERLAY_ID, "SpatialKit", false)]
    internal sealed class UnitySpatialKitSceneGizmoOverlay : ToolbarOverlay
    {
        /// <summary>创建包含开关和索引下拉框的 Scene View Overlay。</summary>
        public UnitySpatialKitSceneGizmoOverlay()
            : base(UnitySpatialKitGizmoToggle.ID, UnitySpatialKitGizmoIndexDropdown.ID)
        {
        }
    }

    /// <summary>控制 SpatialKit Scene Gizmo 是否启用。</summary>
    [EditorToolbarElement(ID, typeof(SceneView))]
    internal sealed class UnitySpatialKitGizmoToggle : EditorToolbarToggle
    {
        internal const string ID = "YokiFrame/SpatialKit/GizmoToggle";

        /// <summary>初始化 Toggle 文案、提示和 Editor 会话状态绑定。</summary>
        public UnitySpatialKitGizmoToggle()
        {
            text = "Spatial";
            tooltip = "显示 SpatialKit 空间索引节点与实体位置";
            SetValueWithoutNotify(UnitySpatialKitGizmoState.Enabled);
            RegisterCallback<ChangeEvent<bool>>(OnValueChanged);
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        /// <summary>把 Toolbar 交互写入当前 Editor 会话开关。</summary>
        private static void OnValueChanged(ChangeEvent<bool> changeEvent)
        {
            UnitySpatialKitGizmoState.Enabled = changeEvent.newValue;
        }

        /// <summary>元素进入面板时订阅外部菜单状态变化。</summary>
        private void OnAttached(AttachToPanelEvent _)
        {
            UnitySpatialKitGizmoState.Changed -= RefreshState;
            UnitySpatialKitGizmoState.Changed += RefreshState;
        }

        /// <summary>元素离开面板时解除静态事件订阅。</summary>
        private void OnDetached(DetachFromPanelEvent _)
        {
            UnitySpatialKitGizmoState.Changed -= RefreshState;
        }

        /// <summary>同步菜单或其它 Toolbar 实例产生的开关变化。</summary>
        private void RefreshState()
        {
            SetValueWithoutNotify(UnitySpatialKitGizmoState.Enabled);
        }
    }

    /// <summary>提供“全部索引”或单 diagnosticsId 的 Scene Gizmo 筛选。</summary>
    [EditorToolbarElement(ID, typeof(SceneView))]
    internal sealed class UnitySpatialKitGizmoIndexDropdown : EditorToolbarDropdown
    {
        internal const string ID = "YokiFrame/SpatialKit/GizmoIndexDropdown";

        /// <summary>初始化索引筛选文案与动态菜单入口。</summary>
        public UnitySpatialKitGizmoIndexDropdown()
        {
            tooltip = "选择需要绘制的 SpatialKit 索引";
            clicked += ShowIndexMenu;
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
            RefreshText();
        }

        /// <summary>使用当前 Gizmo 快照构造索引筛选菜单。</summary>
        private void ShowIndexMenu()
        {
            SpatialGizmoDiagnosticsFrame frame = UnitySpatialKitSceneGizmoDrawer.GetCurrentFrame();
            string selectedId = UnitySpatialKitGizmoState.SelectedDiagnosticsId;
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("全部索引"),
                string.IsNullOrEmpty(selectedId),
                () => UnitySpatialKitGizmoState.SelectIndex(string.Empty));
            if (frame != null)
            {
                AddIndexItems(menu, frame, selectedId);
            }

            menu.ShowAsContext();
        }

        /// <summary>把当前存活索引追加为 diagnosticsId 筛选项。</summary>
        private static void AddIndexItems(
            GenericMenu menu,
            SpatialGizmoDiagnosticsFrame frame,
            string selectedId)
        {
            for (int index = 0; index < frame.Indexes.Count; index++)
            {
                SpatialGizmoIndexSnapshot snapshot = frame.Indexes[index];
                string diagnosticsId = snapshot.DiagnosticsId;
                string label = snapshot.IndexKind + "/" + diagnosticsId;
                menu.AddItem(
                    new GUIContent(label),
                    string.Equals(selectedId, diagnosticsId, StringComparison.Ordinal),
                    () => UnitySpatialKitGizmoState.SelectIndex(diagnosticsId));
            }
        }

        /// <summary>元素进入面板时订阅筛选变化。</summary>
        private void OnAttached(AttachToPanelEvent _)
        {
            UnitySpatialKitGizmoState.Changed -= RefreshText;
            UnitySpatialKitGizmoState.Changed += RefreshText;
        }

        /// <summary>元素离开面板时解除静态事件订阅。</summary>
        private void OnDetached(DetachFromPanelEvent _)
        {
            UnitySpatialKitGizmoState.Changed -= RefreshText;
        }

        /// <summary>根据当前 diagnosticsId 更新紧凑筛选文案。</summary>
        private void RefreshText()
        {
            string selectedId = UnitySpatialKitGizmoState.SelectedDiagnosticsId;
            text = string.IsNullOrEmpty(selectedId) ? "全部索引" : selectedId;
        }
    }
}
#endif
