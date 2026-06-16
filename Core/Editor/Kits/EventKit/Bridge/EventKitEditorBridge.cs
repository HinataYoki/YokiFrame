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
            /// 退出 PlayMode 时卸载注册 Hook，避免关闭 Domain Reload 时残留订阅累积。
            /// </summary>
            protected override void OnExitingPlayModeCore()
            {
                UninstallRegisterHooks();
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
            /// <remarks>
            /// 采用多播订阅（先 -= 再 +=）而非 "快照旧值 → 设为自身" 的链式包装，
            /// 关闭 Domain Reload 时多次重入不会叠加包装层或形成委托环。
            /// </remarks>
            private void InstallRegisterHooks()
            {
                if (mRegisterHooksInstalled)
                {
                    return;
                }

                mRegisterHooksInstalled = true;

                EasyEventEditorHook.OnRegister -= OnEventRegistered;
                EasyEventEditorHook.OnRegister += OnEventRegistered;

                EasyEventEditorHook.OnUnRegister -= OnEventUnregistered;
                EasyEventEditorHook.OnUnRegister += OnEventUnregistered;
            }

            /// <summary>
            /// 卸载监听器注册与注销 Hook。
            /// </summary>
            private void UninstallRegisterHooks()
            {
                if (!mRegisterHooksInstalled)
                {
                    return;
                }

                mRegisterHooksInstalled = false;

                EasyEventEditorHook.OnRegister -= OnEventRegistered;
                EasyEventEditorHook.OnUnRegister -= OnEventUnregistered;
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
