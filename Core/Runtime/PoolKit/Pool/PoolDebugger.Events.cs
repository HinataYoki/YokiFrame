using System;
using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// PoolDebugger - 事件历史和堆栈追踪
    /// </summary>
    public static partial class PoolDebugger
    {
#if UNITY_EDITOR
        /// <summary>
        /// 记录事件到历史队列
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
        /// 解析堆栈追踪获取调用来源
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
        /// 获取调用者堆栈（跳过对象池内部帧）
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

                // 跳过框架内部帧
                if (ShouldSkipFrame(typeName, methodName))
                {
                    continue;
                }

                foundCaller = true;

                // 处理 UniTask 异步状态机（如 <CreateRoleAsync>d__22）
                var displayTypeName = typeName;
                var displayMethodName = methodName;
                
                if (methodName == "MoveNext" && typeName.Contains("+<") && typeName.Contains(">d__"))
                {
                    // 提取原始类名和方法名
                    var plusIndex = typeName.LastIndexOf('+');
                    if (plusIndex > 0)
                    {
                        var outerType = typeName.Substring(0, plusIndex);
                        var innerType = typeName.Substring(plusIndex + 1);
                        
                        // 提取异步方法名（如 <CreateRoleAsync>d__22 -> CreateRoleAsync）
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

                // 构建堆栈帧字符串
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
        /// 判断是否应该跳过该堆栈帧
        /// </summary>
        private static bool ShouldSkipFrame(string typeName, string methodName)
        {
            // 跳过对象池相关
            if (typeName.Contains("PoolDebugger")) return true;
            if (typeName.Contains("SafePoolKit")) return true;
            if (typeName.Contains("SimplePoolKit")) return true;
            if (typeName.Contains("System.Environment")) return true;

            // 跳过 UniTask 内部实现
            if (typeName.StartsWith("Cysharp.Threading.Tasks"))
            {
                // 保留 MoveNext（异步状态机的实际业务逻辑）
                if (methodName == "MoveNext") return false;
                return true;
            }

            // 跳过 YokiFrame 框架内部实现（保留业务代码和测试代码）
            if (typeName.StartsWith("YokiFrame."))
            {
                // 保留测试代码
                if (typeName.Contains("Test")) return false;
                
                // 跳过 ActionKit 所有内部实现
                if (typeName.Contains("ActionKit") || typeName.Contains("Action.")) return true;
                
                // 跳过 ResKit 内部实现
                if (typeName.Contains("ResKit")) return true;
                
                // 跳过 UIKit 内部实现
                if (typeName.Contains("UIKit")) return true;
                
                // 跳过其他 Kit 内部实现
                if (typeName.Contains("Kit")) return true;
            }

            return false;
        }

        /// <summary>
        /// 获取事件历史（最新的在前）
        /// </summary>
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
        /// 清空事件历史
        /// </summary>
        public static void ClearEventHistory()
        {
            sEventHistory.Clear();
        }
#endif
    }
}
