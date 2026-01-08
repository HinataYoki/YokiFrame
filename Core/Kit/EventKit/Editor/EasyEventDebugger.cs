using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>
    /// EasyEvent 调试器 - 仅编辑器使用
    /// </summary>
    public static class EasyEventDebugger
    {
        public struct ListenerDebugInfo
        {
            public string TargetType;
            public string MethodName;
            public string FilePath;
            public int LineNumber;
            public string StackTrace;
        }

        /// <summary>
        /// 事件历史记录
        /// </summary>
        public struct EventHistoryEntry
        {
            public float Time;
            public string Action;      // Register/UnRegister/Send
            public string EventType;   // Enum/Type/String
            public string EventKey;
            public string Args;
            public string CallerInfo;
        }

        private static readonly Dictionary<int, ListenerDebugInfo> sDebugInfoMap = new();
        private static readonly List<EventHistoryEntry> sEventHistory = new(512);
        
        // 字符串缓存，避免重复创建
        private static readonly Dictionary<string, string> sStringCache = new(128);
        private static readonly StringBuilder sStringBuilder = new(256);
        
        public const int MAX_HISTORY_COUNT = 500;

        public static IReadOnlyList<EventHistoryEntry> EventHistory => sEventHistory;
        
        /// <summary>
        /// 是否记录 Send 事件（高频操作，默认关闭以避免 GC）
        /// </summary>
        public static bool RecordSendEvents { get; set; } = false;
        
        /// <summary>
        /// Send 记录是否捕获堆栈（开启会产生大量 GC，仅调试时使用）
        /// </summary>
        public static bool RecordSendStackTrace { get; set; } = false;

        #region 编辑器回调（用于响应式更新）

        /// <summary>
        /// 事件触发回调（eventType, eventKey, args）
        /// </summary>
        public static Action<string, string, string> OnEventTriggered;

        /// <summary>
        /// 事件注册回调（eventType, eventKey）
        /// </summary>
        public static Action<string, string> OnEventRegistered;

        /// <summary>
        /// 事件注销回调（eventType, eventKey）
        /// </summary>
        public static Action<string, string> OnEventUnregistered;

        #endregion

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EasyEventEditorHook.OnRegister = OnRegister;
            EasyEventEditorHook.OnUnRegister = OnUnRegister;
            EasyEventEditorHook.OnSend = OnSend;

            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    ClearDebugInfo();
                    sStringCache.Clear();
                    
                    if (EditorPrefs.GetBool("EventKitViewer_ClearHistoryOnStop", true))
                        ClearHistory();
                }
            };
        }

        private static void OnRegister(Delegate del)
        {
            if (del == null) return;

            var key = del.GetHashCode();
            if (sDebugInfoMap.ContainsKey(key)) return;

            var info = new ListenerDebugInfo
            {
                TargetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown",
                MethodName = del.Method?.Name ?? "Unknown"
            };

            var st = new StackTrace(true);
            info.StackTrace = st.ToString();
            var callerInfo = "";

            for (var i = 0; i < st.FrameCount; i++)
            {
                var frame = st.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null) continue;

                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                var ns = declaringType.Namespace;
                if (ns != null && ns.StartsWith("YokiFrame")) continue;
                if (ns != null && (ns.StartsWith("System") || ns.StartsWith("Unity"))) continue;

                info.FilePath = frame.GetFileName();
                info.LineNumber = frame.GetFileLineNumber();

                if (!string.IsNullOrEmpty(info.FilePath))
                {
                    var idx = info.FilePath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                        info.FilePath = info.FilePath[idx..].Replace("\\", "/");
                    callerInfo = GetCachedString($"{info.FilePath}:{info.LineNumber}");
                }
                break;
            }

            sDebugInfoMap[key] = info;
            
            var eventKey = GetCachedString($"{info.TargetType}.{info.MethodName}");
            AddHistory("Register", "Listener", eventKey, null, callerInfo);
            
            // 触发编辑器回调
            OnEventRegistered?.Invoke("Listener", eventKey);
        }

        private static void OnUnRegister(Delegate del)
        {
            if (del == null) return;

            var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
            var methodName = del.Method?.Name ?? "Unknown";
            var eventKey = GetCachedString($"{targetType}.{methodName}");

            sDebugInfoMap.Remove(del.GetHashCode());
            AddHistory("UnRegister", "Listener", eventKey, null, GetCallerInfo());
            
            // 触发编辑器回调
            OnEventUnregistered?.Invoke("Listener", eventKey);
        }

        private static void OnSend(string eventType, string eventKey, object args)
        {
            // 触发编辑器回调（用于响应式更新）
            OnEventTriggered?.Invoke(eventType, eventKey, args?.ToString());
            
            if (!RecordSendEvents) return;
            
            // eventType 和 eventKey 已经是调用方传入的字符串，直接缓存
            var cachedType = GetCachedString(eventType);
            var cachedKey = GetCachedString(eventKey);
            
            // args 只在需要时转换，且限制长度
            string argsStr = null;
            if (args != null)
            {
                argsStr = args.ToString();
                if (argsStr.Length > 50)
                    argsStr = argsStr[..47] + "...";
            }

            // 只在需要时捕获堆栈
            var callerInfo = RecordSendStackTrace ? GetCallerInfo() : null;
            
            AddHistory("Send", cachedType, cachedKey, argsStr, callerInfo);
        }

        private static void AddHistory(string action, string eventType, string eventKey, string args, string callerInfo)
        {
            if (sEventHistory.Count >= MAX_HISTORY_COUNT)
                sEventHistory.RemoveAt(0);

            sEventHistory.Add(new EventHistoryEntry
            {
                Time = UnityEngine.Time.time,
                Action = action,
                EventType = eventType,
                EventKey = eventKey,
                Args = args,
                CallerInfo = callerInfo
            });
        }

        private static string GetCallerInfo()
        {
            var st = new StackTrace(true);
            for (var i = 0; i < st.FrameCount; i++)
            {
                var frame = st.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null) continue;

                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                var ns = declaringType.Namespace;
                if (ns != null && ns.StartsWith("YokiFrame")) continue;
                if (ns != null && (ns.StartsWith("System") || ns.StartsWith("Unity"))) continue;

                var filePath = frame.GetFileName();
                if (!string.IsNullOrEmpty(filePath))
                {
                    var idx = filePath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                        filePath = filePath[idx..].Replace("\\", "/");
                    return GetCachedString($"{filePath}:{frame.GetFileLineNumber()}");
                }
                break;
            }
            return null;
        }
        
        /// <summary>
        /// 字符串缓存，避免重复创建相同字符串
        /// </summary>
        private static string GetCachedString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            if (sStringCache.TryGetValue(str, out var cached))
                return cached;
            
            // 限制缓存大小
            if (sStringCache.Count > 1000)
                sStringCache.Clear();
                
            sStringCache[str] = str;
            return str;
        }

        public static bool TryGetDebugInfo(Delegate del, out ListenerDebugInfo info)
        {
            if (del != null && sDebugInfoMap.TryGetValue(del.GetHashCode(), out info))
                return true;
            info = default;
            return false;
        }

        public static void Clear()
        {
            sDebugInfoMap.Clear();
            sEventHistory.Clear();
            sStringCache.Clear();
        }

        public static void ClearHistory() => sEventHistory.Clear();
        public static void ClearDebugInfo() => sDebugInfoMap.Clear();
    }
}
