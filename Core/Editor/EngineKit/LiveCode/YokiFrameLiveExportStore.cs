#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Durable staging and compare-before-write. Never imports assets or executes user code.</summary>
    public sealed class YokiFrameLiveExportStore
    {
        public const int MaxVersions = 16;
        private const int MaxRecordBytes = 4 * 1024 * 1024;
        private readonly string mRoot;
        private readonly string mProject;
        public YokiFrameLiveExportStore(string root)
        {
            mRoot = Path.GetFullPath(root);
            mProject = new YokiFrameLiveSnapshotStore(root).ProjectIdentity;
        }

        public string RecordPath(string id) => YokiFrameLiveCodePaths.Record(mRoot, id);
        public string BatchPath(string id) => RecordPath(id) + ".batch";
        public string ScriptPath(YokiFrameLiveExportVersion version)
        {
            YokiFrameLiveCodeManager.ValidateName(version.ClassName);
            if (Path.GetFileName(version.SourcePath) != version.ClassName + ".cs")
                throw new ArgumentException("Export filename must match className.");
            return YokiFrameLiveCodePaths.ExistingScript(mRoot, version.SourcePath, "Assets");
        }

        public YokiFrameLiveExportBatch Stage(string batchId, IReadOnlyList<YokiFrameLiveExportVersion> versions)
        {
            if (versions == null || versions.Count == 0 || versions.Count > MaxVersions)
                throw new ArgumentException("Export requires 1..16 distinct scripts.");
            string batchPath = BatchPath(batchId);
            if (File.Exists(batchPath)) throw new InvalidOperationException("Batch ID exists; query it instead of replaying.");
            var batch = new YokiFrameLiveExportBatch { BatchId = batchId };
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var texts = new List<string>();
            foreach (var version in versions)
            {
                if (version == null || version.BatchId != batchId || !ids.Add(version.ExportId)
                    || !paths.Add(ScriptPath(version)) || !classes.Add(version.ClassName))
                    throw new ArgumentException("Duplicate ID/path/class or mismatched batch.");
                ValidateVersion(version);
                if (File.Exists(RecordPath(version.ExportId))) throw new InvalidOperationException("Export ID exists.");
                ValidateCurrent(version);
                texts.Add(SerializeVersion(version));
                batch.ExportIds.Add(version.ExportId);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(batchPath));
            foreach (var version in versions)
                if (version.PreviousExportId.Length > 0) ArchiveLegacy(version.PreviousExportId);
            for (int index = 0; index < versions.Count; index++)
                WriteNew(RecordPath(versions[index].ExportId), texts[index]);
            WriteNew(batchPath, SerializeBatch(batch));
            return batch;
        }

        public YokiFrameLiveExportVersion ReadVersion(string id)
        {
            using var doc = JsonDocument.Parse(ReadText(RecordPath(id)));
            var root = doc.RootElement;
            if (root.GetProperty("schema").TryGetInt32(out int legacySchema) && legacySchema == 1)
                return ReadLegacy(id, root);
            CheckHeader(root, 2);
            var version = new YokiFrameLiveExportVersion
            {
                ExportId = Text(root, "exportId"), BatchId = Text(root, "batchId"),
                PreviousExportId = Text(root, "previousExportId"), ClassName = Text(root, "className"),
                SourcePath = Text(root, "sourcePath"), Source = Text(root, "source"),
                SourceHash = Text(root, "sourceHash"), PreviousMetaHash = Text(root, "previousMetaHash")
            };
            if (version.ExportId != id) throw new InvalidDataException("Export identity is invalid.");
            foreach (var item in root.GetProperty("targets").EnumerateArray())
                version.Targets.Add(new YokiFrameLiveExportTarget
                { TargetId = Text(item, "targetId"), ScenePath = Text(item, "scenePath"), Fields = Text(item, "fields") });
            ValidateVersion(version);
            return version;
        }

        private YokiFrameLiveExportVersion ReadLegacy(string id, JsonElement root)
        {
            if (Text(root, "exportId") != id) throw new InvalidDataException("Legacy export identity mismatch.");
            var version = new YokiFrameLiveExportVersion
            {
                ExportId = id, BatchId = "", ClassName = Text(root, "className"),
                SourcePath = Text(root, "sourcePath"), SourceHash = Text(root, "sourceHash")
            };
            ScriptPath(version);
            string archive = RecordPath(id) + ".source";
            version.Source = ReadText(File.Exists(archive) ? archive : ScriptPath(version));
            if (YokiFrameLiveCodeManager.Hash(version.Source) != version.SourceHash)
                throw new InvalidDataException("Legacy source changed; cannot infer its original version.");
            version.Targets.Add(new YokiFrameLiveExportTarget
            {
                TargetId = Text(root, "targetId"), ScenePath = Text(root, "scenePath"),
                Fields = root.GetProperty("fields").ToString()
            });
            return version;
        }

        private void ArchiveLegacy(string id)
        {
            var version = ReadVersion(id);
            if (version.BatchId.Length == 0 && !File.Exists(RecordPath(id) + ".source"))
                WriteNew(RecordPath(id) + ".source", version.Source);
        }

        public YokiFrameLiveExportBatch ReadBatch(string id)
        {
            using var doc = JsonDocument.Parse(ReadText(BatchPath(id)));
            var root = doc.RootElement;
            CheckHeader(root, 1);
            var batch = new YokiFrameLiveExportBatch
            { BatchId = Text(root, "batchId"), State = Text(root, "state"), Error = Text(root, "error") };
            if (batch.BatchId != id) throw new InvalidDataException("Batch identity mismatch.");
            var ids = new HashSet<string>();
            foreach (var item in root.GetProperty("exportIds").EnumerateArray())
            {
                string exportId = item.GetString();
                RecordPath(exportId);
                if (!ids.Add(exportId)) throw new InvalidDataException("Duplicate export ID.");
                batch.ExportIds.Add(exportId);
            }
            if (batch.ExportIds.Count == 0 || batch.ExportIds.Count > MaxVersions)
                throw new InvalidDataException("Invalid batch size.");
            if (batch.State != "Prepared" && batch.State != "Queued" && batch.State != "Committing"
                && batch.State != "Committed" && batch.State != "Failed" && batch.State != "Partial")
                throw new InvalidDataException("Invalid batch state.");
            return batch;
        }

        public bool MarkQueued(string id)
        {
            var batch = ReadBatch(id);
            if (batch.State == "Committed") return false;
            if (batch.State != "Prepared") throw new InvalidOperationException("Batch is " + batch.State + "; inspect, do not replay.");
            batch.State = "Queued";
            SaveBatch(batch);
            return true;
        }

        public void FailQueued(string id, string error)
        {
            var batch = ReadBatch(id);
            if (batch.State != "Queued") return;
            batch.State = "Failed"; batch.Error = error;
            SaveBatch(batch);
        }

        public YokiFrameLiveExportBatch Commit(string id)
        {
            var batch = ReadBatch(id);
            if (batch.State == "Committed") return batch;
            if (batch.State != "Queued") throw new InvalidOperationException("Only an explicitly queued batch can commit.");
            var versions = new List<YokiFrameLiveExportVersion>();
            var applied = new List<YokiFrameLiveExportVersion>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (string exportId in batch.ExportIds)
                {
                    var version = ReadVersion(exportId);
                    if (version.BatchId != id || !paths.Add(ScriptPath(version)) || !classes.Add(version.ClassName))
                        throw new InvalidDataException("Revision batch/path/class conflict.");
                    ValidateCurrent(version);
                    versions.Add(version);
                }
                batch.State = "Committing";
                SaveBatch(batch);
                foreach (var version in versions)
                {
                    ValidateCurrent(version);
                    string path = ScriptPath(version);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (version.PreviousExportId.Length == 0) WriteNew(path, version.Source);
                    else Replace(path, version.Source);
                    applied.Add(version);
                }
                batch.State = "Committed";
                SaveBatch(batch);
            }
            catch (Exception error)
            {
                batch.State = "Failed"; batch.Error = error.Message;
                for (int index = applied.Count - 1; index >= 0; index--)
                {
                    var version = applied[index];
                    try
                    {
                        string path = ScriptPath(version);
                        if (!MatchesSource(version)) throw new IOException("File changed after write: " + version.SourcePath);
                        if (version.PreviousExportId.Length == 0) File.Delete(path);
                        else
                        {
                            if (MetaHash(version) != version.PreviousMetaHash) throw new IOException("Script metadata changed.");
                            Replace(path, ReadVersion(version.PreviousExportId).Source);
                        }
                    }
                    catch (Exception rollback) { batch.State = "Partial"; batch.Error += " Rollback: " + rollback.Message; }
                }
                SaveBatch(batch);
            }
            return batch;
        }

        public bool MatchesSource(YokiFrameLiveExportVersion version) =>
            File.Exists(ScriptPath(version)) && YokiFrameLiveCodeManager.Hash(ReadText(ScriptPath(version))) == version.SourceHash;

        public string MetaHash(YokiFrameLiveExportVersion version)
        {
            string path = YokiFrameFilePathPolicy.EnsureInside(mRoot, ScriptPath(version) + ".meta");
            return File.Exists(path) ? YokiFrameLiveCodeManager.Hash(ReadText(path)) : "";
        }

        public void ValidateCurrent(YokiFrameLiveExportVersion version)
        {
            string path = ScriptPath(version);
            if (version.PreviousExportId.Length == 0)
            {
                if (File.Exists(path) || File.Exists(path + ".meta"))
                    throw new IOException("A new export never replaces existing source/metadata: " + version.SourcePath);
                return;
            }
            var previous = ReadVersion(version.PreviousExportId);
            if (previous.ClassName != version.ClassName || previous.SourcePath != version.SourcePath
                || !MatchesSource(previous) || version.PreviousMetaHash.Length == 0
                || MetaHash(version) != version.PreviousMetaHash)
                throw new IOException("ExportConflict: source or metadata no longer matches the previous version.");
        }

        public string SerializeBatch(YokiFrameLiveExportBatch batch)
        {
            var json = new YokiFrameEngineJsonBuilder().StartObject().Property("schema", 1).Property("project", mProject)
                .Property("batchId", batch.BatchId).Property("state", batch.State).Property("error", batch.Error)
                .Name("exportIds").StartArray();
            foreach (string id in batch.ExportIds) json.String(id);
            return json.EndArray().EndObject().ToString();
        }

        private string SerializeVersion(YokiFrameLiveExportVersion version)
        {
            var json = new YokiFrameEngineJsonBuilder().StartObject().Property("schema", 2).Property("project", mProject)
                .Property("exportId", version.ExportId).Property("batchId", version.BatchId)
                .Property("previousExportId", version.PreviousExportId).Property("className", version.ClassName)
                .Property("sourcePath", version.SourcePath).Property("source", version.Source)
                .Property("sourceHash", version.SourceHash).Property("previousMetaHash", version.PreviousMetaHash)
                .Name("targets").StartArray();
            foreach (var target in version.Targets)
                json.StartObject().Property("targetId", target.TargetId).Property("scenePath", target.ScenePath)
                    .Property("fields", target.Fields).EndObject();
            string text = json.EndArray().EndObject().ToString();
            ValidateSize(text, MaxRecordBytes);
            return text;
        }

        private void CheckHeader(JsonElement root, int expected)
        {
            if (!root.GetProperty("schema").TryGetInt32(out int schema) || schema != expected || Text(root, "project") != mProject)
                throw new InvalidDataException("Export schema/project mismatch.");
        }

        private void ValidateVersion(YokiFrameLiveExportVersion version)
        {
            RecordPath(version.ExportId);
            BatchPath(version.BatchId);
            ScriptPath(version);
            if (version.PreviousExportId.Length > 0)
            {
                RecordPath(version.PreviousExportId);
                if (version.PreviousExportId == version.ExportId) throw new InvalidDataException("A version cannot replace itself.");
            }
            ValidateSize(version.SourcePath, 1024);
            ValidateSize(version.Source, 128 * 1024);
            if (version.SourceHash != YokiFrameLiveCodeManager.Hash(version.Source))
                throw new InvalidDataException("Invalid archived source hash.");
            if (version.Targets.Count > 64) throw new InvalidDataException("At most 64 targets per script.");
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in version.Targets)
            {
                if (target == null || string.IsNullOrEmpty(target.TargetId) || !targets.Add(target.TargetId))
                    throw new ArgumentException("Duplicate or missing export target.");
                ValidateSize(target.TargetId, 256);
                ValidateSize(target.ScenePath, 1024);
                ValidateSize(target.Fields, 64 * 1024);
            }
        }

        private void SaveBatch(YokiFrameLiveExportBatch batch) => Replace(BatchPath(batch.BatchId), SerializeBatch(batch));
        private static string Text(JsonElement root, string name) => root.GetProperty(name).GetString() ?? throw new InvalidDataException(name);
        private static void ValidateSize(string text, int bytes)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > bytes) throw new InvalidDataException("Export record size limit exceeded.");
        }
        public static string ReadText(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxRecordBytes) throw new InvalidDataException("Export record exceeds 4 MiB.");
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
            return reader.ReadToEnd();
        }
        public static void WriteNew(string path, string text)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(text);
        }
        public static void Replace(string path, string text)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { WriteNew(temporary, text); File.Replace(temporary, path, null); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
#endif
