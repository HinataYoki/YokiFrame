using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>
    /// EasyEvent 的编辑器调试辅助器。
    /// 负责记录监听器调试信息以及事件历史。
    /// </summary>
    public static class EasyEventDebugger
    {
        /// <summary>
        /// 监听器调试信息。
        /// </summary>
        public struct ListenerDebugInfo
        {
            public string TargetType;
            public string MethodName;
            public string FilePath;
            public int LineNumber;
            public string StackTrace;
        }

        /// <summary>
        /// 事件历史记录项。
        /// </summary>
        public struct EventHistoryEntry
        {
            public float Time;
            public string Action;
            public string EventType;
            public string EventKey;
            public string Args;
            public string CallerInfo;
        }

        private static readonly Dictionary<int, ListenerDebugInfo> sDebugInfoMap = new();
        private static readonly List<EventHistoryEntry> sEventHistory = new(512);
        private static readonly Dictionary<string, string> sStringCache = new(128);

        public const int MAX_HISTORY_COUNT = 500;

        public static IReadOnlyList<EventHistoryEntry> EventHistory => sEventHistory;

        /// <summary>
        /// 是否记录 Send 事件。
        /// 高频场景默认关闭，避免额外 GC。
        /// </summary>
        public static bool RecordSendEvents { get; set; }

        /// <summary>
        /// 记录 Send 事件时是否额外抓取调用栈。
        /// 仅建议在需要定位问题时开启。
        /// </summary>
        public static bool RecordSendStackTrace { get; set; }

        #region 编辑器回调

        /// <summary>
        /// 事件触发回调，参数为 `(eventType, eventKey, args)`。
        /// </summary>
        public static Action<string, string, string> OnEventTriggered;

        /// <summary>
        /// 事件注册回调，参数为 `(eventType, eventKey)`。
        /// </summary>
        public static Action<string, string> OnEventRegistered;

        /// <summary>
        /// 事件注销回调，参数为 `(eventType, eventKey)`。
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
                if (state != PlayModeStateChange.ExitingPlayMode)
                {
                    return;
                }

                ClearDebugInfo();
                sStringCache.Clear();

                if (EditorPrefs.GetBool("EventKitViewer_ClearHistoryOnStop", true))
                {
                    ClearHistory();
                }
            };
        }

        /// <summary>
        /// 处理监听器注册。
        /// </summary>
        private static void OnRegister(Delegate del)
        {
            if (del == null)
            {
                return;
            }

            var key = del.GetHashCode();
            if (sDebugInfoMap.ContainsKey(key))
            {
                return;
            }

            var info = new ListenerDebugInfo
            {
                TargetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown",
                MethodName = del.Method?.Name ?? "Unknown"
            };

            var stackTrace = new StackTrace(true);
            info.StackTrace = stackTrace.ToString();
            var callerInfo = "";

            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null)
                {
                    continue;
                }

                var declaringType = method.DeclaringType;
                if (declaringType == null)
                {
                    continue;
                }

                var ns = declaringType.Namespace;
                if (ns != null && ns.StartsWith("YokiFrame"))
                {
                    continue;
                }

                if (ns != null && (ns.StartsWith("System") || ns.StartsWith("Unity")))
                {
                    continue;
                }

                info.FilePath = frame.GetFileName();
                info.LineNumber = frame.GetFileLineNumber();

                if (!string.IsNullOrEmpty(info.FilePath))
                {
                    var idx = info.FilePath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        info.FilePath = info.FilePath[idx..].Replace("\\", "/");
                    }

                    callerInfo = GetCachedString($"{info.FilePath}:{info.LineNumber}");
                }

                break;
            }

            sDebugInfoMap[key] = info;

            var eventKey = GetCachedString($"{info.TargetType}.{info.MethodName}");
            AddHistory("Register", "Listener", eventKey, null, callerInfo);
            OnEventRegistered?.Invoke("Listener", eventKey);
        }

        /// <summary>
        /// 处理监听器注销。
        /// </summary>
        private static void OnUnRegister(Delegate del)
        {
            if (del == null)
            {
                return;
            }

            var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
            var methodName = del.Method?.Name ?? "Unknown";
            var eventKey = GetCachedString($"{targetType}.{methodName}");

            sDebugInfoMap.Remove(del.GetHashCode());
            AddHistory("UnRegister", "Listener", eventKey, null, GetCallerInfo());
            OnEventUnregistered?.Invoke("Listener", eventKey);
        }

        /// <summary>
        /// 处理事件发送。
        /// </summary>
        private static void OnSend(string eventType, string eventKey, object args)
        {
            OnEventTriggered?.Invoke(eventType, eventKey, args?.ToString());

            if (!RecordSendEvents)
            {
                return;
            }

            var cachedType = GetCachedString(eventType);
            var cachedKey = GetCachedString(eventKey);

            string argsStr = null;
            if (args != null)
            {
                argsStr = args.ToString();
                if (argsStr.Length > 50)
                {
                    argsStr = argsStr[..47] + "...";
                }
            }

            var callerInfo = RecordSendStackTrace ? GetCallerInfo() : null;
            AddHistory("Send", cachedType, cachedKey, argsStr, callerInfo);
        }

        /// <summary>
        /// 向历史中追加一条记录。
        /// </summary>
        private static void AddHistory(string action, string eventType, string eventKey, string args, string callerInfo)
        {
            if (sEventHistory.Count >= MAX_HISTORY_COUNT)
            {
                sEventHistory.RemoveAt(0);
            }

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

        /// <summary>
        /// 抓取第一个非框架层调用位置。
        /// </summary>
        private static string GetCallerInfo()
        {
            var stackTrace = new StackTrace(true);
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null)
                {
                    continue;
                }

                var declaringType = method.DeclaringType;
                if (declaringType == null)
                {
                    continue;
                }

                var ns = declaringType.Namespace;
                if (ns != null && ns.StartsWith("YokiFrame"))
                {
                    continue;
                }

                if (ns != null && (ns.StartsWith("System") || ns.StartsWith("Unity")))
                {
                    continue;
                }

                var filePath = frame.GetFileName();
                if (!string.IsNullOrEmpty(filePath))
                {
                    var idx = filePath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        filePath = filePath[idx..].Replace("\\", "/");
                    }

                    return GetCachedString($"{filePath}:{frame.GetFileLineNumber()}");
                }

                break;
            }

            return null;
        }

        /// <summary>
        /// 对重复字符串进行缓存，减少调试期间的分配。
        /// </summary>
        private static string GetCachedString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            if (sStringCache.TryGetValue(str, out var cached))
            {
                return cached;
            }

            if (sStringCache.Count > 1000)
            {
                sStringCache.Clear();
            }

            sStringCache[str] = str;
            return str;
        }

        /// <summary>
        /// 尝试获取某个委托的调试信息。
        /// </summary>
        public static bool TryGetDebugInfo(Delegate del, out ListenerDebugInfo info)
        {
            if (del != null && sDebugInfoMap.TryGetValue(del.GetHashCode(), out info))
            {
                return true;
            }

            info = default;
            return false;
        }

        /// <summary>
        /// 清空调试信息和事件历史。
        /// </summary>
        public static void Clear()
        {
            sDebugInfoMap.Clear();
            sEventHistory.Clear();
            sStringCache.Clear();
        }

        /// <summary>
        /// 清空事件历史。
        /// </summary>
        public static void ClearHistory() => sEventHistory.Clear();

        /// <summary>
        /// 清空监听器调试信息。
        /// </summary>
        public static void ClearDebugInfo() => sDebugInfoMap.Clear();
    }
}
