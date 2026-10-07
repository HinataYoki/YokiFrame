#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed partial class RoslynRunScheduler
    {
        private void StepRun(ActiveRun active, bool blocked)
        {
            RoslynRunRecord record = active.Record;
            DateTime now = DateTime.UtcNow;

            if (active.Task != null && active.Task.IsCompleted)
            {
                Finalize(active, now);
                return;
            }

            if (record.State == RunStatus.Compiling
                && active.Context is IRoslynRunWork work && !work.IsCompiling)
            {
                record.State = RunStatus.Running;
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
                Detach(active, now, RunStatus.CancelRequested, "cancel grace expired");
                return;
            }

            if (record.TimeoutRequestedAtUtc.HasValue
                && now - record.TimeoutRequestedAtUtc.Value >= TimeSpan.FromMilliseconds(graceMs))
            {
                Detach(active, now, RunStatus.TimeoutRequested, "timeout grace expired");
                return;
            }

            if (active.Context != null)
            {
                active.Context.AdvanceFrame();
            }
        }

        private void RequestCancel(ActiveRun active, DateTime now, string reason)
        {
            RoslynRunRecord record = active.Record;
            record.State = RunStatus.CancelRequested;
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
            RoslynRunRecord record = active.Record;
            if (record.CancelRequestedAtUtc.HasValue)
            {
                return;
            }

            record.State = RunStatus.TimeoutRequested;
            record.TimeoutRequestedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.AddStep("timeout", "TimeoutRequested", "deadline reached");
            mStore.Save(record);
            if (active.Context != null)
            {
                active.Context.Invalidate();
            }
        }

        private void Detach(ActiveRun active, DateTime now, RunStatus requested, string reason)
        {
            RoslynRunRecord record = active.Record;
            record.State = RunStatus.Detached;
            record.DetachedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.Note = "framework stopped waiting (" + reason + "); user code may still be running.";
            record.StaleContextCalls = active.Context == null ? 0 : active.Context.StaleContextCalls;
            record.AddStep("detach", "Detached", requested.ToString());
            mStore.Save(record);
            lock (mSync)
            {
                mStore.ReleaseClaim(record.RunId);
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

                RunResult late = ReadTaskResult(detached.Task);
                detached.Record.LateResultPath = mStore.SaveResult(detached.Record.RunId + "-late", late);
                detached.Record.LateCompletionAtUtc = DateTime.UtcNow;
                detached.Record.UpdatedAtUtc = DateTime.UtcNow;
                detached.Record.AddStep("late-completion", detached.Record.State.ToString(), "terminal state kept");
                mStore.Save(detached.Record);
                mDetached.RemoveAt(index);
            }
        }

        /// <summary>把已完成任务收成终态记录，并释放认领。</summary>
        /// <param name="active">正在运行的任务。</param>
        /// <param name="now">当前时间。</param>
        private void Finalize(ActiveRun active, DateTime now)
        {
            RoslynRunRecord record = active.Record;
            RunResult result = ReadTaskResult(active.Task);
            ApplyTerminalResult(active, result, ResolveTerminalStatus(active, result), now);
            mStore.ReleaseClaim(record.RunId);
            if (active.Context != null) active.Context.Invalidate();
            lock (mSync) { mActive.Remove(record.RunId); }
        }

        /// <summary>按取消、超时和任务状态决定终态，避免用户结果覆盖框架已请求的终止。</summary>
        /// <param name="active">正在运行的任务。</param>
        /// <param name="result">任务结果。</param>
        /// <returns>应写入记录的终态。</returns>
        private static RunStatus ResolveTerminalStatus(ActiveRun active, RunResult result)
        {
            RoslynRunRecord record = active.Record;
            if (record.CancelRequestedAtUtc.HasValue) return RunStatus.Cancelled;
            if (record.TimeoutRequestedAtUtc.HasValue) return RunStatus.Timeout;
            if (active.Task.IsCanceled) return RunStatus.Cancelled;
            if (active.Task.IsFaulted) return RunStatus.Errored;
            return IsTerminalStatus(result.Status) ? result.Status : RunStatus.Unknown;
        }

        /// <summary>写入终态结果和运行记录。</summary>
        /// <param name="active">正在运行的任务。</param>
        /// <param name="result">任务结果。</param>
        /// <param name="status">终态。</param>
        /// <param name="now">当前时间。</param>
        private void ApplyTerminalResult(ActiveRun active, RunResult result, RunStatus status, DateTime now)
        {
            RoslynRunRecord record = active.Record;
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
        }

        private static bool IsTerminalStatus(RunStatus status)
        {
            switch (status)
            {
                case RunStatus.Passed:
                case RunStatus.Failed:
                case RunStatus.Errored:
                case RunStatus.Cancelled:
                case RunStatus.Timeout:
                case RunStatus.Detached:
                case RunStatus.Unknown:
                case RunStatus.CompileFailed:
                    return true;
                default:
                    return false;
            }
        }

        private static RunResult ReadTaskResult(Task<RunResult> task)
        {
            if (task == null)
            {
                return new RunResult { Status = RunStatus.Unknown };
            }

            if (task.IsFaulted)
            {
                Exception exception = task.Exception == null ? null : task.Exception.GetBaseException();
                return new RunResult
                {
                    Status = RunStatus.Errored,
                    ExceptionType = exception == null ? string.Empty : exception.GetType().FullName,
                    ExceptionMessage = exception == null ? string.Empty : exception.Message,
                    ExceptionStack = exception == null ? string.Empty : exception.StackTrace
                };
            }

            if (task.IsCanceled)
            {
                return new RunResult { Status = RunStatus.Cancelled };
            }

            return task.Result ?? new RunResult { Status = RunStatus.Unknown };
        }

        /// <summary>
        /// 判断本轮是否需要扫描待认领运行：提交后立即扫描，否则按固定间隔扫描，
        /// 避免宿主每帧 tick 都做一次目录枚举。
        /// </summary>
        /// <returns>需要扫描时返回 true。</returns>
        private bool ShouldScanForQueuedRuns()
        {
            if (mPendingWork.Count == 0) return false;
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
            // Only this domain's in-memory factories can execute. History is not an execution queue.
            var pending = new List<string>(mPendingWork.Keys);
            foreach (string runId in pending)
            {
                if (!mStore.TryReadRun(runId, out var record)) continue;
                if (record.Kind != "script" || record.OwnerHostId != mOwnerHostId
                    || record.State != RunStatus.Queued)
                {
                    mPendingWork.Remove(runId);
                    continue;
                }

                // 跨进程互斥：同一项目目录下可能有多个宿主（旧会话、另一个编辑器）在同一 tick 扫描。
                if (!mStore.TryClaim(runId, SessionId, Generation, CLAIM_LEASE, out RoslynRunRecord claimed))
                {
                    continue;
                }

                ClaimWork(claimed, DateTime.UtcNow);
                return;
            }
        }

        /// <summary>认领一条仍属于本域的内存任务；身份变化时记为未知且不执行。</summary>
        /// <param name="record">已认领的运行记录。</param>
        /// <param name="now">当前时间。</param>
        private void ClaimWork(RoslynRunRecord record, DateTime now)
        {
            if (!mPendingWork.TryGetValue(record.RunId, out var factory)) return;
            mPendingWork.Remove(record.RunId);
            if (!MatchesCurrentHost(record))
            {
                RejectStaleClaim(record, now);
                return;
            }

            MarkRunning(record, now);
            mActive[record.RunId] = StartClaimedWork(record, factory, now);
        }

        /// <summary>判断记录是否仍属于当前宿主、代次和活动目标。</summary>
        /// <param name="record">运行记录。</param>
        /// <returns>可以执行时返回 true。</returns>
        private bool MatchesCurrentHost(RoslynRunRecord record)
        {
            return record.OwnerSessionId == SessionId && record.Generation == Generation
                && (mHost == null || record.Target == mHost.HostTarget);
        }

        /// <summary>拒绝已换宿主或目标的认领，并释放租约。</summary>
        /// <param name="record">运行记录。</param>
        /// <param name="now">当前时间。</param>
        private void RejectStaleClaim(RoslynRunRecord record, DateTime now)
        {
            record.State = RunStatus.Unknown;
            record.Note = "Host identity or active target changed before execution.";
            record.UpdatedAtUtc = now;
            mStore.Save(record);
            mStore.ReleaseClaim(record.RunId);
        }

        /// <summary>把记录标成运行中；编译尚未完成时再标成编译中。</summary>
        /// <param name="record">运行记录。</param>
        /// <param name="now">当前时间。</param>
        private void MarkRunning(RoslynRunRecord record, DateTime now)
        {
            record.State = RunStatus.Running;
            record.StartedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.Attempt++;
            record.AddStep("claim", "Running", "transient work");
            mStore.Save(record);
        }

        /// <summary>启动工厂并捕获启动异常，避免认领后没有活动记录。</summary>
        /// <param name="record">运行记录。</param>
        /// <param name="factory">只存在于当前域的工作工厂。</param>
        /// <param name="now">当前时间。</param>
        /// <returns>活动运行。</returns>
        private ActiveRun StartClaimedWork(RoslynRunRecord record, Func<IRoslynRunWork> factory, DateTime now)
        {
            IRoslynRunWork work = null;
            Task<RunResult> task = StartWork(factory, record, ref work);
            return new ActiveRun
            {
                Record = record, Context = work, Task = task,
                DeadlineUtc = now.AddMilliseconds(record.TimeoutMs)
            };
        }

        /// <summary>调用工厂；启动失败时返回已失败任务。</summary>
        /// <param name="factory">工作工厂。</param>
        /// <param name="record">运行记录。</param>
        /// <param name="work">成功创建的工作；失败时保持 null。</param>
        /// <returns>用户任务。</returns>
        private Task<RunResult> StartWork(
            Func<IRoslynRunWork> factory, RoslynRunRecord record, ref IRoslynRunWork work)
        {
            try
            {
                work = factory();
                Task<RunResult> task = work.Start();
                if (work.IsCompiling)
                {
                    record.State = RunStatus.Compiling;
                    mStore.Save(record);
                }

                return task;
            }
            catch (Exception exception)
            {
                return Task.FromException<RunResult>(exception);
            }
        }
    }
}
#endif
