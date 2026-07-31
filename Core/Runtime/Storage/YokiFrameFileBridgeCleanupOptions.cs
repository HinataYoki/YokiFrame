using System;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 定义 `.yokiframe` 协议证据与 Workbench 启动日志的保留边界。
    /// </summary>
    public sealed class YokiFrameFileBridgeCleanupOptions
    {
        /// <summary>archive 默认保留天数。</summary>
        public const int DEFAULT_ARCHIVE_RETENTION_DAYS = 7;

        /// <summary>results 默认保留天数。</summary>
        public const int DEFAULT_RESULTS_RETENTION_DAYS = 7;

        /// <summary>deadletter 默认保留天数。</summary>
        public const int DEFAULT_DEADLETTER_RETENTION_DAYS = 30;

        /// <summary>Workbench 启动日志默认保留天数。</summary>
        public const int DEFAULT_STARTUP_TRACE_RETENTION_DAYS = 14;

        /// <summary>archive 默认最大文件数。</summary>
        public const int DEFAULT_ARCHIVE_MAX_FILES = 200;

        /// <summary>results 默认最大文件数。</summary>
        public const int DEFAULT_RESULTS_MAX_FILES = 200;

        /// <summary>deadletter 默认最大文件数。</summary>
        public const int DEFAULT_DEADLETTER_MAX_FILES = 200;

        /// <summary>Workbench 启动日志默认最大文件数。</summary>
        public const int DEFAULT_STARTUP_TRACE_MAX_FILES = 20;

        /// <summary>清理锁默认等待时间；拿不到锁时本轮跳过，避免阻塞宿主。</summary>
        public static readonly TimeSpan DEFAULT_LOCK_TIMEOUT = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// 创建默认清理策略。
        /// </summary>
        public YokiFrameFileBridgeCleanupOptions()
            : this(
                TimeSpan.FromDays(DEFAULT_ARCHIVE_RETENTION_DAYS),
                DEFAULT_ARCHIVE_MAX_FILES,
                TimeSpan.FromDays(DEFAULT_RESULTS_RETENTION_DAYS),
                DEFAULT_RESULTS_MAX_FILES,
                TimeSpan.FromDays(DEFAULT_DEADLETTER_RETENTION_DAYS),
                DEFAULT_DEADLETTER_MAX_FILES,
                TimeSpan.FromDays(DEFAULT_STARTUP_TRACE_RETENTION_DAYS),
                DEFAULT_STARTUP_TRACE_MAX_FILES,
                DEFAULT_LOCK_TIMEOUT)
        {
        }

        /// <summary>
        /// 创建指定保留周期和数量上限的清理策略。
        /// </summary>
        /// <param name="archiveRetention">archive 文件最长保留时间。</param>
        /// <param name="archiveMaxFiles">archive 目录最多保留文件数。</param>
        /// <param name="resultsRetention">results 文件最长保留时间。</param>
        /// <param name="resultsMaxFiles">results 目录最多保留文件数。</param>
        /// <param name="deadletterRetention">deadletter 文件最长保留时间。</param>
        /// <param name="deadletterMaxFiles">deadletter 目录最多保留文件数。</param>
        /// <param name="startupTraceRetention">Workbench 启动日志最长保留时间。</param>
        /// <param name="startupTraceMaxFiles">Workbench 启动日志最多保留文件数。</param>
        /// <param name="lockTimeout">获取项目清理锁的最长等待时间。</param>
        public YokiFrameFileBridgeCleanupOptions(
            TimeSpan archiveRetention,
            int archiveMaxFiles,
            TimeSpan resultsRetention,
            int resultsMaxFiles,
            TimeSpan deadletterRetention,
            int deadletterMaxFiles,
            TimeSpan startupTraceRetention,
            int startupTraceMaxFiles,
            TimeSpan lockTimeout)
        {
            ValidateRetention(archiveRetention, nameof(archiveRetention));
            ValidateRetention(resultsRetention, nameof(resultsRetention));
            ValidateRetention(deadletterRetention, nameof(deadletterRetention));
            ValidateRetention(startupTraceRetention, nameof(startupTraceRetention));
            ValidateCount(archiveMaxFiles, nameof(archiveMaxFiles));
            ValidateCount(resultsMaxFiles, nameof(resultsMaxFiles));
            ValidateCount(deadletterMaxFiles, nameof(deadletterMaxFiles));
            ValidateCount(startupTraceMaxFiles, nameof(startupTraceMaxFiles));
            ValidateLockTimeout(lockTimeout);

            ArchiveRetention = archiveRetention;
            ArchiveMaxFiles = archiveMaxFiles;
            ResultsRetention = resultsRetention;
            ResultsMaxFiles = resultsMaxFiles;
            DeadletterRetention = deadletterRetention;
            DeadletterMaxFiles = deadletterMaxFiles;
            StartupTraceRetention = startupTraceRetention;
            StartupTraceMaxFiles = startupTraceMaxFiles;
            LockTimeout = lockTimeout;
        }

        /// <summary>获取 archive 文件最长保留时间。</summary>
        public TimeSpan ArchiveRetention { get; }

        /// <summary>获取 archive 目录最多保留文件数。</summary>
        public int ArchiveMaxFiles { get; }

        /// <summary>获取 results 文件最长保留时间。</summary>
        public TimeSpan ResultsRetention { get; }

        /// <summary>获取 results 目录最多保留文件数。</summary>
        public int ResultsMaxFiles { get; }

        /// <summary>获取 deadletter 文件最长保留时间。</summary>
        public TimeSpan DeadletterRetention { get; }

        /// <summary>获取 deadletter 目录最多保留文件数。</summary>
        public int DeadletterMaxFiles { get; }

        /// <summary>获取 Workbench 启动日志最长保留时间。</summary>
        public TimeSpan StartupTraceRetention { get; }

        /// <summary>获取 Workbench 启动日志最多保留文件数。</summary>
        public int StartupTraceMaxFiles { get; }

        /// <summary>获取清理锁最长等待时间。</summary>
        public TimeSpan LockTimeout { get; }

        /// <summary>校验保留周期必须为正且不是无限值。</summary>
        private static void ValidateRetention(TimeSpan value, string parameterName)
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Retention must be positive.");
            }
        }

        /// <summary>校验数量上限必须为正。</summary>
        private static void ValidateCount(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Maximum file count must be positive.");
            }
        }

        /// <summary>校验清理锁等待时间不能为负或无限。</summary>
        private static void ValidateLockTimeout(TimeSpan value)
        {
            if (value < TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Lock timeout must be finite and non-negative.");
            }
        }
    }
}
