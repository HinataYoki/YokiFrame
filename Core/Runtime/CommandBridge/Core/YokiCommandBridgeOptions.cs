using System;

namespace YokiFrame
{
    /// <summary>
    /// 文件命令桥运行参数。保持在 Base 层，供 Unity/Godot 等 Adapter 注入自己的引擎标识。
    /// </summary>
    public sealed class YokiCommandBridgeOptions
    {
        private const int DEFAULT_MAX_COMMANDS_PER_POLL = 128;
        private const int DEFAULT_MAX_PENDING_COMMANDS = 2048;

        /// <summary>
        /// 当前宿主引擎标识，会写入命令响应和状态诊断。
        /// </summary>
        public string EngineId { get; set; } = "base";

        /// <summary>
        /// processing 中的命令超过该时间仍未结束时，会写入标准错误响应并移入 deadletter。
        /// </summary>
        public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromSeconds(8);

        /// <summary>
        /// 新出现且结构未闭合的命令文件会在该宽限期内保持 pending，避免消费半写入文件。
        /// </summary>
        public TimeSpan IncompleteCommandGracePeriod { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 单次 Poll 最多处理的命令数；小于等于 0 表示不限制。
        /// </summary>
        public int MaxCommandsPerPoll { get; set; } = DEFAULT_MAX_COMMANDS_PER_POLL;

        /// <summary>
        /// 单次 Poll 的时间预算毫秒数；小于等于 0 表示不限制。
        /// </summary>
        public int PollTimeBudgetMs { get; set; }

        /// <summary>
        /// pending 命令队列允许保留的最大命令数；小于等于 0 表示不限制。
        /// </summary>
        public int MaxPendingCommands { get; set; } = DEFAULT_MAX_PENDING_COMMANDS;

        /// <summary>
        /// 单个命令文件允许的最大 UTF-8 字节数；小于等于 0 表示不限制。
        /// </summary>
        public long MaxPayloadBytes { get; set; }

        /// <summary>
        /// 单个响应文件允许的最大 UTF-8 字节数；小于等于 0 表示不限制。
        /// </summary>
        public long MaxResultBytes { get; set; }

        /// <summary>
        /// 协议目录维护（过期文件清理）的最小执行间隔；小于等于 0 表示每次 Poll 都执行。
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// bridge_status 中诊断型存储统计的单目录最大扫描文件数；小于等于 0 表示不限制。
        /// </summary>
        public int StatusStorageScanFileLimit { get; set; } = 512;

        /// <summary>
        /// bridge_status 中诊断型存储统计的缓存时间。pending/processing 等控制面队列字段仍实时计算。
        /// </summary>
        public TimeSpan StatusStorageCacheDuration { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 临时文件保留时间；小于 0 表示不自动清理。
        /// </summary>
        public TimeSpan TempFileTtl { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 已归档命令文件保留时间；小于 0 表示不自动清理。
        /// </summary>
        public TimeSpan ArchiveFileTtl { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// deadletter 命令文件保留时间；小于 0 表示不自动清理。
        /// </summary>
        public TimeSpan DeadletterFileTtl { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// 响应文件保留时间；小于 0 表示不自动清理。
        /// </summary>
        public TimeSpan ResultFileTtl { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// 错误文件保留时间；小于 0 表示不自动清理。
        /// </summary>
        public TimeSpan ErrorFileTtl { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// 事件文件保留时间；小于 0 表示不自动清理。
        /// </summary>
        public TimeSpan EventFileTtl { get; set; } = TimeSpan.FromDays(1);
    }

    /// <summary>
    /// 命令桥轮询退避策略：空闲时降低扫描频率，有活动或背压时回到最低延迟。
    /// </summary>
    public sealed class CommandBridgePollBackoff
    {
        private readonly int mMinIntervalMs;
        private readonly int mMaxIntervalMs;

        /// <summary>
        /// 当前建议轮询间隔，单位毫秒。
        /// </summary>
        public int CurrentIntervalMs { get; private set; }

        /// <summary>
        /// 创建轮询退避策略。
        /// </summary>
        /// <param name="minIntervalMs">最低轮询间隔。</param>
        /// <param name="maxIntervalMs">最高轮询间隔。</param>
        public CommandBridgePollBackoff(int minIntervalMs, int maxIntervalMs)
        {
            mMinIntervalMs = Math.Max(1, minIntervalMs);
            mMaxIntervalMs = Math.Max(mMinIntervalMs, maxIntervalMs);
            CurrentIntervalMs = mMinIntervalMs;
        }

        /// <summary>
        /// 记录一次轮询结果，并更新下一次轮询间隔。
        /// </summary>
        /// <param name="hadActivity">本次轮询是否处理了命令或遇到背压。</param>
        public void RecordPollResult(bool hadActivity)
        {
            if (hadActivity)
            {
                CurrentIntervalMs = mMinIntervalMs;
                return;
            }

            var next = CurrentIntervalMs * 2;
            CurrentIntervalMs = next > mMaxIntervalMs ? mMaxIntervalMs : next;
        }

        /// <summary>
        /// 立即恢复到最低轮询间隔。
        /// </summary>
        public void Reset()
        {
            CurrentIntervalMs = mMinIntervalMs;
        }
    }
}
