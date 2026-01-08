#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 编辑器桥接 - 直接订阅 EasyEventEditorHook 并转发到 EditorDataBridge
    /// 不经过 EasyEventDebugger 的回调链，避免污染全局状态
    /// </summary>
    [InitializeOnLoad]
    public static class EventKitEditorBridge
    {
        // 保存原始 Hook，支持链式调用
        private static System.Action<System.Delegate> sOriginalOnRegister;
        private static System.Action<System.Delegate> sOriginalOnUnRegister;
        private static System.Action<string, string, object> sOriginalOnSend;
        
        private static bool sIsHooked;

        static EventKitEditorBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            // 编辑器加载时立即安装 Hook（在 EasyEventDebugger 之后）
            EditorApplication.delayCall += InstallHooks;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    // PlayMode 进入时重新安装 Hook（确保在运行时代码之前）
                    sIsHooked = false;
                    InstallHooks();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    // Hook 保持安装，不需要卸载
                    break;
            }
        }

        /// <summary>
        /// 安装 Hook - 包装原有 Hook 实现链式调用
        /// </summary>
        private static void InstallHooks()
        {
            if (sIsHooked) return;
            sIsHooked = true;

            // 保存原始 Hook（EasyEventDebugger 设置的）
            sOriginalOnRegister = EasyEventEditorHook.OnRegister;
            sOriginalOnUnRegister = EasyEventEditorHook.OnUnRegister;
            sOriginalOnSend = EasyEventEditorHook.OnSend;

            // 包装 Hook，先调用原始逻辑，再通知 EditorDataBridge
            EasyEventEditorHook.OnRegister = del =>
            {
                sOriginalOnRegister?.Invoke(del);
                OnEventRegistered(del);
            };

            EasyEventEditorHook.OnUnRegister = del =>
            {
                sOriginalOnUnRegister?.Invoke(del);
                OnEventUnregistered(del);
            };

            EasyEventEditorHook.OnSend = (eventType, eventKey, args) =>
            {
                sOriginalOnSend?.Invoke(eventType, eventKey, args);
                OnEventTriggered(eventType, eventKey, args);
            };
        }

        #region 事件处理

        private static void OnEventTriggered(string eventType, string eventKey, object args)
        {
            // 直接通知 EditorDataBridge，不经过 EasyEventDebugger 回调
            var argsStr = args?.ToString();
            EditorDataBridge.NotifyDataChanged(DataChannels.EVENT_TRIGGERED, (eventType, eventKey, argsStr));
        }

        private static void OnEventRegistered(System.Delegate del)
        {
            if (del == null) return;
            var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
            var methodName = del.Method?.Name ?? "Unknown";
            EditorDataBridge.NotifyDataChanged(DataChannels.EVENT_REGISTERED, ("Listener", $"{targetType}.{methodName}"));
        }

        private static void OnEventUnregistered(System.Delegate del)
        {
            if (del == null) return;
            var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
            var methodName = del.Method?.Name ?? "Unknown";
            EditorDataBridge.NotifyDataChanged(DataChannels.EVENT_UNREGISTERED, ("Listener", $"{targetType}.{methodName}"));
        }

        #endregion
    }
}
#endif
