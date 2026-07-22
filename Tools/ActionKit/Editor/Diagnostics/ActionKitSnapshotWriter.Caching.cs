#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>承载 ActionKit Editor 状态快照的版本缓存与载荷预算编排。</summary>
    internal static partial class ActionKitSnapshotWriter
    {
        private static readonly object sSnapshotCacheSyncRoot = new();
        private static string sCachedWorkbenchSnapshot;
        private static long sCachedWorkbenchVersion = long.MinValue;

        /// <summary>创建活动树、累计指标、最近终态和有界堆栈的 Workbench 状态。</summary>
        /// <returns>适合 Snapshot 与 Shared Memory 的 JSON。</returns>
        internal static string WriteWorkbench()
        {
            long snapshotVersion = ActionKitScheduler.DiagnosticVersion;
            lock (sSnapshotCacheSyncRoot)
            {
                if (sCachedWorkbenchVersion == snapshotVersion
                    && sCachedWorkbenchSnapshot != null)
                {
                    return sCachedWorkbenchSnapshot;
                }
            }

            string snapshot = BuildBoundedWorkbenchSnapshot(snapshotVersion);
            if (snapshotVersion != ActionKitScheduler.DiagnosticVersion)
            {
                return snapshot;
            }

            lock (sSnapshotCacheSyncRoot)
            {
                if (sCachedWorkbenchVersion == snapshotVersion
                    && sCachedWorkbenchSnapshot != null)
                {
                    return sCachedWorkbenchSnapshot;
                }

                sCachedWorkbenchSnapshot = snapshot;
                sCachedWorkbenchVersion = snapshotVersion;
                return snapshot;
            }
        }

        /// <summary>按当前根和终态快照逐级缩减诊断预算，直到结果适合共享内存承载。</summary>
        /// <param name="snapshotVersion">本次快照对应的稳定诊断版本。</param>
        /// <returns>不超过协议载荷上限的完整或紧凑 ActionKit 状态。</returns>
        private static string BuildBoundedWorkbenchSnapshot(long snapshotVersion)
        {
            List<IActionController> controllers = new(MAX_ROOTS);
            ActionKitScheduler.GetExecutingActionControllers(controllers);
            ActionKitTerminalEvent[] events = ActionKitDiagnosticHistory.CreateLatestSnapshot();
            int nodeLimit = MAX_NODES;
            int stackFrameLimit = MAX_STACK_FRAMES;
            int eventLimit = events.Length;
            while (true)
            {
                string snapshot = BuildWorkbench(
                    snapshotVersion, controllers, events, nodeLimit, stackFrameLimit, eventLimit);
                if (Encoding.UTF8.GetByteCount(snapshot)
                    <= YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
                {
                    return snapshot;
                }

                if (stackFrameLimit > 0) stackFrameLimit /= 2;
                else if (nodeLimit > MIN_REDUCED_NODES)
                    nodeLimit = System.Math.Max(MIN_REDUCED_NODES, nodeLimit / 2);
                else if (eventLimit > 0) eventLimit /= 2;
                else return BuildCompactWorkbench(snapshotVersion, controllers);
            }
        }
    }
}
#endif
