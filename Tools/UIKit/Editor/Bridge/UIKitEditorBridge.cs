#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 的编辑器桥接入口。
    /// 负责把运行时的面板和焦点事件转发到 EditorDataBridge，
    /// 并在 PlayMode 生命周期切换时清理 UIKit 编辑器残留状态。
    /// </summary>
    [InitializeOnLoad]
    public static class UIKitEditorBridge
    {
        private static readonly Bridge sBridge;

        static UIKitEditorBridge()
        {
            sBridge = new Bridge();
        }

        /// <summary>
        /// UIKit 的具体桥接实现。
        /// </summary>
        private sealed class Bridge : EasyEventSendHookBridgeBase
        {
            /// <summary>
            /// 进入 PlayMode 后重置退出标记，避免反复进出产生 UIKit 静态状态残留。
            /// </summary>
            protected override void OnEnteredPlayMode()
            {
                base.OnEnteredPlayMode();
                ResetQuitFlag();
            }

            /// <summary>
            /// 退出 PlayMode 前停止动画并销毁临时 UIRoot，避免编辑器侧残留运行时对象。
            /// </summary>
            protected override void OnExitingPlayMode()
            {
                ForceStopAllUIAnimations();
                CleanupUIKit();
            }

            /// <summary>
            /// 处理 UIKit 运行时事件，并转发为编辑器监控消息。
            /// </summary>
            protected override void HandleEvent(string eventType, string eventKey, object args)
            {
                switch (eventKey)
                {
                    case nameof(PanelDidShowEvent):
                        if (args is PanelDidShowEvent showEvt)
                        {
                            EditorDataBridge.NotifyDataChanged(DataChannels.PANEL_OPENED, showEvt.Panel);
                            EditorDataBridge.NotifyDataChanged(DataChannels.PANEL_STATE_CHANGED, (showEvt.Panel, PanelState.Open));
                        }
                        break;

                    case nameof(PanelDidHideEvent):
                        if (args is PanelDidHideEvent hideEvt)
                        {
                            EditorDataBridge.NotifyDataChanged(DataChannels.PANEL_CLOSED, hideEvt.Panel);
                            EditorDataBridge.NotifyDataChanged(DataChannels.PANEL_STATE_CHANGED, (hideEvt.Panel, hideEvt.Panel.State));
                        }
                        break;

                    case nameof(UIFocusChangedEvent):
                        if (args is UIFocusChangedEvent focusEvt)
                        {
                            var focusObj = focusEvt.Current != null ? focusEvt.Current.gameObject : null;
                            EditorDataBridge.NotifyDataChanged(DataChannels.FOCUS_CHANGED, focusObj);
                        }
                        break;
                }
            }

            /// <summary>
            /// 清理 UIRoot 内部退出标记，确保下次进入 PlayMode 时可正常初始化。
            /// </summary>
            private static void ResetQuitFlag()
            {
                var field = typeof(UIRoot).GetField(
                    "sIsQuitting",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                field?.SetValue(null, false);
            }

            /// <summary>
            /// 停止当前场景中所有 UIPanel 的动画协程，避免退出 PlayMode 时残留异步流程。
            /// </summary>
            private static void ForceStopAllUIAnimations()
            {
#if YOKIFRAME_UNITASK_SUPPORT
                var panels = UnityEngine.Object.FindObjectsByType<UIPanel>(UnityEngine.FindObjectsSortMode.None);
                foreach (var panel in panels)
                {
                    if (panel == default) continue;

                    var field = typeof(UIPanel).GetField(
                        "mIsDestroying",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(panel, true);
                    panel.StopAllCoroutines();
                }
#endif
            }

            /// <summary>
            /// 清理 PlayMode 中创建的 UIRoot，避免编辑器层级出现遗留对象。
            /// </summary>
            private static void CleanupUIKit()
            {
                var uikit = UnityEngine.Object.FindFirstObjectByType<UIRoot>();
                if (uikit != default && uikit.gameObject != default)
                {
                    UnityEngine.Object.DestroyImmediate(uikit.gameObject);
                }
            }
        }
    }
}
#endif
