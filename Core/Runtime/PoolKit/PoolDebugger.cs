using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// PoolKit 运行时监控发布器。
    /// 默认只记录对象池注册表；活跃对象和事件历史需要显式启用，避免编辑器工具拖慢运行时热路径。
    /// </summary>
    public static partial class PoolDebugger
    {
        public const int MAX_EVENT_HISTORY = 200;

        private static readonly object sLock = new();
        private static readonly Dictionary<object, PoolDebugInfo> sPools = new();
        private static readonly Dictionary<object, object> sObjectToPool = new();
        private static readonly Queue<PoolEvent> sEventHistory = new(MAX_EVENT_HISTORY);
        private static readonly long sStartTimestamp = Stopwatch.GetTimestamp();

        /// <summary>对象池跟踪是否启用。</summary>
        public static bool EnableTracking { get; set; }

        /// <summary>是否记录堆栈。</summary>
        public static bool EnableStackTrace { get; set; }

        /// <summary>是否记录对象池事件。</summary>
        public static bool EnableEventHistory { get; set; }

        /// <summary>当前跟踪的对象池数量。</summary>
        public static int PoolCount
        {
            get
            {
                lock (sLock)
                    return sPools.Count;
            }
        }

        /// <summary>当前保留的事件历史数量。</summary>
        public static int EventHistoryCount
        {
            get
            {
                lock (sLock)
                    return sEventHistory.Count;
            }
        }

        /// <summary>注册一个需要跟踪的对象池。</summary>
        public static void RegisterPool(object pool, string name, int maxCacheCount = -1)
        {
            if (pool == null)
                return;

            lock (sLock)
            {
                if (sPools.ContainsKey(pool))
                    return;

                sPools.Add(pool, new PoolDebugInfo
                {
                    Name = string.IsNullOrEmpty(name) ? pool.GetType().Name : name,
                    TypeName = pool.GetType().Name,
                    PoolRef = pool,
                    MaxCacheCount = maxCacheCount
                });
            }
        }

        /// <summary>注销一个对象池。</summary>
        public static void UnregisterPool(object pool)
        {
            if (pool == null)
                return;

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out var info))
                    return;

                for (var i = 0; i < info.ActiveObjects.Count; i++)
                {
                    var obj = info.ActiveObjects[i].Obj;
                    if (obj != null)
                        sObjectToPool.Remove(obj);
                }

                info.InactiveObjects.Clear();
                sPools.Remove(pool);
            }
        }

        /// <summary>记录对象分配。</summary>
        public static void TrackAllocate(object pool, object obj)
        {
            if (!EnableTracking || pool == null || obj == null)
                return;

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out var info))
                    return;

                if (sObjectToPool.ContainsKey(obj))
                    return;

                RemoveInactiveObjectLocked(info, obj);
                var stackTraceObject = EnableStackTrace ? new StackTrace(1, true) : null;
                var stackTrace = stackTraceObject != null ? stackTraceObject.ToString() : string.Empty;
                var location = ParseStackTraceLocation(stackTraceObject, stackTrace);
                info.ActiveObjects.Add(new ActiveObjectInfo
                {
                    Obj = obj,
                    SpawnTime = GetElapsedSeconds(),
                    StackTrace = stackTrace,
                    SourceFile = location.FilePath,
                    SourceLine = location.Line
                });
                info.ActiveCount = info.ActiveObjects.Count;
                if (info.ActiveCount > info.PeakCount)
                    info.PeakCount = info.ActiveCount;

                sObjectToPool[obj] = pool;

                if (EnableEventHistory)
                    RecordEventLocked(PoolEventType.Spawn, info.Name, obj, stackTrace, location);
            }
        }

        /// <summary>记录对象回收。</summary>
        public static void TrackRecycle(object pool, object obj)
        {
            if (!EnableTracking || pool == null || obj == null)
                return;

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out var info))
                    return;

                for (var i = info.ActiveObjects.Count - 1; i >= 0; i--)
                {
                    if (!ReferenceEquals(info.ActiveObjects[i].Obj, obj))
                        continue;

                    info.ActiveObjects.RemoveAt(i);
                    break;
                }

                info.ActiveCount = info.ActiveObjects.Count;
                sObjectToPool.Remove(obj);
                AddInactiveObjectLocked(info, obj);

                if (EnableEventHistory)
                {
                    var stackTraceObject = EnableStackTrace ? new StackTrace(1, true) : null;
                    var stackTrace = stackTraceObject != null ? stackTraceObject.ToString() : string.Empty;
                    RecordEventLocked(PoolEventType.Return, info.Name, obj, stackTrace, ParseStackTraceLocation(stackTraceObject, stackTrace));
                }
            }
        }

        /// <summary>更新对象池总数量。</summary>
        public static void UpdateTotalCount(object pool, int totalCount)
        {
            if (pool == null)
                return;

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out var info))
                    return;

                info.TotalCount = totalCount < 0 ? 0 : totalCount;
                if (info.TotalCount > info.PeakCount)
                    info.PeakCount = info.TotalCount;
            }
        }

        /// <summary>记录当前池内可用对象快照。</summary>
        public static void UpdateInactiveObjects(object pool, IEnumerable<object> inactiveObjects)
        {
            if (!EnableTracking || pool == null)
                return;

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out var info))
                    return;

                info.InactiveObjects.Clear();
                if (inactiveObjects == null)
                    return;

                foreach (var obj in inactiveObjects)
                    AddInactiveObjectLocked(info, obj);
            }
        }

        /// <summary>获取对象池活跃对象数量。</summary>
        public static int GetActiveCount(object pool)
        {
            if (pool == null)
                return 0;

            lock (sLock)
                return sPools.TryGetValue(pool, out var info) ? info.ActiveCount : 0;
        }

        /// <summary>更新对象池最大缓存数量。</summary>
        public static void UpdateMaxCacheCount(object pool, int maxCacheCount)
        {
            if (pool == null)
                return;

            lock (sLock)
            {
                if (sPools.TryGetValue(pool, out var info))
                    info.MaxCacheCount = maxCacheCount;
            }
        }

        /// <summary>获取全部已跟踪对象池的调试信息。</summary>
        public static void GetAllPools(List<PoolDebugInfo> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                foreach (var kvp in sPools)
                    result.Add(kvp.Value);
            }
        }

        /// <summary>将对象强制归还到所属对象池。</summary>
        public static bool ForceReturn(object pool, object obj)
        {
            if (pool == null || obj == null)
                return false;

            var debugReturn = pool as IPoolDebugReturn;
            if (debugReturn == null)
                return false;

            try
            {
                if (!debugReturn.TryRecycleObject(obj))
                    return false;

                lock (sLock)
                {
                    var poolName = sPools.TryGetValue(pool, out var info) ? info.Name : pool.GetType().Name;
                    if (EnableEventHistory)
                        RecordEventLocked(PoolEventType.Forced, poolName, obj, "ForceReturn", default);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>检查对象是否被任一对象池跟踪。</summary>
        public static bool IsObjectTracked(object obj)
        {
            if (obj == null)
                return false;

            lock (sLock)
                return sObjectToPool.ContainsKey(obj);
        }

        /// <summary>获取事件历史，按时间倒序输出。</summary>
        public static void GetEventHistory(List<PoolEvent> result, PoolEventType? filterType = null, string poolName = null)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                var events = sEventHistory.ToArray();
                for (var i = events.Length - 1; i >= 0; i--)
                {
                    var item = events[i];
                    if (filterType.HasValue && item.EventType != filterType.Value)
                        continue;

                    if (!string.IsNullOrEmpty(poolName) && !string.Equals(item.PoolName, poolName, StringComparison.Ordinal))
                        continue;

                    result.Add(item);
                }
            }
        }

        /// <summary>清空事件历史。</summary>
        public static void ClearEventHistory()
        {
            lock (sLock)
                sEventHistory.Clear();
        }

        /// <summary>清空全部运行时数据。</summary>
        public static void Clear()
        {
            lock (sLock)
            {
                sPools.Clear();
                sObjectToPool.Clear();
                sEventHistory.Clear();
            }
        }

        /// <summary>清空全部运行时状态。</summary>
        public static void ClearRuntimeMonitorState() => Clear();

        private static void RecordEventLocked(PoolEventType eventType, string poolName, object obj, string stackTrace, SourceLocation location)
        {
            while (sEventHistory.Count >= MAX_EVENT_HISTORY)
                sEventHistory.Dequeue();

            sEventHistory.Enqueue(new PoolEvent
            {
                EventType = eventType,
                Timestamp = GetElapsedSeconds(),
                PoolName = poolName,
                ObjectName = obj != null ? obj.ToString() : "null",
                Source = ParseStackTraceSource(stackTrace),
                SourceFile = location.FilePath,
                SourceLine = location.Line,
                StackTrace = stackTrace,
                ObjRef = obj
            });
        }

        private static void AddInactiveObjectLocked(PoolDebugInfo info, object obj)
        {
            if (info == null || obj == null)
                return;

            for (var i = 0; i < info.InactiveObjects.Count; i++)
            {
                if (ReferenceEquals(info.InactiveObjects[i].Obj, obj))
                    return;
            }

            info.InactiveObjects.Add(new InactiveObjectInfo { Obj = obj });
        }

        private static void RemoveInactiveObjectLocked(PoolDebugInfo info, object obj)
        {
            if (info == null || obj == null)
                return;

            for (var i = info.InactiveObjects.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(info.InactiveObjects[i].Obj, obj))
                    info.InactiveObjects.RemoveAt(i);
            }
        }

        private static float GetElapsedSeconds()
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - sStartTimestamp;
            return (float)(elapsedTicks / (double)Stopwatch.Frequency);
        }

        private static string ParseStackTraceSource(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return string.Empty;

            var lines = stackTrace.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (IsInternalPoolStackLine(line))
                    continue;

                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    return trimmed;
            }

            return string.Empty;
        }

        private static SourceLocation ParseStackTraceLocation(StackTrace stackTraceObject, string stackTrace)
        {
            if (stackTraceObject != null)
            {
                var frames = stackTraceObject.GetFrames();
                if (frames != null)
                {
                    for (var i = 0; i < frames.Length; i++)
                    {
                        var method = frames[i].GetMethod();
                        var typeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName : string.Empty;
                        if (IsInternalPoolFrame(typeName))
                            continue;

                        var filePath = frames[i].GetFileName();
                        var line = frames[i].GetFileLineNumber();
                        if (!string.IsNullOrEmpty(filePath))
                            return new SourceLocation(filePath.Replace('\\', '/'), line);
                    }
                }
            }

            return ParseStackTraceTextLocation(stackTrace);
        }

        private static SourceLocation ParseStackTraceTextLocation(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return default;

            var lines = stackTrace.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length <= 0 || IsInternalPoolStackLine(line))
                    continue;

                var fileMarker = line.IndexOf(" in ", StringComparison.Ordinal);
                if (fileMarker < 0)
                    continue;

                var fileStart = fileMarker + 4;
                var lineMarker = line.LastIndexOf(":line ", StringComparison.Ordinal);
                if (lineMarker <= fileStart)
                    continue;

                var filePath = line.Substring(fileStart, lineMarker - fileStart).Replace('\\', '/');
                var lineText = line.Substring(lineMarker + 6).Trim();
                int.TryParse(lineText, out var lineNumber);
                return new SourceLocation(filePath, lineNumber);
            }

            return default;
        }

        private static bool IsInternalPoolFrame(string typeName)
        {
            return string.IsNullOrEmpty(typeName) ||
                   typeName.IndexOf("PoolDebugger", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.PoolKit", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.SimplePoolKit", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.SafePoolKit", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.ListPool", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.DictPool", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.SetPool", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.Pool", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("System.Diagnostics", StringComparison.Ordinal) >= 0;
        }

        private static bool IsInternalPoolStackLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return true;

            return line.IndexOf("PoolDebugger", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.PoolKit", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.SimplePoolKit", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.SafePoolKit", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.ListPool", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.DictPool", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.SetPool", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("YokiFrame.Pool.", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("System.Diagnostics", StringComparison.Ordinal) >= 0;
        }

        private readonly struct SourceLocation
        {
            public readonly string FilePath;
            public readonly int Line;

            public SourceLocation(string filePath, int line)
            {
                FilePath = filePath;
                Line = line;
            }
        }
    }
}
