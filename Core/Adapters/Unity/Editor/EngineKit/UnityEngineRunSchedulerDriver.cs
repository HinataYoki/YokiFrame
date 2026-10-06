#if UNITY_EDITOR
using System;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>
    /// 在 <c>EditorApplication.update</c> 中推进内存脚本调度器。
    /// </summary>
    /// <remarks>
    /// 每次域加载后做一次对账：重载前停在 Running 且无终态记录的运行判为 Unknown，**绝不自动重放**（§10.2 规则 3）。
    /// 静态字段在域重载时归零，因此对账标记天然按域生效。
    /// </remarks>
    internal static class UnityEngineRunSchedulerDriver
    {
        private static YokiFrameEngineRunScheduler sScheduler;
        private static bool sReconciled;

        /// <summary>幂等启动驱动。</summary>
        /// <param name="scheduler">运行调度器。</param>
        internal static void EnsureStarted(YokiFrameEngineRunScheduler scheduler)
        {
            if (sScheduler != null || scheduler == null)
            {
                return;
            }

            sScheduler = scheduler;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            YokiFrameEngineRunScheduler scheduler = sScheduler;
            if (scheduler == null)
            {
                return;
            }

            if (!sReconciled)
            {
                // The FileBridge publishes identity after load; do not reconcile against an empty identity.
                if (!string.IsNullOrEmpty(scheduler.SessionId))
                {
                    sReconciled = true;
                    scheduler.Reconcile(TimeSpan.FromSeconds(60));
                }
            }

            scheduler.Tick();
        }
    }
}
#endif
