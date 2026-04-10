using System;
using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// Event-history and stack-trace helpers for <see cref="PoolDebugger"/>.
    /// </summary>
    public static partial class PoolDebugger
    {
#if UNITY_EDITOR
        /// <summary>
        /// Writes a pool event into history and publishes it to the Editor monitor channel.
        /// </summary>
        private static void RecordEvent(PoolEventType eventType, string poolName, object obj, string stackTrace)
        {
            while (sEventHistory.Count >= MAX_EVENT_HISTORY)
            {
                sEventHistory.Dequeue();
            }

            var objName = obj?.ToString() ?? "null";
            if (obj is UnityEngine.Object unityObj && unityObj != default)
            {
                objName = unityObj.name;
            }

            var evt = new PoolEvent
            {
                EventType = eventType,
                Timestamp = Time.realtimeSinceStartup,
                PoolName = poolName,
                ObjectName = objName,
                Source = ParseStackTraceSource(stackTrace),
                StackTrace = stackTrace,
                ObjRef = obj
            };

            sEventHistory.Enqueue(evt);
            NotifyEditorDataChanged(CHANNEL_POOL_EVENT_LOGGED, evt);
        }

        /// <summary>
        /// Extracts a user-facing source string from a raw stack trace.
        /// </summary>
        private static string ParseStackTraceSource(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "Unknown";

            var lines = stackTrace.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("System.Environment")) continue;
                if (line.Contains("PoolDebugger")) continue;
                if (line.Contains("PoolKit")) continue;
                if (line.Contains("SafePoolKit")) continue;
                if (line.Contains("SimplePoolKit")) continue;
                if (line.Contains("UnityEngine.")) continue;
                if (line.Contains("UnityEditor.")) continue;

                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith("at "))
                {
                    trimmed = trimmed.Substring(3);
                }

                var parenIndex = trimmed.IndexOf('(');
                if (parenIndex > 0)
                {
                    trimmed = trimmed.Substring(0, parenIndex);
                }

                if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 3 && trimmed.Contains("."))
                {
                    return trimmed;
                }
            }

            return "Unknown";
        }

        /// <summary>
        /// Captures a business-facing caller stack trace while skipping framework-internal frames.
        /// </summary>
        private static string GetCallerStackTrace()
        {
            var st = new StackTrace(true);
            var frames = st.GetFrames();
            if (frames == default || frames.Length == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            var foundCaller = false;

            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var method = frame.GetMethod();
                if (method == default) continue;

                var declaringType = method.DeclaringType;
                if (declaringType == default) continue;

                var typeName = declaringType.FullName ?? declaringType.Name;
                var methodName = method.Name;

                if (ShouldSkipFrame(typeName, methodName))
                {
                    continue;
                }

                foundCaller = true;

                var displayTypeName = typeName;
                var displayMethodName = methodName;
                if (methodName == "MoveNext" && typeName.Contains("+<") && typeName.Contains(">d__"))
                {
                    var plusIndex = typeName.LastIndexOf('+');
                    if (plusIndex > 0)
                    {
                        var outerType = typeName.Substring(0, plusIndex);
                        var innerType = typeName.Substring(plusIndex + 1);
                        var startIndex = innerType.IndexOf('<');
                        var endIndex = innerType.IndexOf('>');
                        if (startIndex >= 0 && endIndex > startIndex)
                        {
                            var asyncMethodName = innerType.Substring(startIndex + 1, endIndex - startIndex - 1);
                            displayTypeName = outerType;
                            displayMethodName = asyncMethodName;
                        }
                    }
                }

                sb.Append(displayTypeName);
                sb.Append('.');
                sb.Append(displayMethodName);
                sb.Append(" ()");

                var fileName = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();
                if (!string.IsNullOrEmpty(fileName) && lineNumber > 0)
                {
                    sb.Append(" [0x00000] in ");
                    sb.Append(fileName);
                    sb.Append(':');
                    sb.Append(lineNumber);
                }

                sb.AppendLine();
            }

            return foundCaller ? sb.ToString() : string.Empty;
        }

        /// <summary>
        /// Determines whether a stack frame should be hidden from the business-facing trace.
        /// </summary>
        private static bool ShouldSkipFrame(string typeName, string methodName)
        {
            if (typeName.Contains("PoolDebugger")) return true;
            if (typeName.Contains("SafePoolKit")) return true;
            if (typeName.Contains("SimplePoolKit")) return true;
            if (typeName.Contains("System.Environment")) return true;

            if (typeName.StartsWith("Cysharp.Threading.Tasks"))
            {
                if (methodName == "MoveNext") return false;
                return true;
            }

            if (typeName.StartsWith("YokiFrame."))
            {
                if (typeName.Contains("Test")) return false;
                if (typeName.Contains("ActionKit") || typeName.Contains("Action.")) return true;
                if (typeName.Contains("ResKit")) return true;
                if (typeName.Contains("UIKit")) return true;
                if (typeName.Contains("Kit")) return true;
            }

            return false;
        }

        /// <summary>
        /// Returns pool event history in reverse chronological order.
        /// </summary>
        /// <param name="result">Target list that will be cleared first.</param>
        /// <param name="filterType">Optional event type filter.</param>
        /// <param name="poolName">Optional pool-name filter.</param>
        public static void GetEventHistory(List<PoolEvent> result, PoolEventType? filterType = null, string poolName = null)
        {
            result.Clear();

            var events = sEventHistory.ToArray();
            for (int i = events.Length - 1; i >= 0; i--)
            {
                var evt = events[i];

                if (filterType != null && evt.EventType != filterType.Value) continue;
                if (!string.IsNullOrEmpty(poolName) && evt.PoolName != poolName) continue;

                result.Add(evt);
            }
        }

        /// <summary>
        /// Clears the retained event history.
        /// </summary>
        public static void ClearEventHistory()
        {
            sEventHistory.Clear();
        }
#endif
    }
}
