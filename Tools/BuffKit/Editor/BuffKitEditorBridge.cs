#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 编辑器桥接 - 通过 Hook 监听运行时事件并转发到 EditorDataBridge
    /// 不使用 EventKit.Type.Register，避免污染运行时监控面板
    /// </summary>
    [InitializeOnLoad]
    public static class BuffKitEditorBridge
    {
        // 保存原始 Hook，支持链式调用
        private static System.Action<string, string, object> sOriginalOnSend;
        private static bool sIsHooked;

        static BuffKitEditorBridge()
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

            // 保存当前 Hook（可能是其他桥接设置的）
            sOriginalOnSend = EasyEventEditorHook.OnSend;

            // 包装 Hook
            EasyEventEditorHook.OnSend = (eventType, eventKey, args) =>
            {
                // 先调用原始逻辑
                sOriginalOnSend?.Invoke(eventType, eventKey, args);
                
                // 处理 BuffKit 相关事件
                if (eventType == "Type")
                {
                    HandleBuffKitEvent(eventKey, args);
                }
            };
        }

        /// <summary>
        /// 处理 BuffKit 相关事件
        /// </summary>
        private static void HandleBuffKitEvent(string eventKey, object args)
        {
            switch (eventKey)
            {
                case nameof(BuffAddedEvent):
                    if (args is BuffAddedEvent addEvt)
                    {
                        EditorDataBridge.NotifyDataChanged(DataChannels.BUFF_ADDED, addEvt);
                    }
                    break;
                    
                case nameof(BuffRemovedEvent):
                    if (args is BuffRemovedEvent removeEvt)
                    {
                        EditorDataBridge.NotifyDataChanged(DataChannels.BUFF_REMOVED, removeEvt);
                    }
                    break;
                    
                case nameof(BuffStackChangedEvent):
                    if (args is BuffStackChangedEvent stackEvt)
                    {
                        // 堆叠变化也通知 BUFF_ADDED 通道，触发 UI 刷新
                        EditorDataBridge.NotifyDataChanged(DataChannels.BUFF_ADDED, new BuffAddedEvent
                        {
                            Container = stackEvt.Container,
                            Instance = stackEvt.Instance
                        });
                    }
                    break;
            }
        }
    }
}
#endif
