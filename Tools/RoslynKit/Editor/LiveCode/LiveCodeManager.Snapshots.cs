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
        private readonly LiveSnapshotStore mSnapshots;
        private bool mRestoring;

        /// <summary>
        /// 捕获当前全部行为快照并写入存储。补丁不能恢复，失效行为记入条目错误而不是中断整份快照。
        /// </summary>
        /// <param name="guard">宿主状态检查。有挂起的批量、恢复或其他操作时拒绝。</param>
        /// <returns>刚保存的快照。</returns>
        public LiveSnapshot Snapshot(Action guard)
        {
            Guard(guard);
            ILiveSnapshotHost host = RequireSnapshotHost();
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before taking a snapshot.");
            var current = mState();
            var snapshot = new LiveSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString("N"), Project = mSnapshots.ProjectIdentity,
                Engine = current.EngineKind, EngineVersion = current.EngineVersion,
                SessionId = current.SessionId, Generation = current.Generation, Target = current.ActiveTarget
            };
            var keys = new Dictionary<object, string>();
            foreach (Entry entry in mEntries.Values)
                if (entry.Target != null && !keys.ContainsKey(entry.Target))
                    keys.Add(entry.Target, entry.Handle.Id + ":target");
            foreach (Entry entry in mEntries.Values) snapshot.mEntries.Add(CaptureSnapshotEntry(host, entry, keys));
            mSnapshots.Save(snapshot);
            return snapshot;
        }

        /// <summary>捕获一条行为。失败时保留空状态和截断到 512 字符的错误。</summary>
        /// <param name="host">快照宿主。</param>
        /// <param name="entry">当前行为。</param>
        /// <param name="keys">对象到稳定键的映射，供引用去重。</param>
        /// <returns>快照条目。</returns>
        private LiveSnapshotEntry CaptureSnapshotEntry(
            ILiveSnapshotHost host, Entry entry, Dictionary<object, string> keys)
        {
            var item = new LiveSnapshotEntry
            {
                Id = entry.Handle.Id, ClassName = entry.ClassName ?? "",
                SourceHash = entry.Handle.SourceHash, Error = ""
            };
            try
            {
                if (entry.Handle.Kind != "behaviour") throw new NotSupportedException("Method patches are not recoverable.");
                if (!mHost.IsAlive(entry.Attachment)) throw new InvalidOperationException("Behaviour is unavailable or faulted.");
                item.State = host.CaptureSnapshot(item.Id, entry.Attachment, (value, suggested) =>
                {
                    if (!keys.TryGetValue(value, out string key)) keys.Add(value, key = suggested);
                    return key;
                });
                LiveSnapshotStore.ValidateSize(item.State.Fields, LiveSnapshotStore.MaxFieldBytes);
            }
            catch (Exception exception)
            {
                item.State = null;
                item.Error = exception.Message.Length > 512 ? exception.Message.Substring(0, 512) : exception.Message;
            }

            return item;
        }

        /// <summary>读取已保存的快照，不恢复行为。</summary>
        /// <param name="snapshotId">快照 ID。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <returns>快照。</returns>
        public LiveSnapshot ReadSnapshot(string snapshotId, Action guard)
        {
            Guard(guard);
            RequireSnapshotHost();
            return mSnapshots.Read(snapshotId);
        }

        /// <summary>
        /// 把快照恢复成一批新行为。先编译再解析对象，全部准备好后才激活。
        /// 失败时释放已准备的附件。调用方解析器造成的对象重建不回滚。
        /// </summary>
        /// <param name="snapshotId">快照 ID。</param>
        /// <param name="sourceProvider">按条目提供与源哈希一致的成员源码。</param>
        /// <param name="resolver">运行时对象解析器，可为 null。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        /// <returns>逐项恢复结果。</returns>
        public async Task<LiveRestoreResult> Restore(string snapshotId,
            Func<LiveSnapshotEntry, string> sourceProvider,
            Func<LiveObjectIdentity, object> resolver, Action guard, CancellationToken token)
        {
            Guard(guard);
            ILiveSnapshotHost host = RequireSnapshotHost();
            EnsureRestoreIdle();
            if (sourceProvider == null) throw new ArgumentNullException(nameof(sourceProvider));
            RestoreWork work = BeginRestore(snapshotId);
            try
            {
                ValidateRestore(work, host, guard, token);
                List<string> sources = ReadRestoreSources(work, sourceProvider, guard, token);
                List<CompiledBehaviour> images = await CompileRestore(work, sources, guard, token);
                ResolvedRestore resolved = ResolveRestoreGraph(work, host, resolver, guard, token);
                PrepareRestore(work, sources, images, resolved.Targets, guard, token);
                ApplyRestoreFields(work, host, resolved.References, guard, token);
                ActivateRestore(work, guard, token);
            }
            catch (Exception exception)
            {
                RollbackRestore(work, exception);
            }
            finally { mRestoring = false; }

            return work.Result;
        }

        /// <summary>有恢复、批量或其他挂起操作时拒绝新的恢复。</summary>
        private void EnsureRestoreIdle()
        {
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before restoring.");
        }

        /// <summary>读取快照并占用恢复标记。读取失败时不会占用该标记。</summary>
        /// <param name="snapshotId">快照 ID。</param>
        /// <returns>恢复工作状态。</returns>
        private RestoreWork BeginRestore(string snapshotId)
        {
            LiveSnapshot snapshot = mSnapshots.Read(snapshotId);
            var current = mState();
            var result = new LiveRestoreResult { RequiredAssemblies = snapshot.Entries.Count };
            foreach (LiveSnapshotEntry item in snapshot.Entries) result.mItems.Add(new LiveRestoreItem { Id = item.Id });
            var work = new RestoreWork
            {
                Snapshot = snapshot,
                Result = result,
                Scope = new RoslynDomainState
                {
                    SessionId = current.SessionId, Generation = current.Generation, ActiveTarget = current.ActiveTarget
                },
                Epoch = mEpoch,
                Prepared = new List<Entry>(),
                ActiveIndex = -1
            };
            mRestoring = true;
            return work;
        }

        /// <summary>确认快照完整、引擎和目标匹配，且不会超过 64 个句柄。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="host">快照宿主。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void ValidateRestore(RestoreWork work, ILiveSnapshotHost host, Action guard, CancellationToken token)
        {
            CheckRestore(work.Scope, work.Epoch, guard, token);
            RoslynDomainState current = mState();
            if (!work.Snapshot.Complete) throw new InvalidOperationException("Snapshot is incomplete; inspect entry errors.");
            if (work.Snapshot.Engine != current.EngineKind || work.Snapshot.EngineVersion != current.EngineVersion
                || work.Snapshot.Target != current.ActiveTarget)
                throw new InvalidOperationException("Snapshot engine/version/target does not match this host.");
            if (work.Snapshot.Entries.Count + mEntries.Count > 64)
                throw new InvalidOperationException("The live code handle limit is 64.");
            foreach (LiveSnapshotEntry item in work.Snapshot.Entries)
            {
                if (mEntries.ContainsKey(item.Id)) throw new InvalidOperationException("Live ID already exists: " + item.Id);
                host.ValidateSnapshotState(item.State);
            }

            mBudget.EnsureAvailable(work.Snapshot.Entries.Count, 0);
        }

        /// <summary>向调用方索取源码并核对哈希。索取本身可能运行用户代码。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="sourceProvider">源码提供者。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        /// <returns>与快照条目顺序一致的成员源码。</returns>
        private List<string> ReadRestoreSources(RestoreWork work, Func<LiveSnapshotEntry, string> sourceProvider,
            Action guard, CancellationToken token)
        {
            var sources = new List<string>();
            foreach (LiveSnapshotEntry item in work.Snapshot.Entries)
            {
                work.ActiveIndex++;
                work.Result.UserCodeMayHaveRun = true;
                string members = sourceProvider(item);
                CheckRestore(work.Scope, work.Epoch, guard, token);
                LiveSnapshotStore.ValidateSize(members, 128 * 1024);
                if (members == null || Hash(members) != item.SourceHash)
                    throw new InvalidOperationException("Source hash mismatch: " + item.Id);
                sources.Add(members);
            }

            return sources;
        }

        /// <summary>编译全部镜像，不加载。精确字节预检发生在任何用户构造之前。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="sources">成员源码。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        /// <returns>编译镜像。</returns>
        private async Task<List<CompiledBehaviour>> CompileRestore(RestoreWork work, List<string> sources,
            Action guard, CancellationToken token)
        {
            work.Result.Stage = "compile";
            var images = new List<CompiledBehaviour>();
            for (int index = 0; index < sources.Count; index++)
            {
                work.ActiveIndex = index;
                CompiledBehaviour image = await CompileImage(mHost.WrapBehaviour(
                    work.Snapshot.Entries[index].ClassName, sources[index], false),
                    work.Scope, work.Epoch, guard, token);
                work.Result.RequiredBytes += image.Bytes;
                mBudget.EnsureAvailable(sources.Count, work.Result.RequiredBytes);
                images.Add(image);
            }

            return images;
        }

        /// <summary>解析目标和字段引用。同一个键必须指向同一份身份。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="host">快照宿主。</param>
        /// <param name="resolver">调用方解析器。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        /// <returns>目标对象和字段引用。</returns>
        private ResolvedRestore ResolveRestoreGraph(RestoreWork work, ILiveSnapshotHost host,
            Func<LiveObjectIdentity, object> resolver, Action guard, CancellationToken token)
        {
            work.Result.Stage = "resolve";
            var resolved = new ResolvedRestore();
            for (int index = 0; index < work.Snapshot.Entries.Count; index++)
            {
                work.ActiveIndex = index;
                resolved.Targets.Add(ResolveRestoreObject(
                    work.Snapshot.Entries[index].State.Target, work, host, resolver, resolved, guard, token));
            }

            for (int index = 0; index < work.Snapshot.Entries.Count; index++)
            {
                work.ActiveIndex = index;
                resolved.References.Add(ResolveRestoreFields(
                    work.Snapshot.Entries[index], work, host, resolver, resolved, guard, token));
            }

            return resolved;
        }

        /// <summary>解析一条行为的字段引用。</summary>
        /// <param name="item">快照条目。</param>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="host">快照宿主。</param>
        /// <param name="resolver">调用方解析器。</param>
        /// <param name="resolved">已经解析的对象。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        /// <returns>字段名到对象的映射。</returns>
        private Dictionary<string, object> ResolveRestoreFields(LiveSnapshotEntry item, RestoreWork work,
            ILiveSnapshotHost host, Func<LiveObjectIdentity, object> resolver, ResolvedRestore resolved,
            Action guard, CancellationToken token)
        {
            var fields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (LiveSnapshotReference reference in item.State.References)
            {
                fields.Add(reference.Field, ResolveRestoreObject(
                    reference.Identity, work, host, resolver, resolved, guard, token));
            }

            return fields;
        }

        /// <summary>
        /// 解析一个快照对象。没有全局 ID 且调用方提供了解析器时，记为可能运行了用户代码。
        /// 调用方重建出的对象不随恢复失败回滚。
        /// </summary>
        /// <param name="identity">对象身份。</param>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="host">快照宿主。</param>
        /// <param name="resolver">调用方解析器。</param>
        /// <param name="resolved">已经解析的对象。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        /// <returns>解析出的对象。</returns>
        private object ResolveRestoreObject(LiveObjectIdentity identity, RestoreWork work, ILiveSnapshotHost host,
            Func<LiveObjectIdentity, object> resolver, ResolvedRestore resolved, Action guard, CancellationToken token)
        {
            CheckRestore(work.Scope, work.Epoch, guard, token);
            if (resolved.Identities.TryGetValue(identity.Key, out LiveObjectIdentity known))
            {
                if (known.GlobalId != identity.GlobalId || known.ScenePath != identity.ScenePath
                    || known.TypeName != identity.TypeName)
                    throw new InvalidOperationException("Conflicting object key: " + identity.Key);
                return resolved.Objects[identity.Key];
            }

            // A runtime resolver may rebuild objects. Those caller-owned effects are not rolled back.
            if (string.IsNullOrEmpty(identity.GlobalId) && resolver != null) work.Result.UserCodeMayHaveRun = true;
            object value = host.ResolveSnapshotObject(identity, resolver);
            CheckRestore(work.Scope, work.Epoch, guard, token);
            if (value == null) throw new InvalidOperationException("Object unavailable: " + identity.Key);
            resolved.Identities.Add(identity.Key, identity);
            resolved.Objects.Add(identity.Key, value);
            return value;
        }

        /// <summary>加载程序集并准备行为，暂不写字段、也不激活。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="sources">成员源码。</param>
        /// <param name="images">编译镜像。</param>
        /// <param name="targets">解析出的目标。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void PrepareRestore(RestoreWork work, List<string> sources, List<CompiledBehaviour> images,
            List<object> targets, Action guard, CancellationToken token)
        {
            work.Result.Stage = "prepare";
            mBudget.EnsureAvailable(sources.Count, work.Result.RequiredBytes);
            for (int index = 0; index < sources.Count; index++)
            {
                work.ActiveIndex = index;
                CheckRestore(work.Scope, work.Epoch, guard, token);
                LiveSnapshotEntry item = work.Snapshot.Entries[index];
                mBudget.Reserve(images[index].Bytes);
                Assembly assembly = mCompiler.LoadAssembly(images[index].Pe, images[index].Symbols);
                work.Result.UserCodeMayHaveRun = true;
                IDisposable attachment = mHost.Prepare(item.Id, targets[index], assembly.GetType(item.ClassName, true), "");
                work.Prepared.Add(new Entry
                {
                    Handle = MakeHandle(item.Id, "behaviour", sources[index], work.Scope, null),
                    Target = targets[index], Members = sources[index], ClassName = item.ClassName, Attachment = attachment
                });
                work.Result.mItems[index].Status = "prepared";
            }
        }

        /// <summary>在激活前写回字段，使跨行为调用能看到完整批次。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="host">快照宿主。</param>
        /// <param name="references">每条行为的字段引用。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void ApplyRestoreFields(RestoreWork work, ILiveSnapshotHost host,
            List<Dictionary<string, object>> references, Action guard, CancellationToken token)
        {
            work.Result.Stage = "fields";
            for (int index = 0; index < work.Prepared.Count; index++)
            {
                work.ActiveIndex = index;
                CheckRestore(work.Scope, work.Epoch, guard, token);
                host.RestoreSnapshotFields(
                    work.Prepared[index].Attachment, work.Snapshot.Entries[index].State.Fields, references[index]);
            }
        }

        /// <summary>先发布全部 ID，再激活。任一行为失效则整批失败。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void ActivateRestore(RestoreWork work, Action guard, CancellationToken token)
        {
            // Publish all IDs before Awake so cross-behaviour calls see the complete, restored batch.
            foreach (Entry entry in work.Prepared) mEntries.Add(entry.Handle.Id, entry);
            work.Result.Stage = "activate";
            for (int index = 0; index < work.Prepared.Count; index++)
            {
                work.ActiveIndex = index;
                CheckRestore(work.Scope, work.Epoch, guard, token);
                mHost.Activate(work.Prepared[index].Attachment);
                if (!mHost.IsAlive(work.Prepared[index].Attachment))
                    throw new InvalidOperationException("Behaviour faulted during activation.");
                CheckRestore(work.Scope, work.Epoch, guard, token);
                work.Result.mItems[index].Status = "restored";
            }

            EnsureRestoreAlive(work);
            work.Result.Stage = "complete";
            foreach (Entry entry in work.Prepared) mRevisions[entry.Handle.Id] = entry.Handle.Revision;
            work.Result.Success = true;
        }

        /// <summary>激活结束后再确认没有行为在批次中途失效。</summary>
        /// <param name="work">恢复工作状态。</param>
        private void EnsureRestoreAlive(RestoreWork work)
        {
            for (int index = 0; index < work.Prepared.Count; index++)
            {
                work.ActiveIndex = index;
                if (!mHost.IsAlive(work.Prepared[index].Attachment))
                    throw new InvalidOperationException("A restored behaviour became unavailable during batch activation.");
            }
        }

        /// <summary>释放已准备的附件，并把已发布的项从路由表移除。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="exception">失败原因。</param>
        private void RollbackRestore(RestoreWork work, Exception exception)
        {
            work.Result.Error = exception.Message;
            if (work.ActiveIndex >= 0 && work.ActiveIndex < work.Result.mItems.Count)
            {
                work.Result.mItems[work.ActiveIndex].Status = "failed";
                work.Result.mItems[work.ActiveIndex].Error = exception.Message;
            }

            for (int index = work.Prepared.Count - 1; index >= 0; index--)
                DisposeRestored(work, index);
        }

        /// <summary>释放一条已准备行为。清理失败记为 cleanupFailed，不中断其余项。</summary>
        /// <param name="work">恢复工作状态。</param>
        /// <param name="index">准备列表下标。</param>
        private void DisposeRestored(RestoreWork work, int index)
        {
            Entry entry = work.Prepared[index];
            try
            {
                entry.Attachment.Dispose();
                if (mEntries.TryGetValue(entry.Handle.Id, out Entry installed) && ReferenceEquals(installed, entry))
                    mEntries.Remove(entry.Handle.Id);
                work.Result.mItems[index].Status = "rolledBack";
            }
            catch (Exception cleanup)
            {
                work.Result.mItems[index].Status = "cleanupFailed";
                work.Result.mItems[index].Error += " Cleanup: " + cleanup.Message;
            }
        }

        /// <summary>要求当前宿主实现快照端口。没有快照存储或宿主不支持时抛出。</summary>
        /// <returns>快照宿主。</returns>
        private ILiveSnapshotHost RequireSnapshotHost()
        {
            if (mSnapshots == null || !(mHost is ILiveSnapshotHost host))
                throw new NotSupportedException("This host has no live snapshot adapter.");
            return host;
        }

        /// <summary>取消或会话变化时中断恢复。</summary>
        /// <param name="scope">开始时的域状态。</param>
        /// <param name="epoch">开始时的世代。</param>
        /// <param name="guard">宿主状态检查。</param>
        /// <param name="token">取消标记。</param>
        private void CheckRestore(RoslynDomainState scope, int epoch, Action guard, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Check(scope, epoch, guard);
        }

        /// <summary>一次恢复已经解析出的对象。只在恢复流程内部使用。</summary>
        private sealed class ResolvedRestore
        {
            public readonly List<object> Targets = new List<object>();
            public readonly List<Dictionary<string, object>> References = new List<Dictionary<string, object>>();
            public readonly Dictionary<string, LiveObjectIdentity> Identities =
                new Dictionary<string, LiveObjectIdentity>(StringComparer.Ordinal);
            public readonly Dictionary<string, object> Objects =
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        /// <summary>一次恢复的阶段状态。</summary>
        private sealed class RestoreWork
        {
            public LiveSnapshot Snapshot;
            public LiveRestoreResult Result;
            public RoslynDomainState Scope;
            public int Epoch;
            public List<Entry> Prepared;
            public int ActiveIndex;
        }
    }
}
#endif
