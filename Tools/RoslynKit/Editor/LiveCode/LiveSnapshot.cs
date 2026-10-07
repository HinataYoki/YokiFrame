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
        /// <summary>保存快照范围内的对象身份。不解析对象，也不接受旧域实例编号。</summary>
        /// <param name="key">快照内稳定键。</param>
        /// <param name="globalId">持久全局标识；运行时对象为空。</param>
        /// <param name="scenePath">场景路径。</param>
        /// <param name="typeName">类型全名。</param>
        /// <param name="name">对象名称。</param>
        public LiveObjectIdentity(string key, string globalId, string scenePath, string typeName, string name)
        {
            Key = key; GlobalId = globalId; ScenePath = scenePath; TypeName = typeName; Name = name;
        }
    }

    public sealed class LiveSnapshotReference
    {
        public string Field { get; }
        public LiveObjectIdentity Identity { get; }
        /// <summary>把字段名关联到快照对象身份。</summary>
        /// <param name="field">字段名。</param>
        /// <param name="identity">字段引用的对象身份。</param>
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
        /// <summary>复制字段文本和引用列表。调用方之后修改原序列不会影响已保存状态。</summary>
        /// <param name="target">行为所在对象。</param>
        /// <param name="fields">字段 JSON。</param>
        /// <param name="references">对象引用。</param>
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
        /// <summary>捕获活动行为的字段和对象引用。键由回调生成，不能写入旧实例编号。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="attachment">活动行为附件。</param>
        /// <param name="keyForObject">为对象生成快照键。</param>
        /// <returns>目标、字段和引用。</returns>
        LiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject);
        /// <summary>在恢复前检查快照状态可被当前宿主接受。不修改附件。</summary>
        /// <param name="state">待恢复状态。</param>
        void ValidateSnapshotState(LiveSnapshotState state);
        /// <summary>按持久标识或显式映射找到当前对象。不能用旧域实例编号恢复。</summary>
        /// <param name="identity">快照中的对象身份。</param>
        /// <param name="resolver">运行时对象映射；持久对象可不使用。</param>
        /// <returns>当前域中的对象。</returns>
        object ResolveSnapshotObject(LiveObjectIdentity identity,
            Func<LiveObjectIdentity, object> resolver);
        /// <summary>把字段 JSON 和已解析引用写回活动行为。</summary>
        /// <param name="attachment">新挂上的行为附件。</param>
        /// <param name="fields">字段 JSON。</param>
        /// <param name="references">字段名到当前对象的映射。</param>
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
        /// <summary>返回快照条目。只读包装防止调用方在保存后改集合。</summary>
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
        /// <summary>返回待恢复条目。只读包装不复制条目内容。</summary>
        public IReadOnlyList<LiveRestoreItem> Items => mItems.AsReadOnly();
    }
}
#endif
