#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>Key is scoped to a snapshot, never an old-domain instance ID.</summary>
    public sealed class LiveObjectIdentity
    {
        public string Key { get; }
        public string GlobalId { get; }
        public string ScenePath { get; }
        public string TypeName { get; }
        public string Name { get; }
        public LiveObjectIdentity(string key, string globalId, string scenePath, string typeName, string name)
        {
            Key = key; GlobalId = globalId; ScenePath = scenePath; TypeName = typeName; Name = name;
        }
    }

    public sealed class LiveSnapshotReference
    {
        public string Field { get; }
        public LiveObjectIdentity Identity { get; }
        public LiveSnapshotReference(string field, LiveObjectIdentity identity)
        {
            Field = field; Identity = identity;
        }
    }

    public sealed class LiveSnapshotState
    {
        public LiveObjectIdentity Target { get; }
        public string Fields { get; }
        public IReadOnlyList<LiveSnapshotReference> References { get; }
        public LiveSnapshotState(LiveObjectIdentity target, string fields,
            IEnumerable<LiveSnapshotReference> references)
        {
            Target = target; Fields = fields;
            References = new List<LiveSnapshotReference>(references).AsReadOnly();
        }
    }

    /// <summary>Adapters capture fields only; resolution must never reuse old instance IDs.</summary>
    public interface ILiveSnapshotHost
    {
        LiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject);
        void ValidateSnapshotState(LiveSnapshotState state);
        object ResolveSnapshotObject(LiveObjectIdentity identity,
            Func<LiveObjectIdentity, object> resolver);
        void RestoreSnapshotFields(IDisposable attachment, string fields, IReadOnlyDictionary<string, object> references);
    }

    public sealed class LiveSnapshotEntry
    {
        public string Id { get; internal set; }
        public string ClassName { get; internal set; }
        public string SourceHash { get; internal set; }
        public string Error { get; internal set; }
        public LiveSnapshotState State { get; internal set; }
    }

    public sealed class LiveSnapshot
    {
        public string SnapshotId { get; internal set; }
        public string Project { get; internal set; }
        public string Engine { get; internal set; }
        public string EngineVersion { get; internal set; }
        public string SessionId { get; internal set; }
        public long Generation { get; internal set; }
        public string Target { get; internal set; }
        public bool Complete
        {
            get
            {
                foreach (var entry in mEntries) if (!string.IsNullOrEmpty(entry.Error)) return false;
                return true;
            }
        }
        internal readonly List<LiveSnapshotEntry> mEntries = new List<LiveSnapshotEntry>();
        public IReadOnlyList<LiveSnapshotEntry> Entries => mEntries.AsReadOnly();
    }

    public sealed class LiveRestoreItem
    {
        public string Id { get; internal set; }
        public string Status { get; internal set; } = "notStarted";
        public string Error { get; internal set; } = "";
    }

    public sealed class LiveRestoreResult
    {
        public bool Success { get; internal set; }
        public string Stage { get; internal set; } = "validation";
        public string Error { get; internal set; } = "";
        public bool UserCodeMayHaveRun { get; internal set; }
        public int RequiredAssemblies { get; internal set; }
        public long RequiredBytes { get; internal set; }
        internal readonly List<LiveRestoreItem> mItems = new List<LiveRestoreItem>();
        public IReadOnlyList<LiveRestoreItem> Items => mItems.AsReadOnly();
    }
}
#endif
