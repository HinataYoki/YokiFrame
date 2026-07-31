#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 按固定白名单清理项目 `.yokiframe` 中已经完成的协议证据和启动诊断。
    /// </summary>
    public static class YokiFrameFileBridgePruner
    {
        private const string CLEANUP_LOCK_FILE_NAME = "cleanup.lock";
        private const string YOKIFRAME_DIRECTORY_NAME = ".yokiframe";
        private const string ENGINES_DIRECTORY_NAME = "engines";
        private const string COMMANDS_DIRECTORY_NAME = "commands";
        private const string ARCHIVE_DIRECTORY_NAME = "archive";
        private const string DEADLETTER_DIRECTORY_NAME = "deadletter";
        private const string RESULTS_DIRECTORY_NAME = "results";
        private const string JSON_EXTENSION = ".json";
        private const string RESPONSE_FILE_SUFFIX = "-response.json";
        private const string WORKBENCH_DIRECTORY_NAME = "workbench";
        private const string STARTUP_TRACE_PREFIX = "startup-";
        private const string STARTUP_TRACE_SUFFIX = ".jsonl";

        /// <summary>
        /// 清理指定项目的过期协议证据和 Workbench 启动日志。
        /// </summary>
        /// <param name="projectRoot">Unity 或 Godot 项目根目录。</param>
        /// <param name="options">清理策略；为空时使用默认策略。</param>
        /// <param name="nowUtc">测试或调用方提供的当前 UTC 时间；为空时使用系统时间。</param>
        /// <returns>本轮清理报告；目录不存在时返回空报告。</returns>
        public static YokiFrameFileBridgeCleanupReport Prune(
            string projectRoot,
            YokiFrameFileBridgeCleanupOptions? options = null,
            DateTimeOffset? nowUtc = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            options = options ?? new YokiFrameFileBridgeCleanupOptions();
            var fullProjectRoot = Path.GetFullPath(projectRoot);
            var yokiframeRoot = Path.Combine(fullProjectRoot, YOKIFRAME_DIRECTORY_NAME);
            var report = new YokiFrameFileBridgeCleanupReport();
            if (!Directory.Exists(yokiframeRoot))
            {
                return report;
            }

            EnsureSafeDirectory(fullProjectRoot, yokiframeRoot);
            using (var cleanupLock = TryAcquireLock(yokiframeRoot, options.LockTimeout))
            {
                if (cleanupLock == null)
                {
                    report.SkippedDueToLock = true;
                    return report;
                }

                var cleanupTime = nowUtc ?? DateTimeOffset.UtcNow;
                CleanupEngineEvidence(yokiframeRoot, cleanupTime, options, report);
                CleanupWorkbenchTraces(yokiframeRoot, cleanupTime, options, report);
            }

            return report;
        }

        /// <summary>
        /// 清理全部已知 engine 的终态证据目录，不接触 pending、processing、snapshot 和活动状态文件。
        /// </summary>
        private static void CleanupEngineEvidence(
            string yokiframeRoot,
            DateTimeOffset nowUtc,
            YokiFrameFileBridgeCleanupOptions options,
            YokiFrameFileBridgeCleanupReport report)
        {
            var enginesRoot = Path.Combine(yokiframeRoot, ENGINES_DIRECTORY_NAME);
            if (!IsSafeDirectory(enginesRoot))
            {
                return;
            }

            foreach (var engineRoot in Directory.GetDirectories(enginesRoot))
            {
                if (!IsSafeDirectory(engineRoot))
                {
                    continue;
                }

                var commandsRoot = Path.Combine(engineRoot, COMMANDS_DIRECTORY_NAME);
                CleanupDirectory(
                    Path.Combine(commandsRoot, ARCHIVE_DIRECTORY_NAME),
                    options.ArchiveRetention,
                    options.ArchiveMaxFiles,
                    IsJsonFile,
                    nowUtc,
                    report);
                CleanupDirectory(
                    Path.Combine(commandsRoot, DEADLETTER_DIRECTORY_NAME),
                    options.DeadletterRetention,
                    options.DeadletterMaxFiles,
                    IsJsonFile,
                    nowUtc,
                    report);
                CleanupDirectory(
                    Path.Combine(engineRoot, RESULTS_DIRECTORY_NAME),
                    options.ResultsRetention,
                    options.ResultsMaxFiles,
                    IsResponseFile,
                    nowUtc,
                    report);
            }
        }

        /// <summary>
        /// 清理项目级 Workbench 启动诊断；WebView2 数据和窗口状态不属于本规则。
        /// </summary>
        private static void CleanupWorkbenchTraces(
            string yokiframeRoot,
            DateTimeOffset nowUtc,
            YokiFrameFileBridgeCleanupOptions options,
            YokiFrameFileBridgeCleanupReport report)
        {
            CleanupDirectory(
                Path.Combine(yokiframeRoot, WORKBENCH_DIRECTORY_NAME),
                options.StartupTraceRetention,
                options.StartupTraceMaxFiles,
                IsStartupTraceFile,
                nowUtc,
                report);
        }

        /// <summary>
        /// 按更新时间排序，删除超过 TTL 或数量上限的白名单文件。
        /// </summary>
        private static void CleanupDirectory(
            string directoryPath,
            TimeSpan retention,
            int maxFiles,
            Func<string, bool> fileFilter,
            DateTimeOffset nowUtc,
            YokiFrameFileBridgeCleanupReport report)
        {
            if (!IsSafeDirectory(directoryPath))
            {
                return;
            }

            var candidates = new List<FileEntry>();
            foreach (var path in Directory.GetFiles(directoryPath))
            {
                if (!fileFilter(path) || IsReparsePoint(path))
                {
                    continue;
                }

                try
                {
                    var info = new FileInfo(path);
                    candidates.Add(new FileEntry(path, new DateTimeOffset(info.LastWriteTimeUtc), info.Length));
                }
                catch (IOException)
                {
                    report.RecordFailure(path);
                }
                catch (UnauthorizedAccessException)
                {
                    report.RecordFailure(path);
                }
            }

            candidates.Sort(CompareNewestFirst);
            var cutoffUtc = nowUtc - retention;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (index < maxFiles && candidate.LastWriteTimeUtc > cutoffUtc)
                {
                    continue;
                }

                DeleteCandidate(candidate, report);
            }
        }

        /// <summary>
        /// 删除单个候选文件；并发写入或权限错误只记录路径，留待下次清理。
        /// </summary>
        private static void DeleteCandidate(FileEntry candidate, YokiFrameFileBridgeCleanupReport report)
        {
            try
            {
                File.Delete(candidate.Path);
                report.RecordDeleted(candidate.Length);
            }
            catch (IOException)
            {
                report.RecordFailure(candidate.Path);
            }
            catch (UnauthorizedAccessException)
            {
                report.RecordFailure(candidate.Path);
            }
        }

        /// <summary>
        /// 尝试获取项目级排他锁；超时不阻塞宿主本轮业务。
        /// </summary>
        private static FileStream? TryAcquireLock(string yokiframeRoot, TimeSpan timeout)
        {
            var lockPath = Path.Combine(yokiframeRoot, CLEANUP_LOCK_FILE_NAME);
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        return null;
                    }

                    System.Threading.Thread.Sleep(5);
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 校验项目级 `.yokiframe` 目录不是符号链接或 Junction，避免清理逃逸到项目外。
        /// </summary>
        private static void EnsureSafeDirectory(string projectRoot, string yokiframeRoot)
        {
            if (IsReparsePoint(projectRoot) || IsReparsePoint(yokiframeRoot))
            {
                throw new IOException("YokiFrame storage path contains a symbolic link or junction.");
            }
        }

        /// <summary>判断目录或文件是否为重解析点。</summary>
        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        /// <summary>判断现存目录可安全枚举。</summary>
        private static bool IsSafeDirectory(string path)
        {
            return Directory.Exists(path) && !IsReparsePoint(path);
        }

        /// <summary>判断候选是普通 JSON 文件。</summary>
        private static bool IsJsonFile(string path)
        {
            return string.Equals(Path.GetExtension(path), JSON_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>判断候选是 FileBridge terminal response 文件。</summary>
        private static bool IsResponseFile(string path)
        {
            return Path.GetFileName(path).EndsWith(
                RESPONSE_FILE_SUFFIX,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>判断候选是 Workbench 启动诊断 JSONL 文件。</summary>
        private static bool IsStartupTraceFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.StartsWith(STARTUP_TRACE_PREFIX, StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(STARTUP_TRACE_SUFFIX, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>按最新写入时间降序排序，时间相同按路径稳定排序。</summary>
        private static int CompareNewestFirst(FileEntry left, FileEntry right)
        {
            var timeComparison = right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
            return timeComparison != 0
                ? timeComparison
                : string.CompareOrdinal(left.Path, right.Path);
        }

        /// <summary>保存候选文件的稳定删除元数据。</summary>
        private sealed class FileEntry
        {
            /// <summary>创建候选文件记录。</summary>
            /// <param name="path">文件完整路径。</param>
            /// <param name="lastWriteTimeUtc">最后写入时间。</param>
            /// <param name="length">文件长度。</param>
            public FileEntry(string path, DateTimeOffset lastWriteTimeUtc, long length)
            {
                Path = path;
                LastWriteTimeUtc = lastWriteTimeUtc;
                Length = length;
            }

            /// <summary>获取文件完整路径。</summary>
            public string Path { get; }

            /// <summary>获取文件最后写入时间。</summary>
            public DateTimeOffset LastWriteTimeUtc { get; }

            /// <summary>获取文件长度。</summary>
            public long Length { get; }
        }
    }
}

#nullable restore
