#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 的编辑器桥接入口。
    /// 负责把运行时事件触发、注册和注销行为同步到 <see cref="EditorDataBridge"/>。
    /// </summary>
    [InitializeOnLoad]
    public static class EventKitEditorBridge
    {
        private static readonly Bridge sBridge;

        static EventKitEditorBridge()
        {
            sBridge = new Bridge();
        }

        /// <summary>
        /// EventKit 的具体桥接实现。
        /// </summary>
        private sealed class Bridge : EasyEventSendHookBridgeBase
        {
            private System.Action<System.Delegate> mOriginalOnRegister;
            private System.Action<System.Delegate> mOriginalOnUnRegister;
            private bool mRegisterHooksInstalled;

            /// <summary>
            /// 创建后延迟安装注册与注销 Hook。
            /// </summary>
            protected override void OnCreated()
            {
                base.OnCreated();
                ScheduleDelayedAction(InstallRegisterHooks);
            }

            /// <summary>
            /// 进入 PlayMode 后重新安装 Hook，确保挂钩有效。
            /// </summary>
            protected override void OnEnteredPlayMode()
            {
                base.OnEnteredPlayMode();
                mRegisterHooksInstalled = false;
                ScheduleDelayedAction(InstallRegisterHooks);
            }

            /// <summary>
            /// 将运行时触发事件转发为编辑器数据通道消息。
            /// </summary>
            protected override void HandleEvent(string eventType, string eventKey, object args)
            {
                var argsStr = args?.ToString();
                EditorDataBridge.NotifyDataChanged(DataChannels.EVENT_TRIGGERED, (eventType, eventKey, argsStr));
            }

            /// <summary>
            /// 安装监听器注册与注销 Hook。
            /// </summary>
            private void InstallRegisterHooks()
            {
                if (mRegisterHooksInstalled)
                {
                    return;
                }

                mRegisterHooksInstalled = true;
                mOriginalOnRegister = EasyEventEditorHook.OnRegister;
                mOriginalOnUnRegister = EasyEventEditorHook.OnUnRegister;

                EasyEventEditorHook.OnRegister = del =>
                {
                    mOriginalOnRegister?.Invoke(del);
                    OnEventRegistered(del);
                };

                EasyEventEditorHook.OnUnRegister = del =>
                {
                    mOriginalOnUnRegister?.Invoke(del);
                    OnEventUnregistered(del);
                };
            }

            /// <summary>
            /// 推送监听器注册事件。
            /// </summary>
            private static void OnEventRegistered(System.Delegate del)
            {
                if (del == null)
                {
                    return;
                }

                var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
                var methodName = del.Method?.Name ?? "Unknown";
                EditorDataBridge.NotifyDataChanged(DataChannels.EVENT_REGISTERED, ("Listener", $"{targetType}.{methodName}"));
            }

            /// <summary>
            /// 推送监听器注销事件。
            /// </summary>
            private static void OnEventUnregistered(System.Delegate del)
            {
                if (del == null)
                {
                    return;
                }

                var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
                var methodName = del.Method?.Name ?? "Unknown";
                EditorDataBridge.NotifyDataChanged(DataChannels.EVENT_UNREGISTERED, ("Listener", $"{targetType}.{methodName}"));
            }
        }
    }
}
#endif
