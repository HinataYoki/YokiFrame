#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 的 Editor 监控桥接。
    /// 通过 <see cref="EasyEventEditorHook"/> 捕获 Buff 相关事件，并同步到 <see cref="EditorDataBridge"/>。
    /// </summary>
    [InitializeOnLoad]
    public static class BuffKitEditorBridge
    {
        private static readonly Bridge sBridge;

        static BuffKitEditorBridge()
        {
            sBridge = new Bridge();
        }

        /// <summary>
        /// BuffKit 实际桥接实现。
        /// </summary>
        private sealed class Bridge : EasyEventSendHookBridgeBase
        {
            /// <summary>
            /// 处理 BuffKit 运行时事件，并转发为 Editor 监控消息。
            /// </summary>
            protected override void HandleEvent(string eventType, string eventKey, object args)
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
}
#endif
