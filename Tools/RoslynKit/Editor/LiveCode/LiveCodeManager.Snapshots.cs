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

        public LiveSnapshot Snapshot(Action guard)
        {
            Guard(guard);
            var host = RequireSnapshotHost();
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
            foreach (var entry in mEntries.Values)
                if (entry.Target != null && !keys.ContainsKey(entry.Target))
                    keys.Add(entry.Target, entry.Handle.Id + ":target");
            foreach (Entry entry in mEntries.Values)
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
                        if (!keys.TryGetValue(value, out var key)) keys.Add(value, key = suggested);
                        return key;
                    });
                    LiveSnapshotStore.ValidateSize(item.State.Fields, LiveSnapshotStore.MaxFieldBytes);
                }
                catch (Exception exception)
                {
                    item.State = null;
                    item.Error = exception.Message.Length > 512 ? exception.Message.Substring(0, 512) : exception.Message;
                }
                snapshot.mEntries.Add(item);
            }
            mSnapshots.Save(snapshot);
            return snapshot;
        }

        public LiveSnapshot ReadSnapshot(string snapshotId, Action guard)
        {
            Guard(guard);
            RequireSnapshotHost();
            return mSnapshots.Read(snapshotId);
        }

        public async Task<LiveRestoreResult> Restore(string snapshotId,
            Func<LiveSnapshotEntry, string> sourceProvider,
            Func<LiveObjectIdentity, object> resolver, Action guard, CancellationToken token)
        {
            Guard(guard);
            var host = RequireSnapshotHost();
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before restoring.");
            if (sourceProvider == null) throw new ArgumentNullException(nameof(sourceProvider));
            var snapshot = mSnapshots.Read(snapshotId);
            var current = mState();
            var scope = new RoslynDomainState
            {
                SessionId = current.SessionId, Generation = current.Generation, ActiveTarget = current.ActiveTarget
            };
            int epoch = mEpoch;
            var result = new LiveRestoreResult { RequiredAssemblies = snapshot.Entries.Count };
            foreach (var item in snapshot.Entries) result.mItems.Add(new LiveRestoreItem { Id = item.Id });
            var prepared = new List<Entry>();
            mRestoring = true;
            int activeIndex = -1;
            try
            {
                CheckRestore(scope, epoch, guard, token);
                if (!snapshot.Complete) throw new InvalidOperationException("Snapshot is incomplete; inspect entry errors.");
                if (snapshot.Engine != current.EngineKind || snapshot.EngineVersion != current.EngineVersion
                    || snapshot.Target != current.ActiveTarget)
                    throw new InvalidOperationException("Snapshot engine/version/target does not match this host.");
                if (snapshot.Entries.Count + mEntries.Count > 64)
                    throw new InvalidOperationException("The live code handle limit is 64.");
                foreach (var item in snapshot.Entries)
                {
                    if (mEntries.ContainsKey(item.Id)) throw new InvalidOperationException("Live ID already exists: " + item.Id);
                    host.ValidateSnapshotState(item.State);
                }
                mBudget.EnsureAvailable(snapshot.Entries.Count, 0);
                var sources = new List<string>();
                foreach (var item in snapshot.Entries)
                {
                    activeIndex++;
                    result.UserCodeMayHaveRun = true;
                    string members = sourceProvider(item);
                    CheckRestore(scope, epoch, guard, token);
                    LiveSnapshotStore.ValidateSize(members, 128 * 1024);
                    if (members == null || Hash(members) != item.SourceHash)
                        throw new InvalidOperationException("Source hash mismatch: " + item.Id);
                    sources.Add(members);
                }

                // Compile all images first, without loading. Exact byte preflight precedes any user constructor.
                result.Stage = "compile";
                var images = new List<CompiledBehaviour>();
                for (int index = 0; index < sources.Count; index++)
                {
                    activeIndex = index;
                    var image = await CompileImage(mHost.WrapBehaviour(snapshot.Entries[index].ClassName,
                        sources[index], false), scope, epoch, guard, token);
                    result.RequiredBytes += image.Bytes;
                    mBudget.EnsureAvailable(sources.Count, result.RequiredBytes);
                    images.Add(image);
                }
                result.Stage = "resolve";
                var targets = new List<object>();
                var references = new List<Dictionary<string, object>>();
                var resolved = new Dictionary<string, object>(StringComparer.Ordinal);
                var identities = new Dictionary<string, LiveObjectIdentity>(StringComparer.Ordinal);
                for (int index = 0; index < snapshot.Entries.Count; index++)
                {
                    activeIndex = index;
                    targets.Add(Resolve(snapshot.Entries[index].State.Target));
                }
                for (int index = 0; index < snapshot.Entries.Count; index++)
                {
                    activeIndex = index;
                    var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var reference in snapshot.Entries[index].State.References)
                        fields.Add(reference.Field, Resolve(reference.Identity));
                    references.Add(fields);
                }

                result.Stage = "prepare";
                mBudget.EnsureAvailable(sources.Count, result.RequiredBytes);
                for (int index = 0; index < sources.Count; index++)
                {
                    activeIndex = index;
                    CheckRestore(scope, epoch, guard, token);
                    var item = snapshot.Entries[index];
                    var image = images[index];
                    mBudget.Reserve(image.Bytes);
                    var assembly = mCompiler.LoadAssembly(image.Pe, image.Symbols);
                    result.UserCodeMayHaveRun = true;
                    var attachment = mHost.Prepare(item.Id, targets[index], assembly.GetType(item.ClassName, true), "");
                    prepared.Add(new Entry
                    {
                        Handle = MakeHandle(item.Id, "behaviour", sources[index], scope, null),
                        Target = targets[index], Members = sources[index], ClassName = item.ClassName, Attachment = attachment
                    });
                    result.mItems[index].Status = "prepared";
                }
                result.Stage = "fields";
                for (int index = 0; index < prepared.Count; index++)
                {
                    activeIndex = index;
                    CheckRestore(scope, epoch, guard, token);
                    host.RestoreSnapshotFields(prepared[index].Attachment, snapshot.Entries[index].State.Fields, references[index]);
                }
                // Publish all IDs before Awake so cross-behaviour calls see the complete, restored batch.
                foreach (var entry in prepared) mEntries.Add(entry.Handle.Id, entry);
                result.Stage = "activate";
                for (int index = 0; index < prepared.Count; index++)
                {
                    activeIndex = index;
                    CheckRestore(scope, epoch, guard, token);
                    mHost.Activate(prepared[index].Attachment);
                    if (!mHost.IsAlive(prepared[index].Attachment))
                        throw new InvalidOperationException("Behaviour faulted during activation.");
                    CheckRestore(scope, epoch, guard, token);
                    result.mItems[index].Status = "restored";
                }
                for (int index = 0; index < prepared.Count; index++)
                {
                    activeIndex = index;
                    if (!mHost.IsAlive(prepared[index].Attachment))
                        throw new InvalidOperationException("A restored behaviour became unavailable during batch activation.");
                }
                result.Stage = "complete";
                foreach (var entry in prepared) mRevisions[entry.Handle.Id] = entry.Handle.Revision;
                result.Success = true;

                object Resolve(LiveObjectIdentity identity)
                {
                    CheckRestore(scope, epoch, guard, token);
                    if (identities.TryGetValue(identity.Key, out var known))
                    {
                        if (known.GlobalId != identity.GlobalId || known.ScenePath != identity.ScenePath
                            || known.TypeName != identity.TypeName)
                            throw new InvalidOperationException("Conflicting object key: " + identity.Key);
                        return resolved[identity.Key];
                    }
                    // A runtime resolver may rebuild objects. Those caller-owned effects are not rolled back.
                    if (string.IsNullOrEmpty(identity.GlobalId) && resolver != null) result.UserCodeMayHaveRun = true;
                    object value = host.ResolveSnapshotObject(identity, resolver);
                    CheckRestore(scope, epoch, guard, token);
                    if (value == null) throw new InvalidOperationException("Object unavailable: " + identity.Key);
                    identities.Add(identity.Key, identity);
                    resolved.Add(identity.Key, value);
                    return value;
                }
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                if (activeIndex >= 0 && activeIndex < result.mItems.Count)
                {
                    result.mItems[activeIndex].Status = "failed";
                    result.mItems[activeIndex].Error = exception.Message;
                }
                for (int index = prepared.Count - 1; index >= 0; index--)
                {
                    Entry entry = prepared[index];
                    try
                    {
                        entry.Attachment.Dispose();
                        if (mEntries.TryGetValue(entry.Handle.Id, out var installed) && ReferenceEquals(installed, entry))
                            mEntries.Remove(entry.Handle.Id);
                        result.mItems[index].Status = "rolledBack";
                    }
                    catch (Exception cleanup)
                    {
                        result.mItems[index].Status = "cleanupFailed";
                        result.mItems[index].Error += " Cleanup: " + cleanup.Message;
                    }
                }
            }
            finally { mRestoring = false; }
            return result;
        }

        private ILiveSnapshotHost RequireSnapshotHost()
        {
            if (mSnapshots == null || !(mHost is ILiveSnapshotHost host))
                throw new NotSupportedException("This host has no live snapshot adapter.");
            return host;
        }

        private void CheckRestore(RoslynDomainState scope, int epoch, Action guard, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Check(scope, epoch, guard);
        }
    }
}
#endif
