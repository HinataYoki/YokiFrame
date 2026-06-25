using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// YokiFrame 命令桥核心（纯 C#，跨引擎复用）
    /// 负责轮询 engine-scoped commands 目录，执行命令，并写入同一 engine 的 results。
    /// 引擎驱动壳（Adapter 层）负责：
    /// 1. 注入根路径（Unity: Path.GetDirectoryName(Application.dataPath), Godot: res://../）
    /// 2. 定时调用 Poll()（Unity: EditorApplication.update, Godot: _Process）
    /// </summary>
    public sealed partial class YokiCommandBridgeCore
    {
        private readonly string mRootDir;
        private readonly string mCommandDir;
        private readonly string mProcessingDir;
        private readonly string mArchiveDir;
        private readonly string mDeadletterDir;
        private readonly string mResultDir;
        private readonly string mErrorDir;
        private readonly string mEventDir;
        private readonly string mSnapshotDir;
        private readonly KitCommandDispatcher mDispatcher;
        private readonly YokiCommandBridgeOptions mOptions;
        private DateTime? mLastPollUtc;
        private DateTime? mLastCleanupUtc;
        private DateTime? mLastCleanupSweepUtc;
        private DateTime? mLastErrorUtc;
        private string mLastErrorMessage;
        private string mActiveProcessingPath;
        private long mProcessedCommandCount;
        private long mDeadletterCommandCount;
        private long mCleanedFileCount;
        private long mDuplicateCommandCount;
        private long mPayloadTooLargeCount;
        private long mResultTooLargeCount;
        private long mBridgeBusyCount;
        private bool mBackpressureActive;
        private string mLastPollLimitReason;
        private DateTime? mStatusStorageSnapshotUtc;
        private StatusStorageSnapshot mStatusStorageSnapshot;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="yokiframeRootDir">引擎注入的根路径（如 "F:/YokiFrame2/.yokiframe"）</param>
        /// <param name="dispatcher">Kit 命令分发器</param>
        public YokiCommandBridgeCore(string yokiframeRootDir, KitCommandDispatcher dispatcher)
            : this(yokiframeRootDir, dispatcher, null)
        {
        }

        /// <summary>
        /// 使用指定命令桥配置创建核心实例。
        /// </summary>
        /// <param name="yokiframeRootDir">.yokiframe 协议根目录。</param>
        /// <param name="dispatcher">Kit 命令分发器。</param>
        /// <param name="options">命令桥运行配置；传入空值时使用默认配置。</param>
        public YokiCommandBridgeCore(string yokiframeRootDir, KitCommandDispatcher dispatcher, YokiCommandBridgeOptions options)
        {
            if (string.IsNullOrEmpty(yokiframeRootDir))
                throw new ArgumentException("根路径不能为空", nameof(yokiframeRootDir));

            mDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            mOptions = options ?? new();
            mRootDir = Path.GetFullPath(yokiframeRootDir);
            Directory.CreateDirectory(mRootDir);
            FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, mRootDir);

            mCommandDir = Path.Combine(mRootDir, "commands");
            mProcessingDir = Path.Combine(mCommandDir, "processing");
            mArchiveDir = Path.Combine(mCommandDir, "archive");
            mDeadletterDir = Path.Combine(mCommandDir, "deadletter");
            mResultDir = Path.Combine(mRootDir, "results");
            mErrorDir = Path.Combine(mRootDir, "results", "errors");
            mEventDir = Path.Combine(mRootDir, "events");
            mSnapshotDir = Path.Combine(mRootDir, "snapshots");
            mDispatcher.DefaultEngineId = mOptions.EngineId;

            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mCommandDir);
            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mProcessingDir);
            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mArchiveDir);
            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mDeadletterDir);
            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mResultDir);
            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mErrorDir);
            FileBridgeFileSystem.CreateDirectoryInRoot(mRootDir, mSnapshotDir);
        }

        /// <summary>
        /// 轮询命令目录，处理所有待处理命令
        /// 由引擎驱动壳（Adapter 层）在主循环中调用
        /// </summary>
        public void Poll()
        {
            mLastPollUtc = DateTime.UtcNow;
            mBackpressureActive = false;
            mLastPollLimitReason = string.Empty;
            var pollStartUtc = mLastPollUtc.Value;
            try
            {
                if (ShouldRunCleanup(mLastPollUtc.Value))
                    CleanupExpiredFiles();
                RecoverStaleProcessingCommands();

                var files = FileBridgeFileSystem.GetFilesInRoot(mRootDir, mCommandDir, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                ApplyPendingQueueLimit(files);
                var processedThisPoll = 0;
                foreach (var file in files)
                {
                    if (!File.Exists(file))
                        continue;

                    if (mOptions.MaxCommandsPerPoll > 0 && processedThisPoll >= mOptions.MaxCommandsPerPoll)
                    {
                        mBackpressureActive = true;
                        mLastPollLimitReason = "MaxCommandsPerPoll";
                        break;
                    }

                    if (!IsCommandReadyForClaim(file))
                        continue;

                    var processingPath = Path.Combine(mProcessingDir, Path.GetFileName(file));
                    if (!FileBridgeFileSystem.TryClaimFileInRoot(mRootDir, file, processingPath))
                        continue;

                    ProcessClaimedCommand(processingPath);
                    processedThisPoll++;
                    if (IsPollTimeBudgetExceeded(pollStartUtc))
                    {
                        mBackpressureActive = true;
                        mLastPollLimitReason = "PollTimeBudgetMs";
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                RecordLastError(ex.Message);
            }
        }

        private bool ShouldRunCleanup(DateTime nowUtc)
        {
            var interval = mOptions.CleanupInterval;
            if (interval <= TimeSpan.Zero || !mLastCleanupSweepUtc.HasValue)
                return true;

            return nowUtc - mLastCleanupSweepUtc.Value >= interval;
        }
    }
}
