#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed partial class YokiFrameLiveCodeManager
    {
        private bool mBatchAttaching;
        public YokiFrameRoslynBudgetStatus ReadBudget() { RequireThread(); return mBudget.ReadStatus(); }

        public async Task<YokiFrameLiveBatchResult> AttachMany(IReadOnlyList<YokiFrameLiveAttachmentRequest> requests,
            Action guard, CancellationToken token)
        {
            Guard(guard);
            if (mHost == null) throw new NotSupportedException("This host has no live behaviour adapter.");
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before attaching a batch.");
            if (requests == null || requests.Count < 1 || requests.Count > 64)
                throw new ArgumentException("AttachMany requires 1..64 items.");
            // Copy the collection before the first await; request values are immutable.
            var batch = new List<YokiFrameLiveAttachmentRequest>(requests);
            var result = new YokiFrameLiveBatchResult { RequiredAssemblies = batch.Count };
            foreach (var item in batch) result.mItems.Add(new YokiFrameLiveBatchItem { Id = item == null ? "" : item.Id });
            var current = mState();
            var scope = new YokiFrameEngineDomainState
            {
                SessionId = current.SessionId, Generation = current.Generation, ActiveTarget = current.ActiveTarget
            };
            int epoch = mEpoch;
            var previous = new List<Entry>();
            var prepared = new List<Entry>();
            var suspended = new List<Entry>();
            bool published = false;
            bool committed = false;
            int activeIndex = -1;
            mBatchAttaching = true;
            try
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                int added = 0;
                for (int index = 0; index < batch.Count; index++)
                {
                    activeIndex = index;
                    var item = batch[index];
                    if (item == null || item.Target == null) throw new ArgumentException("A live target is required.");
                    ValidateName(item.Id);
                    ValidateName(item.ClassName);
                    if (!ids.Add(item.Id)) throw new ArgumentException("Duplicate batch ID: " + item.Id);
                    if (string.IsNullOrWhiteSpace(item.Members)) throw new ArgumentException("Members must be nonempty.");
                    YokiFrameLiveSnapshotStore.ValidateSize(item.Members, 128 * 1024);
                    mEntries.TryGetValue(item.Id, out var old);
                    if (old == null) added++;
                    else if (old.Handle.Kind != "behaviour" || !ReferenceEquals(old.Target, item.Target))
                        throw new InvalidOperationException("An existing behaviour ID must keep its target: " + item.Id);
                    previous.Add(old);
                }
                if (mEntries.Count + added > 64) throw new InvalidOperationException("The live code handle limit is 64.");
                CheckRestore(scope, epoch, guard, token);
                mBudget.EnsureAvailable(batch.Count, 0);
                result.Stage = "compile";
                var images = new List<CompiledBehaviour>();
                for (int index = 0; index < batch.Count; index++)
                {
                    activeIndex = index;
                    var item = batch[index];
                    var image = await CompileImage(mHost.WrapBehaviour(item.ClassName, item.Members, false),
                        scope, epoch, guard, token);
                    images.Add(image);
                    result.RequiredBytes += image.Bytes;
                    mBudget.EnsureAvailable(batch.Count, result.RequiredBytes);
                }
                result.Stage = "prepare";
                for (int index = 0; index < batch.Count; index++)
                {
                    activeIndex = index;
                    CheckRestore(scope, epoch, guard, token);
                    var item = batch[index];
                    Entry old = previous[index];
                    string fields = old != null && mHost.IsAlive(old.Attachment) ? mHost.CaptureState(old.Attachment) : "";
                    mBudget.Reserve(images[index].Bytes);
                    var assembly = mCompiler.LoadAssembly(images[index].Pe, images[index].Symbols);
                    result.UserCodeMayHaveRun = true;
                    var attachment = mHost.Prepare(item.Id, item.Target, assembly.GetType(item.ClassName, true), fields);
                    prepared.Add(new Entry { Handle = MakeHandle(item.Id, "behaviour", item.Members, scope, old),
                        Target = item.Target, Members = item.Members, ClassName = item.ClassName, Attachment = attachment });
                    result.mItems[index].Status = "prepared";
                }
                result.Stage = "suspend";
                CheckRestore(scope, epoch, guard, token);
                foreach (var old in previous)
                {
                    if (old == null || !mHost.IsAlive(old.Attachment)) continue;
                    suspended.Add(old);
                    mHost.Suspend(old.Attachment);
                    CheckRestore(scope, epoch, guard, token);
                }
                foreach (var entry in prepared) mEntries[entry.Handle.Id] = entry;
                published = true;
                result.Stage = "activate";
                for (int index = 0; index < prepared.Count; index++)
                {
                    activeIndex = index;
                    CheckRestore(scope, epoch, guard, token);
                    mHost.Activate(prepared[index].Attachment);
                    CheckRestore(scope, epoch, guard, token);
                }
                foreach (var entry in prepared)
                    if (!mHost.IsAlive(entry.Attachment)) throw new InvalidOperationException("A batch behaviour faulted during activation.");
                committed = true;
                foreach (var entry in prepared) mRevisions[entry.Handle.Id] = entry.Handle.Revision;
                result.Stage = "cleanup";
                for (int index = 0; index < previous.Count; index++)
                {
                    activeIndex = index;
                    if (previous[index] != null) previous[index].Attachment.Dispose();
                    CheckRestore(scope, epoch, guard, token);
                    result.mItems[index].Status = "attached";
                    result.mItems[index].Handle = prepared[index].Handle;
                }
                result.Success = true;
                result.Stage = "complete";
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                if (activeIndex >= 0 && activeIndex < result.mItems.Count)
                {
                    result.mItems[activeIndex].Error = exception.Message;
                    result.mItems[activeIndex].Status = committed ? "cleanupFailed" : "failed";
                }
                if (!committed)
                {
                    // Restore ID routing before disposal callbacks. Never resurrect entries after Clear/session invalidation.
                    if (published)
                        for (int index = 0; index < prepared.Count; index++)
                        {
                            var entry = prepared[index];
                            if (!mEntries.TryGetValue(entry.Handle.Id, out var installed) || !ReferenceEquals(installed, entry)) continue;
                            mEntries.Remove(entry.Handle.Id);
                            if (epoch == mEpoch && previous[index] != null) mEntries[entry.Handle.Id] = previous[index];
                        }
                    for (int index = prepared.Count - 1; index >= 0; index--)
                    {
                        try { prepared[index].Attachment.Dispose(); result.mItems[index].Status = "rolledBack"; }
                        catch (Exception cleanup) { result.mItems[index].Status = "cleanupFailed"; result.mItems[index].Error += " Cleanup: " + cleanup.Message; }
                    }
                    foreach (var old in suspended)
                    {
                        try
                        {
                            if (epoch != mEpoch)
                            {
                                old.Attachment.Dispose();
                                continue;
                            }
                            Check(scope, epoch, guard);
                            if (mHost.IsAlive(old.Attachment)) mHost.Activate(old.Attachment);
                        }
                        catch (Exception resume) { result.Error += " Resume " + old.Handle.Id + ": " + resume.Message; }
                    }
                }
                else
                {
                    // Cleanup failure cannot roll back an already active replacement batch.
                    for (int index = activeIndex + 1; index < previous.Count; index++)
                    {
                        try { if (previous[index] != null) previous[index].Attachment.Dispose(); result.mItems[index].Status = "attached"; }
                        catch (Exception cleanup) { result.mItems[index].Status = "cleanupFailed"; result.mItems[index].Error = cleanup.Message; }
                    }
                    for (int index = 0; index < prepared.Count; index++) result.mItems[index].Handle = prepared[index].Handle;
                }
            }
            finally
            {
                result.Budget = mBudget.ReadStatus();
                foreach (var entry in prepared) entry.Handle.Budget = result.Budget;
                mBatchAttaching = false;
            }
            return result;
        }
    }
}
#endif
