#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class LiveSnapshotStore
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public const int MaxFieldBytes = 64 * 1024;
        private readonly string mRoot;
        public string ProjectIdentity { get; }

        /// <summary>规范化项目根并计算项目身份。Windows 路径先转大写，避免盘符大小写造成不同身份。</summary>
        /// <param name="projectRoot">项目根目录。</param>
        public LiveSnapshotStore(string projectRoot)
        {
            mRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string identity = Path.DirectorySeparatorChar == '\\' ? mRoot.ToUpperInvariant() : mRoot;
            ProjectIdentity = LiveCodeManager.Hash(identity);
        }

        /// <summary>把 N 格式快照标识映射到项目内 JSON 路径。不创建目录。</summary>
        /// <param name="id">32 位快照标识。</param>
        /// <returns>快照绝对路径。</returns>
        public string GetPath(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid snapshot ID.");
            return YokiFrameFilePathPolicy.CombineInside(mRoot, ".yokiframe", "engine", "live-snapshots", id + ".json");
        }

        /// <summary>先序列化并自解析，再用临时文件发布。发布失败时删除临时文件，不留下半份正式记录。</summary>
        /// <param name="snapshot">待写入快照。</param>
        internal void Save(LiveSnapshot snapshot)
        {
            string text = Serialize(snapshot);
            ValidateSize(text, MaxBytes);
            // Validate our own output before publishing; incomplete records remain explicit.
            Parse(text);
            string path = GetPath(snapshot.SnapshotId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = GetPath(Guid.NewGuid().ToString("N"));
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
                File.Move(temporary, GetPath(snapshot.SnapshotId));
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        /// <summary>读取快照并核对标识与项目身份。超过 2 MiB 的文件直接拒绝。</summary>
        /// <param name="id">期望的快照标识。</param>
        /// <returns>解析后的快照。</returns>
        public LiveSnapshot Read(string id)
        {
            string text;
            using (var stream = new FileStream(GetPath(id), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaxBytes) throw new InvalidDataException("Snapshot exceeds 2 MiB.");
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true))) text = reader.ReadToEnd();
            }
            var snapshot = Parse(text);
            if (snapshot.SnapshotId != id || snapshot.Project != ProjectIdentity)
                throw new InvalidDataException("Snapshot ID or project identity does not match.");
            return snapshot;
        }

        /// <summary>按固定字段顺序序列化快照。有错误的条目不写对象状态。</summary>
        /// <param name="snapshot">内存快照。</param>
        /// <returns>架构 1 的 JSON。</returns>
        public static string Serialize(LiveSnapshot snapshot)
        {
            var json = new RoslynJsonBuilder().StartObject().Property("schema", 1)
                .Property("snapshotId", snapshot.SnapshotId).Property("project", snapshot.Project)
                .Property("engine", snapshot.Engine).Property("engineVersion", snapshot.EngineVersion)
                .Property("sessionId", snapshot.SessionId).Property("generation", snapshot.Generation).Property("target", snapshot.Target)
                .Property("complete", snapshot.Complete).Name("entries").StartArray();
            foreach (var entry in snapshot.Entries)
            {
                json.StartObject().Property("id", entry.Id).Property("className", entry.ClassName)
                    .Property("sourceHash", entry.SourceHash).Property("error", entry.Error);
                if (entry.State != null)
                {
                    json.Name("object");
                    WriteIdentity(json, entry.State.Target);
                    json.Property("fields", entry.State.Fields).Name("references").StartArray();
                    foreach (var reference in entry.State.References)
                    {
                        json.StartObject().Property("field", reference.Field).Name("object");
                        WriteIdentity(json, reference.Identity);
                        json.EndObject();
                    }
                    json.EndArray();
                }
                json.EndObject();
            }
            return json.EndArray().EndObject().ToString();
        }

        /// <summary>按键、全局标识、场景、类型和名称写入对象身份。</summary>
        /// <param name="json">当前 JSON 构造器。</param>
        /// <param name="identity">对象身份。</param>
        private static void WriteIdentity(RoslynJsonBuilder json, LiveObjectIdentity identity)
        {
            json.StartObject().Property("key", identity.Key).Property("globalId", identity.GlobalId)
                .Property("scenePath", identity.ScenePath).Property("typeName", identity.TypeName)
                .Property("name", identity.Name).EndObject();
        }

        /// <summary>校验并还原快照。文档在头部、条目和完整标志读取完成前保持打开。</summary>
        /// <param name="text">快照 JSON。</param>
        /// <returns>条目已追加且完整标志一致的快照。</returns>
        private static LiveSnapshot Parse(string text)
        {
            ValidateSize(text, MaxBytes);
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            var snapshot = ReadSnapshotHeader(root);
            AppendSnapshotEntries(root, snapshot);
            if (root.GetProperty("complete").GetBoolean() != snapshot.Complete)
                throw new InvalidDataException("Snapshot completeness does not match its records.");
            return snapshot;
        }

        /// <summary>读取架构、身份和代际。不读取条目，校验顺序与整体解析一致。</summary>
        /// <param name="root">快照根对象。</param>
        /// <returns>尚未加入条目的快照。</returns>
        private static LiveSnapshot ReadSnapshotHeader(JsonElement root)
        {
            if (!root.GetProperty("schema").TryGetInt32(out int schema) || schema != 1)
                throw new InvalidDataException("Unsupported snapshot schema.");
            var snapshot = new LiveSnapshot
            {
                SnapshotId = Text(root, "snapshotId"), Project = Text(root, "project"),
                Engine = Text(root, "engine"), EngineVersion = Text(root, "engineVersion"),
                SessionId = Text(root, "sessionId"), Target = Text(root, "target")
            };
            if (!root.GetProperty("generation").TryGetInt64(out long generation))
                throw new InvalidDataException("Invalid generation.");
            snapshot.Generation = generation;
            if (!Guid.TryParseExact(snapshot.SnapshotId, "N", out _))
                throw new InvalidDataException("Invalid snapshot ID.");
            return snapshot;
        }

        /// <summary>按原顺序校验并追加条目。重复标识或非法状态在该条加入列表前抛出。</summary>
        /// <param name="root">快照根对象。</param>
        /// <param name="snapshot">已读取头部的快照，条目写入其内部列表。</param>
        private static void AppendSnapshotEntries(JsonElement root, LiveSnapshot snapshot)
        {
            var entries = root.GetProperty("entries");
            if (entries.GetArrayLength() > 64) throw new InvalidDataException("Snapshot handle limit is 64.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entries.EnumerateArray())
            {
                var entry = new LiveSnapshotEntry
                {
                    Id = Text(item, "id"), ClassName = Text(item, "className"),
                    SourceHash = Text(item, "sourceHash"), Error = Text(item, "error")
                };
                LiveCodeManager.ValidateName(entry.Id);
                if (!ids.Add(entry.Id)) throw new InvalidDataException("Duplicate snapshot ID: " + entry.Id);
                if (entry.Error.Length == 0)
                {
                    LiveCodeManager.ValidateName(entry.ClassName);
                    if (entry.SourceHash.Length != 64) throw new InvalidDataException("Invalid source hash.");
                    string fields = Text(item, "fields", MaxFieldBytes);
                    var references = new List<LiveSnapshotReference>();
                    var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                    var array = item.GetProperty("references");
                    if (array.GetArrayLength() > 256) throw new InvalidDataException("Too many object references.");
                    foreach (var reference in array.EnumerateArray())
                    {
                        string field = Text(reference, "field");
                        if (!fieldNames.Add(field)) throw new InvalidDataException("Duplicate field reference.");
                        references.Add(new LiveSnapshotReference(field, ReadIdentity(reference.GetProperty("object"))));
                    }
                    entry.State = new LiveSnapshotState(ReadIdentity(item.GetProperty("object")), fields, references);
                }
                snapshot.mEntries.Add(entry);
            }
        }

        /// <summary>读取对象身份。键和类型为空时拒绝。</summary>
        /// <param name="json">身份对象。</param>
        /// <returns>快照内的对象身份。</returns>
        private static LiveObjectIdentity ReadIdentity(JsonElement json)
        {
            string key = Text(json, "key");
            string type = Text(json, "typeName");
            if (key.Length == 0 || type.Length == 0) throw new InvalidDataException("Object key/type is required.");
            return new LiveObjectIdentity(key, Text(json, "globalId"), Text(json, "scenePath"),
                type, Text(json, "name"));
        }

        /// <summary>读取字符串并按字节上限校验。默认上限 4096。</summary>
        /// <param name="json">所在对象。</param>
        /// <param name="name">属性名。</param>
        /// <param name="limit">最大 UTF-8 字节数。</param>
        /// <returns>属性文本。</returns>
        private static string Text(JsonElement json, string name, int limit = 4096)
        {
            string text = json.GetProperty(name).GetString();
            ValidateSize(text, limit);
            return text;
        }

        /// <summary>拒绝 null 或超过字节上限的快照文本。</summary>
        /// <param name="text">待测文本。</param>
        /// <param name="limit">最大 UTF-8 字节数。</param>
        internal static void ValidateSize(string text, int limit)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > limit)
                throw new InvalidDataException("Snapshot value exceeds its size limit.");
        }

        /// <summary>确认字段 JSON 不超过字段上限，且根节点是对象。深度不超过 8。</summary>
        /// <param name="text">字段 JSON。</param>
        public static void ValidateSnapshotFieldText(string text)
        {
            ValidateSize(text, MaxFieldBytes);
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Snapshot fields must be an object.");
        }
    }
}
#endif
