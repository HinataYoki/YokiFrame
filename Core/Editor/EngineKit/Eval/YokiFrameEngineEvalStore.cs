#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// eval 记录与生成源码的持久化存储：<c>&lt;project&gt;/.yokiframe/engine/eval/</c>。
    /// </summary>
    /// <remarks>
    /// 与运行记录分开存放，便于 <c>eval_prune</c> 只清理 eval 自己的产物（§13）。
    /// </remarks>
    public sealed class YokiFrameEngineEvalStore
    {
        /// <summary>记录目录相对项目根。</summary>
        public const string EVAL_RELATIVE_PATH = ".yokiframe/engine/eval";

        private static readonly UTF8Encoding sUtf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly string mEvalRoot;

        /// <summary>创建存储。</summary>
        /// <param name="projectRoot">项目根。</param>
        public YokiFrameEngineEvalStore(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("projectRoot is required.", nameof(projectRoot));
            }

            mEvalRoot = Path.Combine(Path.GetFullPath(projectRoot), ".yokiframe", "engine", "eval");
        }

        /// <summary>获取记录根目录。</summary>
        public string EvalRoot
        {
            get { return mEvalRoot; }
        }

        /// <summary>写入记录（原子替换）。</summary>
        /// <param name="record">记录。</param>
        public void Save(YokiFrameEngineEvalRecord record)
        {
            if (record == null || !YokiFrameEngineEvalRequest.IsSafeId(record.Id))
            {
                throw new ArgumentException("eval record id is not safe.", nameof(record));
            }

            // 复用共享原子写（唯一物理事实源）；私有副本已删除。
            YokiFrameAtomicFileWriter.WriteAllText(RecordPath(record.Id), BuildJson(record));
        }

        /// <summary>读取记录。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="record">记录。</param>
        /// <returns>命中时返回 true。</returns>
        public bool TryRead(string id, out YokiFrameEngineEvalRecord record)
        {
            record = null;
            if (!YokiFrameEngineEvalRequest.IsSafeId(id))
            {
                return false;
            }

            string path = RecordPath(id);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                return TryParse(File.ReadAllText(path, sUtf8NoBom), out record);
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>读取全部记录，按提交时间升序。</summary>
        /// <returns>记录列表。</returns>
        public IReadOnlyList<YokiFrameEngineEvalRecord> ReadAll()
        {
            var records = new List<YokiFrameEngineEvalRecord>();
            if (!Directory.Exists(mEvalRoot))
            {
                return records;
            }

            string[] files = Directory.GetFiles(mEvalRoot, "*.json", SearchOption.TopDirectoryOnly);
            for (var index = 0; index < files.Length; index++)
            {
                try
                {
                    if (TryParse(File.ReadAllText(files[index], sUtf8NoBom), out YokiFrameEngineEvalRecord record))
                    {
                        records.Add(record);
                    }
                }
                catch (IOException)
                {
                    // 单条损坏不影响其它记录。
                }
            }

            records.Sort(static (left, right) => left.SubmittedAtUtc.CompareTo(right.SubmittedAtUtc));
            return records;
        }

        /// <summary>删除记录文件。</summary>
        /// <param name="id">eval 标识。</param>
        /// <returns>删除成功或本就不存在时返回 true。</returns>
        public bool TryDelete(string id)
        {
            if (!YokiFrameEngineEvalRequest.IsSafeId(id))
            {
                return false;
            }

            try
            {
                string path = RecordPath(id);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private string RecordPath(string id)
        {
            return Path.Combine(mEvalRoot, id + ".json");
        }

        private static string BuildJson(YokiFrameEngineEvalRecord record)
        {
            return new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("id", record.Id)
                .Property("token", record.Token)
                .Property("state", record.State)
                .Property("sourcePath", record.SourcePath)
                .Property("codeHash", record.CodeHash)
                .Property("codeLength", record.CodeLength)
                .Property("runId", record.RunId)
                .Property("entryName", record.EntryName)
                .Property("attempts", record.Attempts)
                .Property("submittedAtUtc", record.SubmittedAtUtc.ToString("o", CultureInfo.InvariantCulture))
                .Property("updatedAtUtc", record.UpdatedAtUtc.ToString("o", CultureInfo.InvariantCulture))
                .Property("compilerErrors", record.CompilerErrors)
                .Property("note", record.Note)
                .EndObject()
                .ToString();
        }

        private static bool TryParse(string json, out YokiFrameEngineEvalRecord record)
        {
            record = null;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    var parsed = new YokiFrameEngineEvalRecord
                    {
                        Id = ReadString(root, "id"),
                        Token = ReadString(root, "token"),
                        State = ReadString(root, "state"),
                        SourcePath = ReadString(root, "sourcePath"),
                        CodeHash = ReadString(root, "codeHash"),
                        CodeLength = (int)ReadLong(root, "codeLength"),
                        RunId = ReadString(root, "runId"),
                        EntryName = ReadString(root, "entryName"),
                        Attempts = (int)ReadLong(root, "attempts"),
                        CompilerErrors = ReadString(root, "compilerErrors"),
                        Note = ReadString(root, "note"),
                        SubmittedAtUtc = ReadDate(root, "submittedAtUtc"),
                        UpdatedAtUtc = ReadDate(root, "updatedAtUtc")
                    };
                    record = parsed.Id.Length > 0 ? parsed : null;
                    return record != null;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static long ReadLong(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : 0L;
        }

        private static DateTime ReadDate(JsonElement element, string name)
        {
            string text = ReadString(element, name);
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }
    }

    /// <summary>
    /// eval 记录：编译阶段状态与产物定位（§11）。
    /// </summary>
    public sealed class YokiFrameEngineEvalRecord
    {
        /// <summary>获取或设置 eval 标识。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>获取或设置编译批次令牌。</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>获取或设置状态文本：Compiling / Ready / CompileFailed / Unknown。</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>获取或设置生成源码的项目内相对路径。</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>获取或设置用户代码的 SHA-256。</summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>获取或设置用户代码长度。</summary>
        public int CodeLength { get; set; }

        /// <summary>获取或设置就绪后提交的运行标识。</summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>获取或设置生成入口名。</summary>
        public string EntryName { get; set; } = string.Empty;

        /// <summary>获取或设置等待编译的轮次。</summary>
        public int Attempts { get; set; }

        /// <summary>获取或设置提交时间。</summary>
        public DateTime SubmittedAtUtc { get; set; }

        /// <summary>获取或设置更新时间。</summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>获取或设置编译错误摘要。</summary>
        public string CompilerErrors { get; set; } = string.Empty;

        /// <summary>获取或设置附注。</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>获取是否为终态（就绪或编译失败）。</summary>
        public bool IsTerminal
        {
            get { return State == YokiFrameEngineEvalStates.READY || State == YokiFrameEngineEvalStates.COMPILE_FAILED; }
        }
    }

    /// <summary>eval 编译阶段状态文本。</summary>
    public static class YokiFrameEngineEvalStates
    {
        /// <summary>已写源码，等待编译完成。</summary>
        public const string COMPILING = "Compiling";

        /// <summary>类型与令牌匹配，已提交运行。</summary>
        public const string READY = "Ready";

        /// <summary>编译失败或超时未见生成类型。</summary>
        public const string COMPILE_FAILED = "CompileFailed";

        /// <summary>跨域重载后无法确认归属。</summary>
        public const string UNKNOWN = "Unknown";
    }
}
#endif
