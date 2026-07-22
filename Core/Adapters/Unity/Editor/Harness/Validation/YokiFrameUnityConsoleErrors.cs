#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>通过隔离的 Unity Console 反射读取有界 Error 证据，不支持时明确失败。</summary>
    internal static class YokiFrameUnityConsoleErrors
    {
        private const int MAX_MESSAGE_LENGTH = 1000;

        /// <summary>从当前 Unity Console 创建 Error 快照。</summary>
        /// <param name="context">当前 Harness 身份。</param>
        /// <param name="maxCount">最多返回的 Error 明细。</param>
        /// <returns>带扫描完整性标志的 Error 快照。</returns>
        public static YokiFrameUnityConsoleErrorObservation Inspect(
            YokiFrameUnityHarnessContext context,
            int maxCount)
        {
            return Inspect(context, maxCount, new UnityConsoleProbeProvider());
        }

        /// <summary>使用可注入事实源创建 Error 快照，供 EditMode 测试覆盖裁剪语义。</summary>
        /// <param name="context">当前 Harness 身份。</param>
        /// <param name="maxCount">最多返回明细。</param>
        /// <param name="provider">Console 事实源。</param>
        /// <returns>Console Error 快照。</returns>
        internal static YokiFrameUnityConsoleErrorObservation Inspect(
            YokiFrameUnityHarnessContext context,
            int maxCount,
            IYokiFrameUnityConsoleProbeProvider provider)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var normalizedMaxCount = NormalizeMaxCount(maxCount);
            var probe = provider.Read(2000)
                ?? throw new YokiFrameUnityHarnessQueryException("ConsoleObservationFailed", "Unity Console provider returned no result.");
            return CreateObservation(context, probe, normalizedMaxCount);
        }

        /// <summary>验证返回明细上限，0 使用协议默认值。</summary>
        /// <param name="maxCount">调用方上限。</param>
        /// <returns>规范化后的 1..MAX 上限。</returns>
        private static int NormalizeMaxCount(int maxCount)
        {
            if (maxCount == 0)
            {
                return 100;
            }

            if (maxCount < 1 || maxCount > 100)
            {
                throw new YokiFrameUnityHarnessQueryException(
                    "InvalidPayload",
                    "Validation/get_console_errors maxCount is outside the allowed range.");
            }

            return maxCount;
        }

        /// <summary>从扫描事实筛选 Error，并只保留最后若干明细。</summary>
        /// <param name="context">当前宿主身份。</param>
        /// <param name="probe">Console 扫描事实。</param>
        /// <param name="maxCount">返回明细上限。</param>
        /// <returns>协议观察。</returns>
        private static YokiFrameUnityConsoleErrorObservation CreateObservation(
            YokiFrameUnityHarnessContext context,
            YokiFrameUnityConsoleProbe probe,
            int maxCount)
        {
            var errors = new List<YokiFrameUnityConsoleErrorEntry>(maxCount);
            var errorCount = CollectErrors(probe.Entries, maxCount, errors);
            var result = new YokiFrameUnityConsoleErrorObservation
            {
                status = probe.ScanComplete ? "Ready" : "Partial",
                observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                totalEntryCount = probe.TotalEntryCount,
                scannedEntryCount = probe.Entries.Length,
                scanComplete = probe.ScanComplete,
                errorCount = errorCount,
                returnedCount = errors.Count,
                truncated = !probe.ScanComplete || errorCount > errors.Count,
                errors = errors.ToArray()
            };
            result.ApplyContext(context);
            return result;
        }

        /// <summary>统计全部已扫描 Error，并以固定容量保留最后若干条。</summary>
        /// <param name="entries">扫描条目。</param>
        /// <param name="maxCount">明细上限。</param>
        /// <param name="errors">明细输出。</param>
        /// <returns>扫描范围 Error 总数。</returns>
        private static int CollectErrors(
            YokiFrameUnityConsoleEntryFact[] entries,
            int maxCount,
            IList<YokiFrameUnityConsoleErrorEntry> errors)
        {
            var errorCount = 0;
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry == null || !entry.IsError)
                {
                    continue;
                }

                errorCount++;
                if (errors.Count == maxCount)
                {
                    errors.RemoveAt(0);
                }

                errors.Add(new YokiFrameUnityConsoleErrorEntry
                {
                    index = entry.Index,
                    message = TrimMessage(entry.Message)
                });
            }

            return errorCount;
        }

        /// <summary>裁剪单条 Console 消息，避免 response 被异常长堆栈撑大。</summary>
        /// <param name="message">原始消息。</param>
        /// <returns>有界消息。</returns>
        private static string TrimMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= MAX_MESSAGE_LENGTH)
            {
                return message ?? string.Empty;
            }

            return message.Substring(0, MAX_MESSAGE_LENGTH);
        }

        /// <summary>使用 UnityEditor.LogEntries 内部只读 API 的隔离反射事实源。</summary>
        private sealed class UnityConsoleProbeProvider : IYokiFrameUnityConsoleProbeProvider
        {
            /// <summary>解析当前 Unity 版本反射入口并读取最后的固定数量条目。</summary>
            /// <param name="maxEntries">最多扫描条目数。</param>
            /// <returns>Console 扫描事实。</returns>
            public YokiFrameUnityConsoleProbe Read(int maxEntries)
            {
                var reflection = ResolveReflection();
                try
                {
                    return ReadEntries(reflection, maxEntries);
                }
                catch (TargetInvocationException exception)
                {
                    var detail = exception.InnerException == null ? exception.Message : exception.InnerException.Message;
                    throw new YokiFrameUnityHarnessQueryException("ConsoleObservationFailed", detail);
                }
                catch (Exception exception)
                {
                    throw new YokiFrameUnityHarnessQueryException("ConsoleObservationFailed", exception.Message);
                }
            }

            /// <summary>解析 UnityEditor.LogEntries/LogEntry 类型、方法和字段。</summary>
            /// <returns>完整可调用反射入口。</returns>
            private static ConsoleReflection ResolveReflection()
            {
                var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
                if (assembly == null)
                {
                    throw Unsupported("UnityEditor assembly is unavailable.");
                }

                var entriesType = ResolveType(assembly, "UnityEditor.LogEntries", "UnityEditorInternal.LogEntries");
                var entryType = ResolveType(assembly, "UnityEditor.LogEntry", "UnityEditorInternal.LogEntry");
                return BuildReflection(entriesType, entryType);
            }

            /// <summary>构造并验证读取 Console 所需的最小反射入口。</summary>
            /// <param name="entriesType">LogEntries 类型。</param>
            /// <param name="entryType">LogEntry 类型。</param>
            /// <returns>已验证反射入口。</returns>
            private static ConsoleReflection BuildReflection(Type entriesType, Type entryType)
            {
                if (entriesType == null || entryType == null)
                {
                    throw Unsupported("Unity Console reflection types are unavailable in this Editor version.");
                }

                const BindingFlags methodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                const BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var result = new ConsoleReflection
                {
                    EntryType = entryType,
                    GetCount = entriesType.GetMethod("GetCount", methodFlags),
                    Start = entriesType.GetMethod("StartGettingEntries", methodFlags),
                    End = entriesType.GetMethod("EndGettingEntries", methodFlags),
                    GetEntry = entriesType.GetMethod("GetEntryInternal", methodFlags),
                    Message = entryType.GetField("message", fieldFlags),
                    Condition = entryType.GetField("condition", fieldFlags),
                    Mode = entryType.GetField("mode", fieldFlags)
                };
                EnsureReflectionComplete(result);
                return result;
            }

            /// <summary>拒绝缺失任一必要方法或字段的 Unity 版本，避免静默返回空日志。</summary>
            /// <param name="reflection">待验证入口。</param>
            private static void EnsureReflectionComplete(ConsoleReflection reflection)
            {
                if (reflection.GetCount == null
                    || reflection.Start == null
                    || reflection.End == null
                    || reflection.GetEntry == null
                    || reflection.Mode == null
                    || reflection.Message == null && reflection.Condition == null)
                {
                    throw Unsupported("Unity Console reflection members are unsupported in this Editor version.");
                }
            }

            /// <summary>在 Start/EndGettingEntries 配对范围内读取有界 Console 条目。</summary>
            /// <param name="reflection">已验证反射入口。</param>
            /// <param name="maxEntries">扫描上限。</param>
            /// <returns>扫描事实。</returns>
            private static YokiFrameUnityConsoleProbe ReadEntries(ConsoleReflection reflection, int maxEntries)
            {
                var totalCount = (int)reflection.GetCount.Invoke(null, null);
                var startIndex = Math.Max(0, totalCount - maxEntries);
                var facts = new List<YokiFrameUnityConsoleEntryFact>(totalCount - startIndex);
                reflection.Start.Invoke(null, null);
                try
                {
                    for (var index = startIndex; index < totalCount; index++)
                    {
                        AddEntry(reflection, index, facts);
                    }
                }
                finally
                {
                    reflection.End.Invoke(null, null);
                }

                return new YokiFrameUnityConsoleProbe
                {
                    TotalEntryCount = totalCount,
                    ScanComplete = startIndex == 0,
                    Entries = facts.ToArray()
                };
            }

            /// <summary>读取一条 Console 记录并转换成最小分类事实。</summary>
            /// <param name="reflection">反射入口。</param>
            /// <param name="index">Console 索引。</param>
            /// <param name="facts">事实输出。</param>
            private static void AddEntry(
                ConsoleReflection reflection,
                int index,
                ICollection<YokiFrameUnityConsoleEntryFact> facts)
            {
                var entry = Activator.CreateInstance(reflection.EntryType);
                if (!(reflection.GetEntry.Invoke(null, new[] { (object)index, entry }) is bool success) || !success)
                {
                    return;
                }

                var mode = reflection.Mode.GetValue(entry) is int value ? value : 0;
                facts.Add(new YokiFrameUnityConsoleEntryFact
                {
                    Index = index,
                    IsError = IsErrorMode(mode),
                    Message = ReadMessage(reflection, entry)
                });
            }

            /// <summary>优先读取 message，旧 Unity 缺失时回落 condition。</summary>
            /// <param name="reflection">反射入口。</param>
            /// <param name="entry">Console 条目对象。</param>
            /// <returns>Console 消息。</returns>
            private static string ReadMessage(ConsoleReflection reflection, object entry)
            {
                var message = reflection.Message == null ? null : reflection.Message.GetValue(entry) as string;
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }

                return reflection.Condition == null ? string.Empty : reflection.Condition.GetValue(entry) as string ?? string.Empty;
            }

            /// <summary>按 Unity LogMessageFlags 兼容掩码归类 Error/Assert/Exception/Compile Error。</summary>
            /// <param name="mode">Unity Console mode 位标志。</param>
            /// <returns>属于 Error 证据时返回 true。</returns>
            private static bool IsErrorMode(int mode)
            {
                return (mode & ERROR_LOG_MASK) != 0;
            }

            /// <summary>按候选名称解析 Unity Editor 内部类型。</summary>
            /// <param name="assembly">UnityEditor 程序集。</param>
            /// <param name="names">候选类型名。</param>
            /// <returns>首个已解析类型。</returns>
            private static Type ResolveType(Assembly assembly, params string[] names)
            {
                for (var index = 0; index < names.Length; index++)
                {
                    var type = assembly.GetType(names[index]);
                    if (type != null)
                    {
                        return type;
                    }
                }

                return null;
            }

            /// <summary>创建明确的不支持错误，不把反射漂移伪装成零错误。</summary>
            /// <param name="message">不支持原因。</param>
            /// <returns>稳定 terminal error。</returns>
            private static YokiFrameUnityHarnessQueryException Unsupported(string message)
            {
                return new YokiFrameUnityHarnessQueryException("ConsoleObservationUnsupported", message);
            }
        }

        /// <summary>缓存单次 Console 读取所需的反射成员。</summary>
        private sealed class ConsoleReflection
        {
            public Type EntryType;
            public MethodInfo GetCount;
            public MethodInfo Start;
            public MethodInfo End;
            public MethodInfo GetEntry;
            public FieldInfo Message;
            public FieldInfo Condition;
            public FieldInfo Mode;
        }

        private const int ERROR_LOG_MASK =
            1 << 0 |
            1 << 1 |
            1 << 4 |
            1 << 6 |
            1 << 8 |
            1 << 11 |
            1 << 17 |
            1 << 21;
    }
}

#endif
