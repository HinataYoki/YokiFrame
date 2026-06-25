using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 文件命令桥核心的协议目录存储维护片段。
    /// </summary>
    public sealed partial class YokiCommandBridgeCore
    {
        private string TryReadAllText(string path)
        {
            try
            {
                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, path);
                return File.Exists(path) ? FileBridgeFileSystem.ReadAllTextInRoot(mRootDir, path) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void WriteError(string commandFile, Exception ex)
        {
            var errorPath = Path.Combine(mErrorDir,
                Path.GetFileNameWithoutExtension(commandFile) + "-error.txt");
            TryWriteTextOrRecord(errorPath, $"[{DateTime.UtcNow:O}] {ex}", "error log");
        }

        private bool TryWriteTextOrRecord(string path, string content, string description)
        {
            try
            {
                FileBridgeFileSystem.AtomicWriteAllTextInRoot(mRootDir, path, content);
                return true;
            }
            catch (Exception ex)
            {
                RecordLastError("Failed to write " + description + ": " + ex.Message);
                return false;
            }
        }

        private bool HasTerminalResponse(string path)
        {
            try
            {
                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, path);
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                RecordLastError("Failed to check terminal response: " + ex.Message);
                return false;
            }
        }

        private void RecordLastError(string message)
        {
            mLastErrorUtc = DateTime.UtcNow;
            mLastErrorMessage = message;
        }

        private void CleanupExpiredFiles()
        {
            var deleted = 0;
            deleted += DeleteExpiredFiles(mCommandDir, "*.tmp", mOptions.TempFileTtl);
            deleted += DeleteExpiredFiles(mProcessingDir, "*.tmp", mOptions.TempFileTtl);
            deleted += DeleteExpiredFiles(mResultDir, "*.tmp", mOptions.TempFileTtl);
            deleted += DeleteExpiredFiles(mErrorDir, "*.tmp", mOptions.TempFileTtl);
            deleted += DeleteExpiredFiles(mEventDir, "*.tmp", mOptions.TempFileTtl);
            deleted += DeleteExpiredFiles(mArchiveDir, "*.json", mOptions.ArchiveFileTtl);
            deleted += DeleteExpiredFiles(mDeadletterDir, "*.json", mOptions.DeadletterFileTtl);
            deleted += DeleteExpiredFiles(mResultDir, "*-response.json", mOptions.ResultFileTtl);
            deleted += DeleteExpiredFiles(mErrorDir, "*-error.txt", mOptions.ErrorFileTtl);
            deleted += DeleteExpiredFiles(mEventDir, "*.jsonl", mOptions.EventFileTtl);
            deleted += DeleteExpiredFiles(mEventDir, "*.archive", mOptions.EventFileTtl);

            mLastCleanupSweepUtc = DateTime.UtcNow;

            if (deleted > 0)
            {
                mCleanedFileCount += deleted;
                mLastCleanupUtc = mLastCleanupSweepUtc;
            }
        }

        private int DeleteExpiredFiles(string dir, string pattern, TimeSpan ttl)
        {
            if (ttl < TimeSpan.Zero || !Directory.Exists(dir))
                return 0;

            var deleted = 0;
            string[] files;
            try
            {
                files = FileBridgeFileSystem.GetFilesInRoot(mRootDir, dir, pattern);
            }
            catch
            {
                return 0;
            }

            var now = DateTime.UtcNow;
            for (var i = 0; i < files.Length; i++)
            {
                try
                {
                    FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, files[i]);
                    if (now - File.GetLastWriteTimeUtc(files[i]) < ttl)
                        continue;

                    FileBridgeFileSystem.TryDeleteInRoot(mRootDir, files[i]);
                    deleted++;
                }
                catch
                {
                    // 清理是后台维护能力，失败时不应阻断命令处理。
                }
            }

            return deleted;
        }

        private StatusStorageSnapshot GetStatusStorageSnapshot()
        {
            var nowUtc = DateTime.UtcNow;
            var cacheDuration = mOptions.StatusStorageCacheDuration;
            if (cacheDuration > TimeSpan.Zero && mStatusStorageSnapshotUtc.HasValue &&
                nowUtc - mStatusStorageSnapshotUtc.Value < cacheDuration)
            {
                return mStatusStorageSnapshot;
            }

            var snapshot = new StatusStorageSnapshot();
            snapshot.ArchiveCommandCount = CountFilesBounded(mArchiveDir, "*.json", null, ref snapshot.Truncated);
            snapshot.DeadletterCommandCount = CountFilesBounded(mDeadletterDir, "*.json", null, ref snapshot.Truncated);
            snapshot.ResultCount = CountFilesBounded(mResultDir, "*-response.json", null, ref snapshot.Truncated);
            snapshot.ErrorCount = CountFilesBounded(mErrorDir, "*-error.txt", null, ref snapshot.Truncated);
            snapshot.SnapshotCount = CountFilesRecursiveBounded(mSnapshotDir, "*.json", ref snapshot.Truncated);
            snapshot.ProtocolStats = CollectProtocolStorageStats();
            snapshot.Truncated = snapshot.Truncated || snapshot.ProtocolStats.Truncated;

            mStatusStorageSnapshot = snapshot;
            mStatusStorageSnapshotUtc = nowUtc;
            return snapshot;
        }

        private int CountFiles(string dir, string pattern, Func<string, bool> predicate)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return 0;

                var files = FileBridgeFileSystem.GetFilesInRoot(mRootDir, dir, pattern);
                if (predicate == null)
                    return files.Length;

                var count = 0;
                for (var i = 0; i < files.Length; i++)
                {
                    if (predicate(files[i]))
                        count++;
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private int CountFilesBounded(string dir, string pattern, Func<string, bool> predicate, ref bool truncated)
        {
            var limit = mOptions.StatusStorageScanFileLimit;
            if (limit <= 0)
                return CountFiles(dir, pattern, predicate);

            try
            {
                if (!Directory.Exists(dir))
                    return 0;

                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, dir);
                var count = 0;
                foreach (var file in Directory.EnumerateFiles(dir, pattern))
                {
                    if (!FileBridgeFileSystem.IsPathSafeWithinRoot(mRootDir, file))
                        continue;

                    if (predicate == null || predicate(file))
                    {
                        count++;
                        if (count >= limit)
                        {
                            truncated = true;
                            break;
                        }
                    }
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private ProtocolStorageStats CollectProtocolStorageStats()
        {
            var stats = new ProtocolStorageStats();
            AccumulateProtocolFiles(ref stats, mCommandDir, "*.json");
            AccumulateProtocolFiles(ref stats, mCommandDir, "*.tmp");
            AccumulateProtocolFiles(ref stats, mProcessingDir, "*.json");
            AccumulateProtocolFiles(ref stats, mProcessingDir, "*.tmp");
            AccumulateProtocolFiles(ref stats, mArchiveDir, "*.json");
            AccumulateProtocolFiles(ref stats, mDeadletterDir, "*.json");
            AccumulateProtocolFiles(ref stats, mResultDir, "*-response.json");
            AccumulateProtocolFiles(ref stats, mResultDir, "*.tmp");
            AccumulateProtocolFiles(ref stats, mErrorDir, "*-error.txt");
            AccumulateProtocolFiles(ref stats, mErrorDir, "*.tmp");
            AccumulateProtocolFiles(ref stats, mEventDir, "*.jsonl");
            AccumulateProtocolFiles(ref stats, mEventDir, "*.archive");
            AccumulateProtocolFiles(ref stats, mEventDir, "*.tmp");
            AccumulateProtocolFilesRecursive(ref stats, mSnapshotDir, "*.json");
            AccumulateProtocolFilesRecursive(ref stats, mSnapshotDir, "*.tmp");
            return stats;
        }

        private void AccumulateProtocolFiles(ref ProtocolStorageStats stats, string dir, string pattern)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return;

                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, dir);
                var limit = mOptions.StatusStorageScanFileLimit;
                var count = 0;
                foreach (var file in Directory.EnumerateFiles(dir, pattern))
                {
                    try
                    {
                        if (!FileBridgeFileSystem.IsPathSafeWithinRoot(mRootDir, file))
                            continue;

                        var info = new FileInfo(file);
                        if (!info.Exists)
                            continue;

                        stats.FileCount++;
                        stats.TotalBytes += info.Length;
                        var lastWriteUtc = info.LastWriteTimeUtc;
                        if (!stats.OldestFileUtc.HasValue || lastWriteUtc < stats.OldestFileUtc.Value)
                            stats.OldestFileUtc = lastWriteUtc;

                        count++;
                        if (limit > 0 && count >= limit)
                        {
                            stats.Truncated = true;
                            break;
                        }
                    }
                    catch
                    {
                        // 统计只用于诊断，单个文件不可读时跳过，避免影响命令处理。
                    }
                }
            }
            catch
            {
                // 统计失败不应阻断 bridge_status。
            }
        }

        private int CountFilesRecursive(string dir, string pattern)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return 0;

                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, dir);
                var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                var count = 0;
                for (var i = 0; i < files.Length; i++)
                {
                    if (FileBridgeFileSystem.IsPathSafeWithinRoot(mRootDir, files[i]))
                        count++;
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private int CountFilesRecursiveBounded(string dir, string pattern, ref bool truncated)
        {
            var limit = mOptions.StatusStorageScanFileLimit;
            if (limit <= 0)
                return CountFilesRecursive(dir, pattern);

            try
            {
                if (!Directory.Exists(dir))
                    return 0;

                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, dir);
                var count = 0;
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                {
                    if (!FileBridgeFileSystem.IsPathSafeWithinRoot(mRootDir, file))
                        continue;

                    count++;
                    if (count >= limit)
                    {
                        truncated = true;
                        break;
                    }
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private void AccumulateProtocolFilesRecursive(ref ProtocolStorageStats stats, string dir, string pattern)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return;

                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, dir);
                var limit = mOptions.StatusStorageScanFileLimit;
                var count = 0;
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                {
                    if (!FileBridgeFileSystem.IsPathSafeWithinRoot(mRootDir, file))
                        continue;

                    try
                    {
                        var info = new FileInfo(file);
                        if (!info.Exists)
                            continue;

                        stats.FileCount++;
                        stats.TotalBytes += info.Length;
                        var lastWriteUtc = info.LastWriteTimeUtc;
                        if (!stats.OldestFileUtc.HasValue || lastWriteUtc < stats.OldestFileUtc.Value)
                            stats.OldestFileUtc = lastWriteUtc;

                        count++;
                        if (limit > 0 && count >= limit)
                        {
                            stats.Truncated = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Snapshot 统计失败只影响诊断完整度，不应影响 bridge_status。
                    }
                }
            }
            catch
            {
                // 统计失败不应阻断 bridge_status。
            }
        }

        private static string GetUniquePath(string dir, string fileName)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path))
                return path;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            for (var i = 1; i < 10000; i++)
            {
                path = Path.Combine(dir, name + "-" + i + ext);
                if (!File.Exists(path))
                    return path;
            }

            return Path.Combine(dir, name + "-" + Guid.NewGuid().ToString("N") + ext);
        }

        private struct ProtocolStorageStats
        {
            /// <summary>协议目录内参与统计的文件数量。</summary>
            public long FileCount;

            /// <summary>协议目录内参与统计的文件总字节数。</summary>
            public long TotalBytes;

            /// <summary>参与统计文件中最早的最后写入时间。</summary>
            public DateTime? OldestFileUtc;

            /// <summary>统计是否因扫描上限而被截断。</summary>
            public bool Truncated;
        }

        private struct StatusStorageSnapshot
        {
            /// <summary>archive 目录中的命令文件数量。</summary>
            public int ArchiveCommandCount;

            /// <summary>deadletter 目录中的命令文件数量。</summary>
            public int DeadletterCommandCount;

            /// <summary>results 目录中的响应文件数量。</summary>
            public int ResultCount;

            /// <summary>errors 目录中的错误文件数量。</summary>
            public int ErrorCount;

            /// <summary>snapshot 目录中的快照文件数量。</summary>
            public int SnapshotCount;

            /// <summary>统计是否因扫描上限而被截断。</summary>
            public bool Truncated;

            /// <summary>协议目录的聚合存储统计。</summary>
            public ProtocolStorageStats ProtocolStats;
        }
    }
}
