#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed partial class LiveCodeManager
    {
        private bool mBatchAttaching;

        /// <summary>读取当前内存程序集预算。必须在 LiveCode 所属线程调用。</summary>
        /// <returns>预算快照。</returns>
        public RoslynBudgetStatus ReadBudget()
        {
            RequireThread();
            return mBudget.ReadStatus();
        }

        /// <summary>
        /// 在同一次会话里编译并挂上多段临时行为。尚未全部激活时失败会回滚；
        /// 已经激活后的清理失败不再恢复旧行为。
        /// </summary>
        /// <param name="requests">1 到 64 条挂接请求。进入等待前会先复制。</param>
        /// <param name="guard">每次可能让出线程后重新检查宿主状态。</param>
        /// <param name="token">取消标记。取消不能撤销用户代码已经造成的副作用。</param>
        /// <returns>逐项状态。全部成功时 Success 为 true。</returns>
        public async Task<LiveBatchResult> AttachMany(IReadOnlyList<LiveAttachmentRequest> requests,
            Action guard, CancellationToken token)
        {
            BatchWork work = BeginBatch(requests, guard);
            try
            {
                ValidateBatchIdentities(work);
                await CompileBatch(work, guard, token);
                PrepareBatch(work, guard, token);
                SuspendPrevious(work, guard, token);
                ActivateBatch(work, guard, token);
                CommitBatch(work, guard, token);
            }
            catch (Exception exception)
            {
                RollbackBatch(work, exception);
            }
            finally
            {
                FinishBatch(work);
            }

            return work.Result;
        }

        /// <summary>复制请求并占用批量挂接标记。校验失败时不会占用该标记。</summary>
        /// <param name="requests">调用方传入的请求。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <returns>后续阶段共用的工作状态。</returns>
        private BatchWork BeginBatch(IReadOnlyList<LiveAttachmentRequest> requests, Action guard)
        {
            Guard(guard);
            if (mHost == null) throw new NotSupportedException("This host has no live behaviour adapter.");
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before attaching a batch.");
            if (requests == null || requests.Count < 1 || requests.Count > 64)
                throw new ArgumentException("AttachMany requires 1..64 items.");
            // Copy the collection before the first await; request values are immutable.
            var batch = new List<LiveAttachmentRequest>(requests);
            var result = new LiveBatchResult { RequiredAssemblies = batch.Count };
            foreach (var item in batch) result.mItems.Add(new LiveBatchItem { Id = item == null ? "" : item.Id });
            var current = mState();
            mBatchAttaching = true;
            return new BatchWork
            {
                Guard = guard,
                Batch = batch,
                Result = result,
                Scope = new RoslynDomainState
                {
                    SessionId = current.SessionId, Generation = current.Generation, ActiveTarget = current.ActiveTarget
                },
                Epoch = mEpoch,
                Previous = new List<Entry>(),
                Prepared = new List<Entry>(),
                Suspended = new List<Entry>(),
                Images = new List<CompiledBehaviour>(),
                ActiveIndex = -1
            };
        }

        /// <summary>校验 ID、类型名和目标。新 ID 计入 64 个句柄上限，已有行为必须仍指向同一目标。</summary>
        /// <param name="work">批量工作状态。</param>
        private void ValidateBatchIdentities(BatchWork work)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int added = 0;
            for (int index = 0; index < work.Batch.Count; index++)
            {
                work.ActiveIndex = index;
                LiveAttachmentRequest item = work.Batch[index];
                if (item == null || item.Target == null) throw new ArgumentException("A live target is required.");
                ValidateName(item.Id);
                ValidateName(item.ClassName);
                if (!ids.Add(item.Id)) throw new ArgumentException("Duplicate batch ID: " + item.Id);
                if (string.IsNullOrWhiteSpace(item.Members)) throw new ArgumentException("Members must be nonempty.");
                LiveSnapshotStore.ValidateSize(item.Members, 128 * 1024);
                mEntries.TryGetValue(item.Id, out Entry old);
                if (old == null) added++;
                else if (old.Handle.Kind != "behaviour" || !ReferenceEquals(old.Target, item.Target))
                    throw new InvalidOperationException("An existing behaviour ID must keep its target: " + item.Id);
                work.Previous.Add(old);
            }

            if (mEntries.Count + added > 64) throw new InvalidOperationException("The live code handle limit is 64.");
        }

        /// <summary>先编译全部镜像，不加载。每段编译后按累计字节再检查预算。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private async Task CompileBatch(BatchWork work, Action guard, CancellationToken token)
        {
            CheckRestore(work.Scope, work.Epoch, guard, token);
            mBudget.EnsureAvailable(work.Batch.Count, 0);
            work.Result.Stage = "compile";
            for (int index = 0; index < work.Batch.Count; index++)
            {
                work.ActiveIndex = index;
                LiveAttachmentRequest item = work.Batch[index];
                CompiledBehaviour image = await CompileImage(
                    mHost.WrapBehaviour(item.ClassName, item.Members, false),
                    work.Scope, work.Epoch, guard, token);
                work.Images.Add(image);
                work.Result.RequiredBytes += image.Bytes;
                mBudget.EnsureAvailable(work.Batch.Count, work.Result.RequiredBytes);
            }
        }

        /// <summary>加载程序集并准备新行为，同时从仍存活的旧行为捕获字段。此阶段用户代码可能已经运行。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void PrepareBatch(BatchWork work, Action guard, CancellationToken token)
        {
            work.Result.Stage = "prepare";
            for (int index = 0; index < work.Batch.Count; index++)
            {
                work.ActiveIndex = index;
                CheckRestore(work.Scope, work.Epoch, guard, token);
                LiveAttachmentRequest item = work.Batch[index];
                Entry old = work.Previous[index];
                string fields = old != null && mHost.IsAlive(old.Attachment) ? mHost.CaptureState(old.Attachment) : "";
                mBudget.Reserve(work.Images[index].Bytes);
                Assembly assembly = mCompiler.LoadAssembly(work.Images[index].Pe, work.Images[index].Symbols);
                work.Result.UserCodeMayHaveRun = true;
                IDisposable attachment = mHost.Prepare(item.Id, item.Target, assembly.GetType(item.ClassName, true), fields);
                work.Prepared.Add(new Entry
                {
                    Handle = MakeHandle(item.Id, "behaviour", item.Members, work.Scope, old),
                    Target = item.Target, Members = item.Members, ClassName = item.ClassName, Attachment = attachment
                });
                work.Result.mItems[index].Status = "prepared";
            }
        }

        /// <summary>挂起仍存活的旧行为，再发布新 ID。发布后失败必须先恢复路由，再释放新附件。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void SuspendPrevious(BatchWork work, Action guard, CancellationToken token)
        {
            work.Result.Stage = "suspend";
            CheckRestore(work.Scope, work.Epoch, guard, token);
            foreach (Entry old in work.Previous)
            {
                if (old == null || !mHost.IsAlive(old.Attachment)) continue;
                work.Suspended.Add(old);
                mHost.Suspend(old.Attachment);
                CheckRestore(work.Scope, work.Epoch, guard, token);
            }

            foreach (Entry entry in work.Prepared) mEntries[entry.Handle.Id] = entry;
            work.Published = true;
        }

        /// <summary>激活全部新行为。任一行为失效则整批视为未提交，交给回滚。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void ActivateBatch(BatchWork work, Action guard, CancellationToken token)
        {
            work.Result.Stage = "activate";
            for (int index = 0; index < work.Prepared.Count; index++)
            {
                work.ActiveIndex = index;
                CheckRestore(work.Scope, work.Epoch, guard, token);
                mHost.Activate(work.Prepared[index].Attachment);
                CheckRestore(work.Scope, work.Epoch, guard, token);
            }

            foreach (Entry entry in work.Prepared)
                if (!mHost.IsAlive(entry.Attachment))
                    throw new InvalidOperationException("A batch behaviour faulted during activation.");
            work.Committed = true;
        }

        /// <summary>提交成功后释放被替换的旧附件，并写回句柄。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void CommitBatch(BatchWork work, Action guard, CancellationToken token)
        {
            foreach (Entry entry in work.Prepared) mRevisions[entry.Handle.Id] = entry.Handle.Revision;
            work.Result.Stage = "cleanup";
            for (int index = 0; index < work.Previous.Count; index++)
            {
                work.ActiveIndex = index;
                if (work.Previous[index] != null) work.Previous[index].Attachment.Dispose();
                CheckRestore(work.Scope, work.Epoch, guard, token);
                work.Result.mItems[index].Status = "attached";
                work.Result.mItems[index].Handle = work.Prepared[index].Handle;
            }

            work.Result.Success = true;
            work.Result.Stage = "complete";
        }

        /// <summary>按是否已经激活选择回滚或只清理剩余旧附件。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="exception">失败原因。</param>
        private void RollbackBatch(BatchWork work, Exception exception)
        {
            work.Result.Error = exception.Message;
            MarkBatchFailure(work, exception);
            if (!work.Committed) RollbackUncommitted(work);
            else CleanupCommittedFailure(work);
        }

        /// <summary>把当前项标成失败。已经提交时状态是清理失败，否则是挂接失败。</summary>
        /// <param name="work">批量工作状态。</param>
        /// <param name="exception">失败原因。</param>
        private static void MarkBatchFailure(BatchWork work, Exception exception)
        {
            if (work.ActiveIndex < 0 || work.ActiveIndex >= work.Result.mItems.Count) return;
            work.Result.mItems[work.ActiveIndex].Error = exception.Message;
            work.Result.mItems[work.ActiveIndex].Status = work.Committed ? "cleanupFailed" : "failed";
        }

        /// <summary>
        /// 恢复 ID 路由后再释放新附件，并重新激活被挂起的旧行为。
        /// 会话已经失效时不复活旧项，只释放附件。
        /// </summary>
        /// <param name="work">批量工作状态。</param>
        private void RollbackUncommitted(BatchWork work)
        {
            // Restore ID routing before disposal callbacks. Never resurrect entries after Clear/session invalidation.
            if (work.Published) RestorePreviousRouting(work);
            DisposePrepared(work);
            ResumeSuspended(work);
        }

        /// <summary>撤销已发布的新项。会话世代未变时放回对应的旧项。</summary>
        /// <param name="work">批量工作状态。</param>
        private void RestorePreviousRouting(BatchWork work)
        {
            for (int index = 0; index < work.Prepared.Count; index++)
            {
                Entry entry = work.Prepared[index];
                if (!mEntries.TryGetValue(entry.Handle.Id, out Entry installed) || !ReferenceEquals(installed, entry)) continue;
                mEntries.Remove(entry.Handle.Id);
                if (work.Epoch == mEpoch && work.Previous[index] != null) mEntries[entry.Handle.Id] = work.Previous[index];
            }
        }

        /// <summary>逆序释放已准备的新附件。释放失败记为 cleanupFailed，不中断其余项。</summary>
        /// <param name="work">批量工作状态。</param>
        private static void DisposePrepared(BatchWork work)
        {
            for (int index = work.Prepared.Count - 1; index >= 0; index--)
            {
                try { work.Prepared[index].Attachment.Dispose(); work.Result.mItems[index].Status = "rolledBack"; }
                catch (Exception cleanup)
                {
                    work.Result.mItems[index].Status = "cleanupFailed";
                    work.Result.mItems[index].Error += " Cleanup: " + cleanup.Message;
                }
            }
        }

        /// <summary>重新激活被挂起的旧行为。世代变化时改为释放，避免把行为留在失效会话里。</summary>
        /// <param name="work">批量工作状态。</param>
        private void ResumeSuspended(BatchWork work)
        {
            foreach (Entry old in work.Suspended)
            {
                try
                {
                    if (work.Epoch != mEpoch)
                    {
                        old.Attachment.Dispose();
                        continue;
                    }

                    Check(work.Scope, work.Epoch, work.Guard);
                    if (mHost.IsAlive(old.Attachment)) mHost.Activate(old.Attachment);
                }
                catch (Exception resume) { work.Result.Error += " Resume " + old.Handle.Id + ": " + resume.Message; }
            }
        }

        /// <summary>已经激活的替换不能回滚。只继续释放尚未清理的旧附件，并保留新句柄。</summary>
        /// <param name="work">批量工作状态。</param>
        private static void CleanupCommittedFailure(BatchWork work)
        {
            // Cleanup failure cannot roll back an already active replacement batch.
            for (int index = work.ActiveIndex + 1; index < work.Previous.Count; index++)
            {
                try
                {
                    if (work.Previous[index] != null) work.Previous[index].Attachment.Dispose();
                    work.Result.mItems[index].Status = "attached";
                }
                catch (Exception cleanup)
                {
                    work.Result.mItems[index].Status = "cleanupFailed";
                    work.Result.mItems[index].Error = cleanup.Message;
                }
            }

            for (int index = 0; index < work.Prepared.Count; index++)
                work.Result.mItems[index].Handle = work.Prepared[index].Handle;
        }

        /// <summary>写回预算并释放批量挂接标记。成功和失败都会执行。</summary>
        /// <param name="work">批量工作状态。</param>
        private void FinishBatch(BatchWork work)
        {
            work.Result.Budget = mBudget.ReadStatus();
            foreach (Entry entry in work.Prepared) entry.Handle.Budget = work.Result.Budget;
            mBatchAttaching = false;
        }

        /// <summary>一次批量挂接的阶段状态。只在 LiveCodeManager 内部传递。</summary>
        private sealed class BatchWork
        {
            public Action Guard;
            public List<LiveAttachmentRequest> Batch;
            public LiveBatchResult Result;
            public RoslynDomainState Scope;
            public int Epoch;
            public List<Entry> Previous;
            public List<Entry> Prepared;
            public List<Entry> Suspended;
            public List<CompiledBehaviour> Images;
            public bool Published;
            public bool Committed;
            public int ActiveIndex;
        }
    }
}
#endif
