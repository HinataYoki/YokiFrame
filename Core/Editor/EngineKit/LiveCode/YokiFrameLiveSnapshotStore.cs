#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class YokiFrameLiveSnapshotStore
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public const int MaxFieldBytes = 64 * 1024;
        private readonly string mRoot;
        public string ProjectIdentity { get; }

        public YokiFrameLiveSnapshotStore(string projectRoot)
        {
            mRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string identity = Path.DirectorySeparatorChar == '\\' ? mRoot.ToUpperInvariant() : mRoot;
            ProjectIdentity = YokiFrameLiveCodeManager.Hash(identity);
        }

        public string GetPath(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid snapshot ID.");
            return YokiFrameFilePathPolicy.CombineInside(mRoot, ".yokiframe", "engine", "live-snapshots", id + ".json");
        }

        internal void Save(YokiFrameLiveSnapshot snapshot)
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

        public YokiFrameLiveSnapshot Read(string id)
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

        public static string Serialize(YokiFrameLiveSnapshot snapshot)
        {
            var json = new YokiFrameEngineJsonBuilder().StartObject().Property("schema", 1)
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

        private static void WriteIdentity(YokiFrameEngineJsonBuilder json, YokiFrameLiveObjectIdentity identity)
        {
            json.StartObject().Property("key", identity.Key).Property("globalId", identity.GlobalId)
                .Property("scenePath", identity.ScenePath).Property("typeName", identity.TypeName)
                .Property("name", identity.Name).EndObject();
        }

        private static YokiFrameLiveSnapshot Parse(string text)
        {
            ValidateSize(text, MaxBytes);
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            if (!root.GetProperty("schema").TryGetInt32(out int schema) || schema != 1)
                throw new InvalidDataException("Unsupported snapshot schema.");
            var snapshot = new YokiFrameLiveSnapshot
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
            var entries = root.GetProperty("entries");
            if (entries.GetArrayLength() > 64) throw new InvalidDataException("Snapshot handle limit is 64.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entries.EnumerateArray())
            {
                var entry = new YokiFrameLiveSnapshotEntry
                {
                    Id = Text(item, "id"), ClassName = Text(item, "className"),
                    SourceHash = Text(item, "sourceHash"), Error = Text(item, "error")
                };
                YokiFrameLiveCodeManager.ValidateName(entry.Id);
                if (!ids.Add(entry.Id)) throw new InvalidDataException("Duplicate snapshot ID: " + entry.Id);
                if (entry.Error.Length == 0)
                {
                    YokiFrameLiveCodeManager.ValidateName(entry.ClassName);
                    if (entry.SourceHash.Length != 64) throw new InvalidDataException("Invalid source hash.");
                    string fields = Text(item, "fields", MaxFieldBytes);
                    var references = new List<YokiFrameLiveSnapshotReference>();
                    var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                    var array = item.GetProperty("references");
                    if (array.GetArrayLength() > 256) throw new InvalidDataException("Too many object references.");
                    foreach (var reference in array.EnumerateArray())
                    {
                        string field = Text(reference, "field");
                        if (!fieldNames.Add(field)) throw new InvalidDataException("Duplicate field reference.");
                        references.Add(new YokiFrameLiveSnapshotReference(field, ReadIdentity(reference.GetProperty("object"))));
                    }
                    entry.State = new YokiFrameLiveSnapshotState(ReadIdentity(item.GetProperty("object")), fields, references);
                }
                snapshot.mEntries.Add(entry);
            }
            if (root.GetProperty("complete").GetBoolean() != snapshot.Complete)
                throw new InvalidDataException("Snapshot completeness does not match its records.");
            return snapshot;
        }

        private static YokiFrameLiveObjectIdentity ReadIdentity(JsonElement json)
        {
            string key = Text(json, "key");
            string type = Text(json, "typeName");
            if (key.Length == 0 || type.Length == 0) throw new InvalidDataException("Object key/type is required.");
            return new YokiFrameLiveObjectIdentity(key, Text(json, "globalId"), Text(json, "scenePath"),
                type, Text(json, "name"));
        }

        private static string Text(JsonElement json, string name, int limit = 4096)
        {
            string text = json.GetProperty(name).GetString();
            ValidateSize(text, limit);
            return text;
        }

        internal static void ValidateSize(string text, int limit)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > limit)
                throw new InvalidDataException("Snapshot value exceeds its size limit.");
        }

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
