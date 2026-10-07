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
    /// 运行记录与请求索引的持久化存储（§10 / §10.1）：原子写、独占建索引、TTL 回收。
    /// </summary>
    /// <remarks>
    /// 根目录固定为 <c>&lt;project&gt;/.yokiframe/engine/runs/</c>：不在 Assets 下，不触发导入。
    /// 记录不含业务数据，只有入口名与参数摘要（§13）。
    /// </remarks>
    public sealed partial class RoslynRunStore
    {
        /// <summary>运行记录相对项目根的目录。</summary>
        public const string RUNS_RELATIVE_PATH = ".yokiframe/engine/runs";

        private const string INDEX_FOLDER = "index";
        private const string RESULT_SUFFIX = ".result.json";

        /// <summary>认领租约文件后缀；刻意不是 .json，避免被 ReadAll 当成运行记录。</summary>
        private const string CLAIM_SUFFIX = ".claim";
        private static readonly UTF8Encoding sUtf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly string mRunsRoot;
        /// <summary>Local writes invalidate the scheduler's bounded recent-history cache.</summary>
        public long Revision { get; private set; }

        /// <summary>创建存储。</summary>
        /// <param name="projectRoot">项目根目录。</param>
        public RoslynRunStore(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("projectRoot is required.", nameof(projectRoot));
            }

            mRunsRoot = Path.Combine(Path.GetFullPath(projectRoot), ".yokiframe", "engine", "runs");
        }

        /// <summary>获取运行记录根目录。</summary>
        public string RunsRoot
        {
            get { return mRunsRoot; }
        }

        /// <summary>写入运行记录（原子替换）。</summary>
        /// <param name="record">运行记录。</param>
        public void Save(RoslynRunRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (!IsSafeId(record.RunId))
            {
                throw new ArgumentException("runId is not a safe identifier.", nameof(record));
            }

            WriteAtomic(RecordPath(record.RunId), BuildRecordJson(record));
            Revision++;
        }

        /// <summary>读取运行记录。</summary>
        /// <param name="runId">运行标识。</param>
        /// <param name="record">运行记录。</param>
        /// <returns>读取并解析成功时返回 true。</returns>
        public bool TryReadRun(string runId, out RoslynRunRecord record)
        {
            record = null;
            if (!IsSafeId(runId))
            {
                return false;
            }

            string path = RecordPath(runId);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                return TryParseRecord(File.ReadAllText(path, sUtf8NoBom), out record);
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// 原子认领一条待运行记录（跨进程互斥）。
        /// </summary>
        /// <remarks>
        /// 同一项目目录下可能存在多个宿主（旧编辑器会话、另一个编辑器实例）。仅凭记录状态无法互斥，
        /// 因此用「独占创建认领文件」提供原子性：只有一个进程能 CreateNew 成功；文件已存在时，
        /// 只有**租约过期**（或内容不可读）才允许接管，避免两个宿主同时执行同一条运行。
        /// 认领文件刻意不带 .json 后缀，因此不会被 <see cref="ReadAll"/> 当成运行记录。
        /// </remarks>
        /// <param name="runId">运行标识。</param>
        /// <param name="ownerSessionId">认领方会话标识。</param>
        /// <param name="generation">认领方域代次。</param>
        /// <param name="lease">租约时长；过期后可被其它宿主接管。</param>
        /// <param name="record">认领到的记录。</param>
        /// <returns>认领成功时返回 true。</returns>
        public bool TryClaim(
            string runId,
            string ownerSessionId,
            long generation,
            TimeSpan lease,
            out RoslynRunRecord record)
        {
            record = null;
            if (!IsSafeId(runId)
                || !TryReadRun(runId, out RoslynRunRecord current)
                || current == null)
            {
                return false;
            }

            // 只有待运行状态可被认领：Running/终态说明已有宿主在执行或已结束。
            if (current.State != RunStatus.Queued)
            {
                return false;
            }

            if (!TryAcquireClaim(ClaimPath(runId), ownerSessionId, generation, lease)) return false;
            record = current;
            return true;
        }

        /// <summary>释放认领文件（记录进入终态后调用；不存在时直接返回）。</summary>
        /// <param name="runId">运行标识。</param>
        public void ReleaseClaim(string runId)
        {
            if (!IsSafeId(runId))
            {
                return;
            }

            string path = ClaimPath(runId);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // 已被其它宿主清理：忽略。
            }
            catch (UnauthorizedAccessException)
            {
                // 无权限：忽略（下一次扫描还会尝试）。
            }
        }

        /// <summary>读取全部运行记录，按提交时间升序。</summary>
        /// <returns>运行记录列表。</returns>
        public IReadOnlyList<RoslynRunRecord> ReadAll()
        {
            var records = new List<RoslynRunRecord>();
            if (!Directory.Exists(mRunsRoot))
            {
                return records;
            }

            string[] files = Directory.GetFiles(mRunsRoot, "*.json", SearchOption.TopDirectoryOnly);
            for (var index = 0; index < files.Length; index++)
            {
                if (files[index].EndsWith(RESULT_SUFFIX, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    if (TryParseRecord(File.ReadAllText(files[index], sUtf8NoBom), out RoslynRunRecord record))
                    {
                        records.Add(record);
                    }
                }
                catch (IOException)
                {
                    // 读不到就跳过：记录损坏不应影响其它运行。
                }
            }

            records.Sort(static (left, right) => left.SubmittedAtUtc.CompareTo(right.SubmittedAtUtc));
            return records;
        }

        /// <summary>
        /// 为 requestId 建立索引；已存在时读回既有 runId（幂等，不创建第二个运行）。
        /// </summary>
        /// <param name="requestId">命令 requestId。</param>
        /// <param name="runId">本次要建立的运行标识。</param>
        /// <param name="existingRunId">实际生效的 runId（已存在时为既有值）。</param>
        /// <returns>本次创建成功返回 true，命中既有索引返回 false。</returns>
        public bool TryCreateRequestIndex(string requestId, string runId, out string existingRunId)
        {
            existingRunId = string.Empty;
            if (!IsSafeId(requestId) || !IsSafeId(runId))
            {
                return false;
            }

            string path = IndexPath(requestId);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, sUtf8NoBom))
                {
                    writer.Write(BuildIndexJson(requestId, runId));
                }

                existingRunId = runId;
                return true;
            }
            catch (IOException)
            {
                existingRunId = ReadIndexRunIdWithRetry(path);
                return false;
            }
        }

        /// <summary>读取请求索引。</summary>
        /// <param name="requestId">命令 requestId。</param>
        /// <param name="runId">运行标识。</param>
        /// <returns>命中时返回 true。</returns>
        public bool TryReadRequestIndex(string requestId, out string runId)
        {
            runId = string.Empty;
            if (!IsSafeId(requestId))
            {
                return false;
            }

            string path = IndexPath(requestId);
            if (!File.Exists(path))
            {
                return false;
            }

            runId = ReadIndexRunIdWithRetry(path);
            return runId.Length > 0;
        }

        /// <summary>写入终态结果文件。</summary>
        /// <param name="runId">运行标识。</param>
        /// <param name="result">入口结果。</param>
        /// <returns>结果文件的相对路径；失败时为空。</returns>
        public string SaveResult(string runId, RunResult result)
        {
            if (!IsSafeId(runId) || result == null)
            {
                return string.Empty;
            }

            WriteAtomic(ResultPath(runId), BuildResultJson(result));
            return Path.Combine(".yokiframe", "engine", "runs", runId + RESULT_SUFFIX).Replace('\\', '/');
        }

        /// <summary>读取结果文件原文。</summary>
        /// <param name="relativePath">记录里的结果路径。</param>
        /// <param name="json">结果 JSON。</param>
        /// <returns>读取成功时返回 true。</returns>
        public bool TryReadResult(string relativePath, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string fileName = Path.GetFileName(relativePath);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            string path = Path.Combine(mRunsRoot, fileName);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                json = File.ReadAllText(path, sUtf8NoBom);
                return json.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// 按 TTL 回收终态记录、结果文件与请求索引。
        /// </summary>
        /// <param name="nowUtc">当前时间。</param>
        /// <param name="ttl">终态记录保留时长。</param>
        /// <returns>回收的记录条数。</returns>
        public int Prune(DateTime nowUtc, TimeSpan ttl)
        {
            IReadOnlyList<RoslynRunRecord> records = ReadAll();
            int removed = 0;
            for (var index = 0; index < records.Count; index++)
            {
                RoslynRunRecord record = records[index];
                if (!record.IsTerminal)
                {
                    continue;
                }

                DateTime basis = record.ExpiresAtUtc ?? record.UpdatedAtUtc;
                bool expired = record.ExpiresAtUtc.HasValue
                    ? record.ExpiresAtUtc.Value <= nowUtc
                    : basis.Add(ttl) <= nowUtc;
                if (!expired)
                {
                    continue;
                }

                DeleteFile(RecordPath(record.RunId));
                DeleteFile(ResultPath(record.RunId));
                if (IsSafeId(record.RequestId))
                {
                    DeleteFile(IndexPath(record.RequestId));
                }

                removed++;
            }

            if (removed > 0) Revision++;
            return removed;
        }

        private string RecordPath(string runId)
        {
            return Path.Combine(mRunsRoot, runId + ".json");
        }

        private string ResultPath(string runId)
        {
            return Path.Combine(mRunsRoot, runId + RESULT_SUFFIX);
        }

        private string IndexPath(string requestId)
        {
            return Path.Combine(mRunsRoot, INDEX_FOLDER, requestId + ".json");
        }

        private static void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // 回收失败不影响其它记录。
            }
        }

        private static void WriteAtomic(string path, string content)
        {
            // 复用共享原子写（唯一物理事实源）：同目录 GUID 临时文件 + flush + 替换/备份恢复兜底。
            // 原先这里私有复制了一份，用固定 ".tmp" 名——多进程同时写同一路径会互相踩，且缺少备份恢复路径。
            YokiFrameAtomicFileWriter.WriteAllText(path, content);
        }

        private static string ReadIndexRunIdWithRetry(string path)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    string json = File.ReadAllText(path, sUtf8NoBom);
                    if (TryParseIndex(json, out string runId))
                    {
                        return runId;
                    }
                }
                catch (IOException)
                {
                    // 写入方可能刚创建文件，重试即可。
                }

                System.Threading.Thread.Sleep(20);
            }

            return string.Empty;
        }

    }
}
#endif
