#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// PoolDebugger 的事件写入辅助逻辑。
    /// </summary>
    public static partial class PoolDebugger
    {
        /// <summary>
        /// 在事件历史开启时记录对象归还事件。
        /// </summary>
        /// <param name="info">对象池诊断信息。</param>
        /// <param name="obj">归还对象。</param>
        private static void RecordReturnEventIfNeeded(PoolDebugInfo info, object obj)
        {
            if (!EnableEventHistory)
            {
                return;
            }

            System.Diagnostics.StackTrace stackTraceObject = EnableStackTrace ? new System.Diagnostics.StackTrace(1, true) : null;
            string stackTrace = stackTraceObject != null ? stackTraceObject.ToString() : string.Empty;
            RecordEventLocked(
                PoolEventType.Return,
                info.PoolId,
                info.Name,
                obj,
                stackTrace,
                ParseStackTraceLocation(stackTraceObject, stackTrace));
        }

        /// <summary>
        /// 记录强制归还事件。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="obj">被强制归还的对象。</param>
        private static void RecordForcedReturn(object pool, object obj)
        {
            lock (sLock)
            {
                string poolId = string.Empty;
                string poolName = pool.GetType().Name;
                if (sPools.TryGetValue(pool, out PoolDebugInfo info))
                {
                    poolId = info.PoolId;
                    poolName = info.Name;
                }

                if (EnableEventHistory)
                {
                    RecordEventLocked(PoolEventType.Forced, poolId, poolName, obj, "ForceReturn", default);
                }

                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 写入一条事件记录；调用方必须持有 sLock。
        /// </summary>
        /// <param name="eventType">事件类型。</param>
        /// <param name="poolId">对象池稳定诊断标识。</param>
        /// <param name="poolName">对象池名称。</param>
        /// <param name="obj">事件对象。</param>
        /// <param name="stackTrace">调用堆栈。</param>
        /// <param name="location">调用位置。</param>
        private static void RecordEventLocked(
            PoolEventType eventType,
            string poolId,
            string poolName,
            object obj,
            string stackTrace,
            SourceLocation location)
        {
            while (sEventHistory.Count >= MAX_EVENT_HISTORY)
            {
                sEventHistory.Dequeue();
            }

            sEventHistory.Enqueue(new PoolEvent
            {
                PoolId = poolId,
                EventType = eventType,
                Timestamp = GetElapsedSeconds(),
                PoolName = poolName,
                ObjectName = GetObjectName(obj),
                Source = ParseStackTraceSource(stackTrace),
                SourceFile = location.FilePath,
                SourceLine = location.Line,
                StackTrace = stackTrace,
                ObjRef = obj
            });
        }

        /// <summary>
        /// 安全获取对象显示名；业务类型的 ToString 失败不能反向中断对象池生命周期。
        /// </summary>
        /// <param name="obj">需要显示的对象。</param>
        /// <returns>可用于诊断事件的稳定回退文本。</returns>
        private static string GetObjectName(object obj)
        {
            if (obj == null)
            {
                return "null";
            }

            try
            {
                return obj.ToString();
            }
            catch
            {
                return obj.GetType().FullName;
            }
        }
    }
}
#endif
