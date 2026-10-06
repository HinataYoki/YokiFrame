#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>Key is scoped to a snapshot, never an old-domain instance ID.</summary>
    public sealed class YokiFrameLiveObjectIdentity
    {
        public string Key { get; }
        public string GlobalId { get; }
        public string ScenePath { get; }
        public string TypeName { get; }
        public string Name { get; }
        public YokiFrameLiveObjectIdentity(string key, string globalId, string scenePath, string typeName, string name)
        {
            Key = key; GlobalId = globalId; ScenePath = scenePath; TypeName = typeName; Name = name;
        }
    }

    public sealed class YokiFrameLiveSnapshotReference
    {
        public string Field { get; }
        public YokiFrameLiveObjectIdentity Identity { get; }
        public YokiFrameLiveSnapshotReference(string field, YokiFrameLiveObjectIdentity identity)
        {
            Field = field; Identity = identity;
        }
    }

    public sealed class YokiFrameLiveSnapshotState
    {
        public YokiFrameLiveObjectIdentity Target { get; }
        public string Fields { get; }
        public IReadOnlyList<YokiFrameLiveSnapshotReference> References { get; }
        public YokiFrameLiveSnapshotState(YokiFrameLiveObjectIdentity target, string fields,
            IEnumerable<YokiFrameLiveSnapshotReference> references)
        {
            Target = target; Fields = fields;
            References = new List<YokiFrameLiveSnapshotReference>(references).AsReadOnly();
        }
    }

    /// <summary>Adapters capture fields only; resolution must never reuse old instance IDs.</summary>
    public interface IYokiFrameLiveSnapshotHost
    {
        YokiFrameLiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject);
        void ValidateSnapshotState(YokiFrameLiveSnapshotState state);
        object ResolveSnapshotObject(YokiFrameLiveObjectIdentity identity,
            Func<YokiFrameLiveObjectIdentity, object> resolver);
        void RestoreSnapshotFields(IDisposable attachment, string fields, IReadOnlyDictionary<string, object> references);
    }

    public sealed class YokiFrameLiveSnapshotEntry
    {
        public string Id { get; internal set; }
        public string ClassName { get; internal set; }
        public string SourceHash { get; internal set; }
        public string Error { get; internal set; }
        public YokiFrameLiveSnapshotState State { get; internal set; }
    }

    public sealed class YokiFrameLiveSnapshot
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
        internal readonly List<YokiFrameLiveSnapshotEntry> mEntries = new List<YokiFrameLiveSnapshotEntry>();
        public IReadOnlyList<YokiFrameLiveSnapshotEntry> Entries => mEntries.AsReadOnly();
    }

    public sealed class YokiFrameLiveRestoreItem
    {
        public string Id { get; internal set; }
        public string Status { get; internal set; } = "notStarted";
        public string Error { get; internal set; } = "";
    }

    public sealed class YokiFrameLiveRestoreResult
    {
        public bool Success { get; internal set; }
        public string Stage { get; internal set; } = "validation";
        public string Error { get; internal set; } = "";
        public bool UserCodeMayHaveRun { get; internal set; }
        public int RequiredAssemblies { get; internal set; }
        public long RequiredBytes { get; internal set; }
        internal readonly List<YokiFrameLiveRestoreItem> mItems = new List<YokiFrameLiveRestoreItem>();
        public IReadOnlyList<YokiFrameLiveRestoreItem> Items => mItems.AsReadOnly();
    }
}
#endif
