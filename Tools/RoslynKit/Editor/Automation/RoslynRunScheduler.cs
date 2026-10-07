#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 默认调度器实现：单线程 tick 驱动、运行记录持久化、跨域重载按代次与租约对账。
    /// </summary>
    /// <remarks>
    /// 认领在当前域内由锁与记录状态保证；孤儿运行由 <see cref="Reconcile"/> 按租约判为
    /// <see cref="RunStatus.Unknown"/>，因此**绝不自动重放**用户代码（§10.2 规则 3）。
    /// </remarks>
    public sealed partial class RoslynRunScheduler : IRoslynRunScheduler
    {
        /// <summary>默认运行级超时毫秒。</summary>
        public const int DEFAULT_TIMEOUT_MS = 30000;

        /// <summary>默认取消/超时宽限期毫秒。</summary>
        public const int DEFAULT_GRACE_MS = 5000;

        /// <summary>默认终态记录保留时长。</summary>
        public static readonly TimeSpan DEFAULT_TTL = TimeSpan.FromHours(6);

        private sealed class ActiveRun
        {
            internal RoslynRunRecord Record;
            internal IRoslynRunLifetime Context;
            internal Task<RunResult> Task;
            internal DateTime DeadlineUtc;
        }

        private sealed class DetachedRun
        {
            internal RoslynRunRecord Record;
            internal Task<RunResult> Task;
        }

        private readonly RoslynRunStore mStore;
        private readonly IRoslynRunHost mHost;
        private readonly Func<bool> mExecutionBlocked;
        private readonly string mOwnerHostId;
        private const int SCAN_INTERVAL_MS = 250;

        private readonly Dictionary<string, ActiveRun> mActive = new Dictionary<string, ActiveRun>(StringComparer.Ordinal);
        private readonly List<DetachedRun> mDetached = new List<DetachedRun>();
        private readonly Dictionary<string, System.Func<IRoslynRunWork>> mPendingWork =
            new Dictionary<string, System.Func<IRoslynRunWork>>(StringComparer.Ordinal);

        /// <summary>认领租约：只覆盖 Queued→Running 的窗口，因此很短即可。</summary>
        private static readonly TimeSpan CLAIM_LEASE = TimeSpan.FromSeconds(30);
        private readonly object mSync = new object();
        private bool mPendingScan = true;
        private DateTime mLastScanUtc = DateTime.MinValue;
        private const int MAX_CACHED_RECENT_RUNS = 64;
        private IReadOnlyList<RoslynRunRecord> mRecentRuns;
        private DateTime mRecentReadUtc = DateTime.MinValue;
        private long mRecentRevision = -1;

        /// <summary>创建调度器。</summary>
        /// <param name="store">运行存储。</param>
        /// <param name="host">宿主接缝。</param>
        /// <param name="executionBlocked">执行开关是否阻断：关闭时不再认领，并请求取消运行中的任务。</param>
        public RoslynRunScheduler(
            RoslynRunStore store,
            IRoslynRunHost host,
            Func<bool> executionBlocked = null,
            string ownerHostId = "")
        {
            mStore = store ?? throw new ArgumentNullException(nameof(store));
            mHost = host;
            mExecutionBlocked = executionBlocked;
            mOwnerHostId = ownerHostId ?? string.Empty;
        }

        /// <summary>获取当前会话标识。</summary>
        public string SessionId
        {
            get { return mHost == null ? string.Empty : mHost.SessionId ?? string.Empty; }
        }

        /// <summary>获取当前域代次。</summary>
        public long Generation
        {
            get { return mHost == null ? 0L : mHost.Generation; }
        }

        /// <summary>Queues in-memory work; only this domain's factories can be claimed.</summary>
        public RoslynRunRecord SubmitWork(
            string target, string requestId, string source, string payloadHash, int timeoutMs,
            System.Func<IRoslynRunWork> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!RoslynExecutionTargets.TryParse(target, out var parsed)
                || !RoslynExecutionTargets.IsSingleTarget(parsed))
                throw new ArgumentException("A single target is required.", nameof(target));
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(payloadHash))
                throw new ArgumentException("requestId and payloadHash are required.");
            if (timeoutMs < 1 || timeoutMs > 600000)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            lock (mSync)
            {
                if (TryLookupReadOnly(requestId, out var previous))
                {
                    if (previous.Kind != "script" || previous.PayloadHash != payloadHash
                        || previous.OwnerHostId != mOwnerHostId
                        || previous.Target != target || previous.OwnerSessionId != SessionId
                        || previous.Generation != Generation || previous.Source != source)
                        throw new InvalidOperationException("Request identity conflicts with a previous run.");
                    return previous;
                }
                if (mPendingWork.Count >= 16 || mActive.Count + mDetached.Count >= 16)
                    throw new InvalidOperationException("The script queue is full.");
                var record = new RoslynRunRecord
                {
                    RunId = Guid.NewGuid().ToString("N"), RequestId = requestId, Source = source,
                    Kind = "script", Target = target, PayloadHash = payloadHash, TimeoutMs = timeoutMs,
                    GraceMs = DEFAULT_GRACE_MS, OwnerSessionId = SessionId, Generation = Generation,
                    OwnerHostId = mOwnerHostId,
                    State = RunStatus.Queued, SubmittedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.Add(DEFAULT_TTL)
                };
                if (!mStore.TryCreateRequestIndex(requestId, record.RunId, out _))
                    throw new InvalidOperationException("Request index is unavailable or already claimed.");
                record.AddStep("submit", "Queued", "in-memory C#; source not stored in run record");
                mStore.Save(record);
                mPendingWork.Add(record.RunId, factory);
                mPendingScan = true;
                return record;
            }
        }

        /// <summary>推进一帧。</summary>
        public void Tick()
        {
            lock (mSync)
            {
                bool blocked = mExecutionBlocked != null && mExecutionBlocked();
                if (mActive.Count > 0)
                {
                    var snapshot = new List<ActiveRun>(mActive.Values);
                    for (var index = 0; index < snapshot.Count; index++)
                    {
                        StepRun(snapshot[index], blocked);
                    }
                }

                StepDetached();

                if (!blocked && ShouldScanForQueuedRuns())
                {
                    ClaimNextQueued();
                }
            }
        }

        /// <summary>读取运行记录。</summary>
        /// <param name="runId">运行标识。</param>
        /// <param name="record">运行记录。</param>
        /// <returns>命中时返回 true。</returns>
        /// <summary>读取最近运行记录（按提交时间倒序）；纯读。</summary>
        /// <param name="limit">最多返回条数；≤0 时返回空集合。</param>
        /// <returns>运行记录。</returns>
        public IReadOnlyList<RoslynRunRecord> ReadRecentRuns(int limit)
        {
            if (limit <= 0)
            {
                return Array.Empty<RoslynRunRecord>();
            }

            lock (mSync)
            {
                if (limit > MAX_CACHED_RECENT_RUNS) return ReadRecentUncached(limit);
                DateTime now = DateTime.UtcNow;
                if (mRecentRuns == null || mRecentRevision != mStore.Revision
                    || now - mRecentReadUtc >= TimeSpan.FromSeconds(5))
                {
                    mRecentRuns = ReadRecentUncached(MAX_CACHED_RECENT_RUNS);
                    mRecentRevision = mStore.Revision;
                    mRecentReadUtc = now;
                }
                if (mRecentRuns.Count <= limit) return mRecentRuns;
                var result = new List<RoslynRunRecord>(limit);
                for (int i = 0; i < limit; i++) result.Add(mRecentRuns[i]);
                return result.AsReadOnly();
            }
        }

        private IReadOnlyList<RoslynRunRecord> ReadRecentUncached(int limit)
        {
            var ordered = new List<RoslynRunRecord>(mStore.ReadAll());
            ordered.Reverse();
            if (ordered.Count > limit) ordered.RemoveRange(limit, ordered.Count - limit);
            return ordered.AsReadOnly();
        }

        public bool TryReadRun(string runId, out RoslynRunRecord record)
        {
            return mStore.TryReadRun(runId, out record);
        }

        public bool TryLookupReadOnly(string requestId, out RoslynRunRecord record)
        {
            record = null;
            if (mStore.TryReadRequestIndex(requestId, out string runId))
                return mStore.TryReadRun(runId, out record);
            foreach (var candidate in mStore.ReadAll())
            {
                if (candidate.RequestId != requestId) continue;
                record = candidate;
                return true;
            }
            return false;
        }

        /// <summary>请求取消运行。</summary>
        /// <param name="runId">运行标识。</param>
        /// <param name="errorCode">失败错误码。</param>
        /// <param name="errorMessage">失败说明。</param>
        /// <returns>请求被接受时返回 true。</returns>
        public bool TryCancel(string runId, out string errorCode, out string errorMessage)
        {
            errorCode = string.Empty;
            errorMessage = string.Empty;
            lock (mSync)
            {
                if (mActive.TryGetValue(runId, out ActiveRun active))
                {
                    RequestCancel(active, DateTime.UtcNow, "cancel requested");
                    return true;
                }
            }

            if (!mStore.TryReadRun(runId, out RoslynRunRecord record) || record == null)
            {
                errorCode = RoslynErrorCodes.RUN_NOT_FOUND;
                errorMessage = "Run '" + runId + "' does not exist.";
                return false;
            }

            if (record.IsTerminal)
            {
                errorCode = RoslynErrorCodes.FAILED;
                errorMessage = "Run '" + runId + "' already finished with state " + record.State + ".";
                return false;
            }

            if (record.Kind != "script" || record.OwnerHostId != mOwnerHostId
                || record.OwnerSessionId != SessionId || record.Generation != Generation
                || !mPendingWork.ContainsKey(record.RunId))
            {
                errorCode = RoslynErrorCodes.UNAVAILABLE;
                errorMessage = "This scheduler does not own the live script; its execution state is unknown.";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            record.State = RunStatus.Cancelled;
            record.CancelRequestedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.Note = "cancelled before the scheduler claimed it.";
            record.AddStep("cancel", "Cancelled", "queued run cancelled");
            mStore.Save(record);
            mStore.ReleaseClaim(record.RunId);
            lock (mSync) { mPendingWork.Remove(runId); }
            return true;
        }

        /// <summary>读取结果文件原文。</summary>
        /// <param name="relativePath">记录里的结果路径。</param>
        /// <param name="json">结果 JSON。</param>
        /// <returns>读取成功时返回 true。</returns>
        public bool TryReadResultFile(string relativePath, out string json)
        {
            return mStore.TryReadResult(relativePath, out json);
        }

        /// <summary>跨域重载对账。</summary>
        /// <param name="lease">认领租约。</param>
        /// <returns>处理条数。</returns>
        public int Reconcile(TimeSpan lease)
        {
            mPendingScan = true;
            IReadOnlyList<RoslynRunRecord> records = mStore.ReadAll();
            DateTime now = DateTime.UtcNow;
            int touched = 0;
            for (var index = 0; index < records.Count; index++)
            {
                RoslynRunRecord record = records[index];
                if (record.Kind != "script" || record.OwnerHostId != mOwnerHostId) continue;
                if (!record.IsTerminal
                    && !mActive.ContainsKey(record.RunId) && !mPendingWork.ContainsKey(record.RunId))
                {
                    // This host's new domain has no source to replay.
                    if (record.OwnerSessionId == SessionId && record.Generation == Generation) continue;
                    record.State = RunStatus.Unknown;
                    record.Note = "Script memory was lost across host/domain change; not replayed.";
                    record.UpdatedAtUtc = now;
                    mStore.Save(record);
                    mStore.ReleaseClaim(record.RunId);
                    touched++;
                    continue;
                }
                if (record.State == RunStatus.Running)
                {
                    bool active;
                    lock (mSync)
                    {
                        active = mActive.ContainsKey(record.RunId);
                    }

                    if (active || now - record.UpdatedAtUtc < lease)
                    {
                        continue;
                    }

                    record.State = RunStatus.Unknown;
                    record.UpdatedAtUtc = now;
                    record.Note = "domain reloaded while the run was active; state is unknown and will not be replayed.";
                    record.AddStep("reload", "Unknown", "no terminal record before reload");
                    mStore.Save(record);
                    mStore.ReleaseClaim(record.RunId);
                    touched++;
                }
            }

            return touched;
        }
    }
}
#endif
