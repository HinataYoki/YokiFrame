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
    /// <see cref="YokiFrameRunStatus.Unknown"/>，因此**绝不自动重放**用户代码（§10.2 规则 3）。
    /// </remarks>
    public sealed class YokiFrameEngineRunScheduler : IYokiFrameEngineRunScheduler
    {
        /// <summary>默认运行级超时毫秒。</summary>
        public const int DEFAULT_TIMEOUT_MS = 30000;

        /// <summary>默认取消/超时宽限期毫秒。</summary>
        public const int DEFAULT_GRACE_MS = 5000;

        /// <summary>默认终态记录保留时长。</summary>
        public static readonly TimeSpan DEFAULT_TTL = TimeSpan.FromHours(6);

        private sealed class ActiveRun
        {
            internal YokiFrameEngineRunRecord Record;
            internal IYokiFrameEngineRunLifetime Context;
            internal Task<YokiFrameRunResult> Task;
            internal DateTime DeadlineUtc;
        }

        private sealed class DetachedRun
        {
            internal YokiFrameEngineRunRecord Record;
            internal Task<YokiFrameRunResult> Task;
        }

        private readonly YokiFrameEngineRunStore mStore;
        private readonly IYokiFrameEngineRunHost mHost;
        private readonly Func<bool> mExecutionBlocked;
        private readonly string mOwnerHostId;
        private const int SCAN_INTERVAL_MS = 250;

        private readonly Dictionary<string, ActiveRun> mActive = new Dictionary<string, ActiveRun>(StringComparer.Ordinal);
        private readonly List<DetachedRun> mDetached = new List<DetachedRun>();
        private readonly Dictionary<string, System.Func<IYokiFrameEngineRunWork>> mPendingWork =
            new Dictionary<string, System.Func<IYokiFrameEngineRunWork>>(StringComparer.Ordinal);

        /// <summary>认领租约：只覆盖 Queued→Running 的窗口，因此很短即可。</summary>
        private static readonly TimeSpan CLAIM_LEASE = TimeSpan.FromSeconds(30);
        private readonly object mSync = new object();
        private bool mPendingScan = true;
        private DateTime mLastScanUtc = DateTime.MinValue;

        /// <summary>创建调度器。</summary>
        /// <param name="store">运行存储。</param>
        /// <param name="host">宿主接缝。</param>
        /// <param name="executionBlocked">执行开关是否阻断：关闭时不再认领，并请求取消运行中的任务。</param>
        public YokiFrameEngineRunScheduler(
            YokiFrameEngineRunStore store,
            IYokiFrameEngineRunHost host,
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
        public YokiFrameEngineRunRecord SubmitWork(
            string target, string requestId, string source, string payloadHash, int timeoutMs,
            System.Func<IYokiFrameEngineRunWork> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!YokiFrameEngineExecutionTargets.TryParse(target, out var parsed)
                || !YokiFrameEngineExecutionTargets.IsSingleTarget(parsed))
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
                var record = new YokiFrameEngineRunRecord
                {
                    RunId = Guid.NewGuid().ToString("N"), RequestId = requestId, Source = source,
                    Kind = "script", Target = target, PayloadHash = payloadHash, TimeoutMs = timeoutMs,
                    GraceMs = DEFAULT_GRACE_MS, OwnerSessionId = SessionId, Generation = Generation,
                    OwnerHostId = mOwnerHostId,
                    State = YokiFrameRunStatus.Queued, SubmittedAtUtc = DateTime.UtcNow,
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
        public IReadOnlyList<YokiFrameEngineRunRecord> ReadRecentRuns(int limit)
        {
            if (limit <= 0)
            {
                return Array.Empty<YokiFrameEngineRunRecord>();
            }

            IReadOnlyList<YokiFrameEngineRunRecord> records = mStore.ReadAll();
            var ordered = new List<YokiFrameEngineRunRecord>(records);
            ordered.Sort((left, right) => right.SubmittedAtUtc.CompareTo(left.SubmittedAtUtc));
            return ordered.Count <= limit ? ordered : ordered.GetRange(0, limit);
        }

        public bool TryReadRun(string runId, out YokiFrameEngineRunRecord record)
        {
            return mStore.TryReadRun(runId, out record);
        }

        public bool TryLookupReadOnly(string requestId, out YokiFrameEngineRunRecord record)
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

            if (!mStore.TryReadRun(runId, out YokiFrameEngineRunRecord record) || record == null)
            {
                errorCode = YokiFrameEngineErrorCodes.RUN_NOT_FOUND;
                errorMessage = "Run '" + runId + "' does not exist.";
                return false;
            }

            if (record.IsTerminal)
            {
                errorCode = YokiFrameEngineErrorCodes.FAILED;
                errorMessage = "Run '" + runId + "' already finished with state " + record.State + ".";
                return false;
            }

            if (record.Kind != "script" || record.OwnerHostId != mOwnerHostId
                || record.OwnerSessionId != SessionId || record.Generation != Generation
                || !mPendingWork.ContainsKey(record.RunId))
            {
                errorCode = YokiFrameEngineErrorCodes.UNAVAILABLE;
                errorMessage = "This scheduler does not own the live script; its execution state is unknown.";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            record.State = YokiFrameRunStatus.Cancelled;
            record.CancelRequestedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.Note = "cancelled before the scheduler claimed it.";
            record.AddStep("cancel", "Cancelled", "queued run cancelled");
            mStore.Save(record);
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
            IReadOnlyList<YokiFrameEngineRunRecord> records = mStore.ReadAll();
            DateTime now = DateTime.UtcNow;
            int touched = 0;
            for (var index = 0; index < records.Count; index++)
            {
                YokiFrameEngineRunRecord record = records[index];
                if (record.Kind != "script" || record.OwnerHostId != mOwnerHostId) continue;
                if (!record.IsTerminal
                    && !mActive.ContainsKey(record.RunId) && !mPendingWork.ContainsKey(record.RunId))
                {
                    // This host's new domain has no source to replay.
                    if (record.OwnerSessionId == SessionId && record.Generation == Generation) continue;
                    record.State = YokiFrameRunStatus.Unknown;
                    record.Note = "Script memory was lost across host/domain change; not replayed.";
                    record.UpdatedAtUtc = now;
                    mStore.Save(record);
                    touched++;
                    continue;
                }
                if (record.State == YokiFrameRunStatus.Running)
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

                    record.State = YokiFrameRunStatus.Unknown;
                    record.UpdatedAtUtc = now;
                    record.Note = "domain reloaded while the run was active; state is unknown and will not be replayed.";
                    record.AddStep("reload", "Unknown", "no terminal record before reload");
                    mStore.Save(record);
                    touched++;
                }
            }

            return touched;
        }

        private void StepRun(ActiveRun active, bool blocked)
        {
            YokiFrameEngineRunRecord record = active.Record;
            DateTime now = DateTime.UtcNow;

            if (active.Task != null && active.Task.IsCompleted)
            {
                Finalize(active, now);
                return;
            }

            if (record.State == YokiFrameRunStatus.Compiling
                && active.Context is IYokiFrameEngineRunWork work && !work.IsCompiling)
            {
                record.State = YokiFrameRunStatus.Running;
                record.AddStep("compiled", "Running", "in-memory assembly loaded");
                mStore.Save(record);
            }
            if (blocked && !record.CancelRequestedAtUtc.HasValue)
            {
                RequestCancel(active, now, "execution switch turned off");
            }

            if (!record.TimeoutRequestedAtUtc.HasValue && active.DeadlineUtc <= now)
            {
                RequestTimeout(active, now);
            }

            int graceMs = record.GraceMs > 0 ? record.GraceMs : DEFAULT_GRACE_MS;
            if (record.CancelRequestedAtUtc.HasValue
                && now - record.CancelRequestedAtUtc.Value >= TimeSpan.FromMilliseconds(graceMs))
            {
                Detach(active, now, YokiFrameRunStatus.CancelRequested, "cancel grace expired");
                return;
            }

            if (record.TimeoutRequestedAtUtc.HasValue
                && now - record.TimeoutRequestedAtUtc.Value >= TimeSpan.FromMilliseconds(graceMs))
            {
                Detach(active, now, YokiFrameRunStatus.TimeoutRequested, "timeout grace expired");
                return;
            }

            if (active.Context != null)
            {
                active.Context.AdvanceFrame();
            }
        }

        private void RequestCancel(ActiveRun active, DateTime now, string reason)
        {
            YokiFrameEngineRunRecord record = active.Record;
            record.State = YokiFrameRunStatus.CancelRequested;
            record.CancelRequestedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.AddStep("cancel", "CancelRequested", reason);
            mStore.Save(record);
            if (active.Context != null)
            {
                active.Context.Invalidate();
            }
        }

        private void RequestTimeout(ActiveRun active, DateTime now)
        {
            YokiFrameEngineRunRecord record = active.Record;
            if (record.CancelRequestedAtUtc.HasValue)
            {
                return;
            }

            record.State = YokiFrameRunStatus.TimeoutRequested;
            record.TimeoutRequestedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.AddStep("timeout", "TimeoutRequested", "deadline reached");
            mStore.Save(record);
            if (active.Context != null)
            {
                active.Context.Invalidate();
            }
        }

        private void Detach(ActiveRun active, DateTime now, YokiFrameRunStatus requested, string reason)
        {
            YokiFrameEngineRunRecord record = active.Record;
            record.State = YokiFrameRunStatus.Detached;
            record.DetachedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.Note = "framework stopped waiting (" + reason + "); user code may still be running.";
            record.StaleContextCalls = active.Context == null ? 0 : active.Context.StaleContextCalls;
            record.AddStep("detach", "Detached", requested.ToString());
            mStore.Save(record);
            lock (mSync)
            {
                mActive.Remove(record.RunId);
                if (active.Task != null && !active.Task.IsCompleted)
                {
                    mDetached.Add(new DetachedRun { Record = record, Task = active.Task });
                }
            }
        }

        private void StepDetached()
        {
            for (var index = mDetached.Count - 1; index >= 0; index--)
            {
                DetachedRun detached = mDetached[index];
                if (detached.Task == null || !detached.Task.IsCompleted)
                {
                    continue;
                }

                YokiFrameRunResult late = ReadTaskResult(detached.Task);
                detached.Record.LateResultPath = mStore.SaveResult(detached.Record.RunId + "-late", late);
                detached.Record.LateCompletionAtUtc = DateTime.UtcNow;
                detached.Record.UpdatedAtUtc = DateTime.UtcNow;
                detached.Record.AddStep("late-completion", detached.Record.State.ToString(), "terminal state kept");
                mStore.Save(detached.Record);
                mDetached.RemoveAt(index);
            }
        }

        private void Finalize(ActiveRun active, DateTime now)
        {
            YokiFrameEngineRunRecord record = active.Record;
            YokiFrameRunResult result = ReadTaskResult(active.Task);
            YokiFrameRunStatus status = result.Status;
            if (record.CancelRequestedAtUtc.HasValue)
            {
                status = YokiFrameRunStatus.Cancelled;
            }
            else if (record.TimeoutRequestedAtUtc.HasValue)
            {
                status = YokiFrameRunStatus.Timeout;
            }
            else if (active.Task.IsFaulted)
            {
                status = YokiFrameRunStatus.Errored;
            }
            else if (active.Task.IsCanceled)
            {
                status = YokiFrameRunStatus.Cancelled;
            }
            else if (!IsTerminalStatus(status))
            {
                status = YokiFrameRunStatus.Unknown;
            }

            result.Status = status;
            result.Frames = active.Context == null ? result.Frames : active.Context.Frames;
            result.StaleContextCalls = active.Context == null ? result.StaleContextCalls : active.Context.StaleContextCalls;
            result.DurationMs = (long)(now - (record.StartedAtUtc ?? record.SubmittedAtUtc)).TotalMilliseconds;

            record.State = status;
            record.UpdatedAtUtc = now;
            record.StaleContextCalls = result.StaleContextCalls;
            record.ResultPath = mStore.SaveResult(record.RunId, result);
            record.Result = result;
            record.AddStep("complete", status.ToString(), "frames=" + result.Frames);
            mStore.Save(record);

            if (active.Context != null)
            {
                active.Context.Invalidate();
            }

            lock (mSync)
            {
                mActive.Remove(record.RunId);
            }
        }

        private static bool IsTerminalStatus(YokiFrameRunStatus status)
        {
            switch (status)
            {
                case YokiFrameRunStatus.Passed:
                case YokiFrameRunStatus.Failed:
                case YokiFrameRunStatus.Errored:
                case YokiFrameRunStatus.Cancelled:
                case YokiFrameRunStatus.Timeout:
                case YokiFrameRunStatus.Detached:
                case YokiFrameRunStatus.Unknown:
                case YokiFrameRunStatus.CompileFailed:
                    return true;
                default:
                    return false;
            }
        }

        private static YokiFrameRunResult ReadTaskResult(Task<YokiFrameRunResult> task)
        {
            if (task == null)
            {
                return new YokiFrameRunResult { Status = YokiFrameRunStatus.Unknown };
            }

            if (task.IsFaulted)
            {
                Exception exception = task.Exception == null ? null : task.Exception.GetBaseException();
                return new YokiFrameRunResult
                {
                    Status = YokiFrameRunStatus.Errored,
                    ExceptionType = exception == null ? string.Empty : exception.GetType().FullName,
                    ExceptionMessage = exception == null ? string.Empty : exception.Message,
                    ExceptionStack = exception == null ? string.Empty : exception.StackTrace
                };
            }

            if (task.IsCanceled)
            {
                return new YokiFrameRunResult { Status = YokiFrameRunStatus.Cancelled };
            }

            return task.Result ?? new YokiFrameRunResult { Status = YokiFrameRunStatus.Unknown };
        }

        /// <summary>
        /// 判断本轮是否需要扫描待认领运行：提交后立即扫描，否则按固定间隔扫描，
        /// 避免宿主每帧 tick 都做一次目录枚举。
        /// </summary>
        /// <returns>需要扫描时返回 true。</returns>
        private bool ShouldScanForQueuedRuns()
        {
            DateTime now = DateTime.UtcNow;
            if (mPendingScan || (now - mLastScanUtc).TotalMilliseconds >= SCAN_INTERVAL_MS)
            {
                mPendingScan = false;
                mLastScanUtc = now;
                return true;
            }

            return false;
        }

        private void ClaimNextQueued()
        {
            IReadOnlyList<YokiFrameEngineRunRecord> records = mStore.ReadAll();
            for (var index = 0; index < records.Count; index++)
            {
                if (records[index].Kind != "script" || records[index].OwnerHostId != mOwnerHostId)
                    continue;
                if (records[index].State != YokiFrameRunStatus.Queued)
                {
                    // 终态记录不再需要认领文件（惰性清理）。
                    mStore.ReleaseClaim(records[index].RunId);
                    continue;
                }
                if (!mPendingWork.ContainsKey(records[index].RunId))
                    continue;

                // 跨进程互斥：同一项目目录下可能有多个宿主（旧会话、另一个编辑器）在同一 tick 扫描。
                if (!mStore.TryClaim(records[index].RunId, SessionId, Generation, CLAIM_LEASE, out YokiFrameEngineRunRecord claimed))
                {
                    continue;
                }

                ClaimWork(claimed, DateTime.UtcNow);
                return;
            }
        }

        private void ClaimWork(YokiFrameEngineRunRecord record, DateTime now)
        {
            if (!mPendingWork.TryGetValue(record.RunId, out var factory)) return;
            mPendingWork.Remove(record.RunId);
            if (record.OwnerSessionId != SessionId || record.Generation != Generation
                || (mHost != null && record.Target != mHost.HostTarget))
            {
                record.State = YokiFrameRunStatus.Unknown;
                record.Note = "Host identity or active target changed before execution.";
                record.UpdatedAtUtc = now;
                mStore.Save(record);
                return;
            }
            record.State = YokiFrameRunStatus.Running;
            record.StartedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.Attempt++;
            record.AddStep("claim", "Running", "transient work");
            mStore.Save(record);
            IYokiFrameEngineRunWork work = null;
            Task<YokiFrameRunResult> task;
            try
            {
                work = factory();
                task = work.Start();
                if (work.IsCompiling)
                {
                    record.State = YokiFrameRunStatus.Compiling;
                    mStore.Save(record);
                }
            }
            catch (Exception exception)
            {
                task = Task.FromException<YokiFrameRunResult>(exception);
            }
            mActive[record.RunId] = new ActiveRun
            {
                Record = record, Context = work, Task = task,
                DeadlineUtc = now.AddMilliseconds(record.TimeoutMs)
            };
        }
    }
}
#endif
