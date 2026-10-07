#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Durable staging and compare-before-write. Never imports assets or executes user code.</summary>
    public sealed class LiveExportStore
    {
        public const int MaxVersions = 16;
        private const int MaxRecordBytes = 4 * 1024 * 1024;
        private readonly string mRoot;
        private readonly string mProject;
        /// <summary>规范化项目根，并用同一路径规则计算项目身份。不创建目录。</summary>
        /// <param name="root">项目根目录。</param>
        public LiveExportStore(string root)
        {
            mRoot = Path.GetFullPath(root);
            mProject = new LiveSnapshotStore(root).ProjectIdentity;
        }

        /// <summary>返回导出记录的项目内路径。标识必须是 N 格式 GUID。</summary>
        /// <param name="id">导出标识。</param>
        /// <returns>记录 JSON 的绝对路径。</returns>
        public string RecordPath(string id) => LiveCodePaths.Record(mRoot, id);
        /// <summary>返回批次记录路径，即导出记录路径加 .batch。</summary>
        /// <param name="id">批次标识。</param>
        /// <returns>批次文件绝对路径。</returns>
        public string BatchPath(string id) => RecordPath(id) + ".batch";
        /// <summary>校验类名与文件名一致后，解析已有脚本的项目内路径。不检查文件是否存在。</summary>
        /// <param name="version">含类名和相对源码路径的版本。</param>
        /// <returns>脚本绝对路径。</returns>
        public string ScriptPath(LiveExportVersion version)
        {
            LiveCodeManager.ValidateName(version.ClassName);
            if (Path.GetFileName(version.SourcePath) != version.ClassName + ".cs")
                throw new ArgumentException("Export filename must match className.");
            return LiveCodePaths.ExistingScript(mRoot, version.SourcePath, "Assets");
        }

        /// <summary>把 1 到 16 个版本暂存为 Prepared 批次。先校验再归档旧版、写版本记录，最后写批次文件。</summary>
        /// <param name="batchId">新批次标识，不能与已有批次重复。</param>
        /// <param name="versions">同一批次内路径、类名和导出标识都不得重复。</param>
        /// <returns>只含导出标识的 Prepared 批次。</returns>
        public LiveExportBatch Stage(string batchId, IReadOnlyList<LiveExportVersion> versions)
        {
            if (versions == null || versions.Count == 0 || versions.Count > MaxVersions)
                throw new ArgumentException("Export requires 1..16 distinct scripts.");
            string batchPath = BatchPath(batchId);
            if (File.Exists(batchPath)) throw new InvalidOperationException("Batch ID exists; query it instead of replaying.");
            var batch = new LiveExportBatch { BatchId = batchId };
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

        /// <summary>读取导出版本。架构 1 走旧记录，架构 2 读取目标列表并再校验。</summary>
        /// <param name="id">导出标识，必须与记录内身份一致。</param>
        /// <returns>校验后的版本。</returns>
        public LiveExportVersion ReadVersion(string id)
        {
            using var doc = JsonDocument.Parse(ReadText(RecordPath(id)));
            var root = doc.RootElement;
            if (root.GetProperty("schema").TryGetInt32(out int legacySchema) && legacySchema == 1)
                return ReadLegacy(id, root);
            CheckHeader(root, 2);
            var version = new LiveExportVersion
            {
                ExportId = Text(root, "exportId"), BatchId = Text(root, "batchId"),
                PreviousExportId = Text(root, "previousExportId"), ClassName = Text(root, "className"),
                SourcePath = Text(root, "sourcePath"), Source = Text(root, "source"),
                SourceHash = Text(root, "sourceHash"), PreviousMetaHash = Text(root, "previousMetaHash")
            };
            if (version.ExportId != id) throw new InvalidDataException("Export identity is invalid.");
            foreach (var item in root.GetProperty("targets").EnumerateArray())
                version.Targets.Add(new LiveExportTarget
                { TargetId = Text(item, "targetId"), ScenePath = Text(item, "scenePath"), Fields = Text(item, "fields") });
            ValidateVersion(version);
            return version;
        }

        /// <summary>把架构 1 记录补成版本对象。源码优先读旁路归档，否则读脚本，哈希不符则拒绝推断。</summary>
        /// <param name="id">记录中的导出标识。</param>
        /// <param name="root">已解析的旧记录根对象。</param>
        /// <returns>无批次号的版本，含单个目标。</returns>
        private LiveExportVersion ReadLegacy(string id, JsonElement root)
        {
            if (Text(root, "exportId") != id) throw new InvalidDataException("Legacy export identity mismatch.");
            var version = new LiveExportVersion
            {
                ExportId = id, BatchId = "", ClassName = Text(root, "className"),
                SourcePath = Text(root, "sourcePath"), SourceHash = Text(root, "sourceHash")
            };
            ScriptPath(version);
            string archive = RecordPath(id) + ".source";
            version.Source = ReadText(File.Exists(archive) ? archive : ScriptPath(version));
            if (LiveCodeManager.Hash(version.Source) != version.SourceHash)
                throw new InvalidDataException("Legacy source changed; cannot infer its original version.");
            version.Targets.Add(new LiveExportTarget
            {
                TargetId = Text(root, "targetId"), ScenePath = Text(root, "scenePath"),
                Fields = root.GetProperty("fields").ToString()
            });
            return version;
        }

        /// <summary>旧版尚无旁路源码时，把当前读到的源码写入 .source。已有批次或归档则不覆盖。</summary>
        /// <param name="id">上一版导出标识。</param>
        private void ArchiveLegacy(string id)
        {
            var version = ReadVersion(id);
            if (version.BatchId.Length == 0 && !File.Exists(RecordPath(id) + ".source"))
                WriteNew(RecordPath(id) + ".source", version.Source);
        }

        /// <summary>读取并校验批次身份、数量和状态。不修改文件。</summary>
        /// <param name="id">批次标识。</param>
        /// <returns>批次及其导出标识。</returns>
        public LiveExportBatch ReadBatch(string id)
        {
            using var doc = JsonDocument.Parse(ReadText(BatchPath(id)));
            var root = doc.RootElement;
            CheckHeader(root, 1);
            var batch = new LiveExportBatch
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

        /// <summary>仅把 Prepared 批次标为 Queued。已提交返回 false，其他状态拒绝重放。</summary>
        /// <param name="id">批次标识。</param>
        /// <returns>是否新写入 Queued。</returns>
        public bool MarkQueued(string id)
        {
            var batch = ReadBatch(id);
            if (batch.State == "Committed") return false;
            if (batch.State != "Prepared") throw new InvalidOperationException("Batch is " + batch.State + "; inspect, do not replay.");
            batch.State = "Queued";
            SaveBatch(batch);
            return true;
        }

        /// <summary>Queued 批次记为 Failed 并保存错误。其他状态不改文件。</summary>
        /// <param name="id">批次标识。</param>
        /// <param name="error">失败说明。</param>
        public void FailQueued(string id, string error)
        {
            var batch = ReadBatch(id);
            if (batch.State != "Queued") return;
            batch.State = "Failed"; batch.Error = error;
            SaveBatch(batch);
        }

        /// <summary>
        /// 提交一批导出。已提交的批次原样返回；失败也返回批次，不把部分写入伪装成未发生。
        /// </summary>
        /// <param name="id">批次标识。</param>
        /// <returns>提交后的批次。</returns>
        public LiveExportBatch Commit(string id)
        {
            var batch = ReadBatch(id);
            if (batch.State == "Committed") return batch;
            if (batch.State != "Queued") throw new InvalidOperationException("Only an explicitly queued batch can commit.");
            var applied = new List<LiveExportVersion>();
            try
            {
                var versions = PrepareCommitVersions(batch);
                batch.State = "Committing";
                SaveBatch(batch);
                WriteCommitVersions(versions, applied);
                batch.State = "Committed";
                SaveBatch(batch);
            }
            catch (Exception error)
            {
                batch.State = "Failed";
                batch.Error = error.Message;
                RollbackCommittedFiles(batch, applied);
                SaveBatch(batch);
            }

            return batch;
        }

        /// <summary>写文件前校验整批路径、类名和当前源码。</summary>
        /// <param name="batch">待提交批次。</param>
        /// <returns>通过校验的版本。</returns>
        private List<LiveExportVersion> PrepareCommitVersions(LiveExportBatch batch)
        {
            var versions = new List<LiveExportVersion>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string exportId in batch.ExportIds)
            {
                var version = ReadVersion(exportId);
                if (version.BatchId != batch.BatchId || !paths.Add(ScriptPath(version)) || !classes.Add(version.ClassName))
                    throw new InvalidDataException("Revision batch/path/class conflict.");
                ValidateCurrent(version);
                versions.Add(version);
            }

            return versions;
        }

        /// <summary>按校验后的版本写入脚本。每个文件写入前再校验一次。</summary>
        /// <param name="versions">待写入版本。</param>
        /// <param name="applied">已写入版本，供失败回滚。</param>
        private void WriteCommitVersions(
            List<LiveExportVersion> versions, List<LiveExportVersion> applied)
        {
            foreach (var version in versions)
            {
                ValidateCurrent(version);
                string path = ScriptPath(version);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (version.PreviousExportId.Length == 0) WriteNew(path, version.Source);
                else Replace(path, version.Source);
                applied.Add(version);
            }
        }

        /// <summary>
        /// 逆序回滚已写入文件。源码或 meta 已变化时停止该文件并标记 Partial，其余文件继续处理。
        /// </summary>
        /// <param name="batch">失败批次。</param>
        /// <param name="applied">已写入版本。</param>
        private void RollbackCommittedFiles(LiveExportBatch batch, List<LiveExportVersion> applied)
        {
            for (int index = applied.Count - 1; index >= 0; index--)
            {
                var version = applied[index];
                try
                {
                    string path = ScriptPath(version);
                    if (!MatchesSource(version)) throw new IOException("File changed after write: " + version.SourcePath);
                    if (version.PreviousExportId.Length == 0) File.Delete(path);
                    else RestorePrevious(version, path);
                }
                catch (Exception rollback)
                {
                    batch.State = "Partial";
                    batch.Error += " Rollback: " + rollback.Message;
                }
            }
        }

        /// <summary>恢复上一版源码；meta 已变化时拒绝覆盖。</summary>
        /// <param name="version">当前版本。</param>
        /// <param name="path">脚本路径。</param>
        private void RestorePrevious(LiveExportVersion version, string path)
        {
            if (MetaHash(version) != version.PreviousMetaHash) throw new IOException("Script metadata changed.");
            Replace(path, ReadVersion(version.PreviousExportId).Source);
        }

        /// <summary>判断脚本是否存在且内容哈希等于该版本。</summary>
        /// <param name="version">待比较版本。</param>
        /// <returns>路径和哈希都匹配时为 true。</returns>
        public bool MatchesSource(LiveExportVersion version) =>
            File.Exists(ScriptPath(version)) && LiveCodeManager.Hash(ReadText(ScriptPath(version))) == version.SourceHash;

        /// <summary>计算脚本 .meta 的哈希。文件不存在时返回空字符串，不创建文件。</summary>
        /// <param name="version">提供脚本路径的版本。</param>
        /// <returns>meta 哈希或空字符串。</returns>
        public string MetaHash(LiveExportVersion version)
        {
            string path = YokiFrameFilePathPolicy.EnsureInside(mRoot, ScriptPath(version) + ".meta");
            return File.Exists(path) ? LiveCodeManager.Hash(ReadText(path)) : "";
        }

        /// <summary>新导出不能覆盖已有源码或 meta；修订必须与上一版的类、路径、源码和 meta 一致。</summary>
        /// <param name="version">即将写入或暂存的版本。</param>
        public void ValidateCurrent(LiveExportVersion version)
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

        /// <summary>序列化批次。不写盘，导出标识顺序与列表一致。</summary>
        /// <param name="batch">批次。</param>
        /// <returns>架构 1 的 JSON。</returns>
        public string SerializeBatch(LiveExportBatch batch)
        {
            var json = new RoslynJsonBuilder().StartObject().Property("schema", 1).Property("project", mProject)
                .Property("batchId", batch.BatchId).Property("state", batch.State).Property("error", batch.Error)
                .Name("exportIds").StartArray();
            foreach (string id in batch.ExportIds) json.String(id);
            return json.EndArray().EndObject().ToString();
        }

        /// <summary>序列化架构 2 版本并检查记录大小。不写盘。</summary>
        /// <param name="version">含目标和源码的版本。</param>
        /// <returns>未超过记录上限的 JSON。</returns>
        private string SerializeVersion(LiveExportVersion version)
        {
            var json = new RoslynJsonBuilder().StartObject().Property("schema", 2).Property("project", mProject)
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

        /// <summary>核对架构号和项目身份，不匹配则拒绝记录。</summary>
        /// <param name="root">JSON 根对象。</param>
        /// <param name="expected">期望架构号。</param>
        private void CheckHeader(JsonElement root, int expected)
        {
            if (!root.GetProperty("schema").TryGetInt32(out int schema) || schema != expected || Text(root, "project") != mProject)
                throw new InvalidDataException("Export schema/project mismatch.");
        }

        /// <summary>校验版本身份、路径、源码哈希和最多 64 个不重复目标。不写盘。</summary>
        /// <param name="version">待校验版本。</param>
        private void ValidateVersion(LiveExportVersion version)
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
            if (version.SourceHash != LiveCodeManager.Hash(version.Source))
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

        /// <summary>用替换写入批次记录。</summary>
        /// <param name="batch">已更新状态的批次。</param>
        private void SaveBatch(LiveExportBatch batch) => Replace(BatchPath(batch.BatchId), SerializeBatch(batch));
        /// <summary>读取必填字符串。缺失或 JSON null 都视为记录损坏。</summary>
        /// <param name="root">对象。</param>
        /// <param name="name">属性名，同时用作异常文案。</param>
        /// <returns>属性字符串。</returns>
        private static string Text(JsonElement root, string name) => root.GetProperty(name).GetString() ?? throw new InvalidDataException(name);
        /// <summary>拒绝 null 或 UTF-8 字节数超限的文本。</summary>
        /// <param name="text">待测文本。</param>
        /// <param name="bytes">允许的最大字节数。</param>
        private static void ValidateSize(string text, int bytes)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > bytes) throw new InvalidDataException("Export record size limit exceeded.");
        }
        /// <summary>按 UTF-8 读取已有文件。超过 4 MiB 的记录直接拒绝。</summary>
        /// <param name="path">绝对路径。</param>
        /// <returns>文件全文。</returns>
        public static string ReadText(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxRecordBytes) throw new InvalidDataException("Export record exceeds 4 MiB.");
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
            return reader.ReadToEnd();
        }
        /// <summary>仅当路径不存在时创建并写入。不覆盖已有文件。</summary>
        /// <param name="path">新文件路径。</param>
        /// <param name="text">全文。</param>
        public static void WriteNew(string path, string text)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(text);
        }
        /// <summary>先写临时文件再替换目标。失败时删除仍存在的临时文件。</summary>
        /// <param name="path">已有目标路径。</param>
        /// <param name="text">替换后的全文。</param>
        public static void Replace(string path, string text)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { WriteNew(temporary, text); File.Replace(temporary, path, null); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
#endif
