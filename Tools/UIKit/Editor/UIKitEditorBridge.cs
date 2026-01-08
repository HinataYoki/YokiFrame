#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 编辑器桥接 - 通过 Hook 监听运行时事件并转发到 EditorDataBridge
    /// 不使用 EventKit.Type.Register，避免污染运行时监控面板
    /// </summary>
    [InitializeOnLoad]
    public static class UIKitEditorBridge
    {
        // 保存原始 Hook，支持链式调用
        private static System.Action<string, string, object> sOriginalOnSend;
        private static bool sIsHooked;

        static UIKitEditorBridge()
        {
            // 延迟安装 Hook，确保在 EasyEventDebugger 和 EventKitEditorBridge 之后
            EditorApplication.delayCall += () => EditorApplication.delayCall += InstallHook;
        }

        /// <summary>
        /// 安装 Hook - 包装原有 Hook 实现链式调用
        /// </summary>
        private static void InstallHook()
        {
            if (sIsHooked) return;
            sIsHooked = true;

            // 保存当前 Hook（可能是 EventKitEditorBridge 设置的）
            sOriginalOnSend = EasyEventEditorHook.OnSend;

            // 包装 Hook
            EasyEventEditorHook.OnSend = (eventType, eventKey, args) =>
            {
                // 先调用原始逻辑
                sOriginalOnSend?.Invoke(eventType, eventKey, args);
                
                // 处理 UIKit 相关事件
                if (eventType == "Type")
                {
                    HandleUIKitEvent(eventKey, args);
                }
            };
        }

        /// <summary>
        /// 处理 UIKit 相关事件
        /// </summary>
        private static void HandleUIKitEvent(string eventKey, object args)
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
    }
}
#endif
