#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 表示已完成解析、校验和 dispatcher 执行的 FileBridge 命令结果。
    /// </summary>
    internal sealed class YokiFrameHostCommandExecution
    {
        /// <summary>
        /// 创建命令执行结果。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="responseJson">待写入 results 的 terminal response JSON。</param>
        public YokiFrameHostCommandExecution(string requestId, string responseJson)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Command request ID is required.", nameof(requestId));
            }

            if (responseJson == null)
            {
                throw new ArgumentNullException(nameof(responseJson));
            }

            RequestId = requestId;
            ResponseJson = responseJson;
        }

        /// <summary>
        /// 获取请求标识。
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// 获取已由宿主 serializer 生成的 terminal response JSON。
        /// </summary>
        public string ResponseJson { get; }
    }

    /// <summary>
    /// 为 HostCommandCoordinator 提供 FileBridge 文件生命周期操作。
    /// </summary>
    internal interface IYokiFrameHostCommandStore
    {
        /// <summary>
        /// 复核并准备当前 Host 的 FileBridge 根目录。
        /// </summary>
        void EnsureReady();

        /// <summary>
        /// 获取 commands 根目录是否存在。
        /// </summary>
        bool PendingRootExists { get; }

        /// <summary>
        /// 读取当前 Host 的 pending 命令路径，并保留宿主要求的排序语义。
        /// </summary>
        IReadOnlyList<string> ReadPendingCommandPaths();

        /// <summary>
        /// 读取 processing 根目录中的命令路径。
        /// </summary>
        IReadOnlyList<string> ReadProcessingCommandPaths();

        /// <summary>
        /// 原子 claim 一个 pending 命令。
        /// </summary>
        /// <param name="pendingPath">pending 命令路径。</param>
        /// <param name="claimedPath">claim 后的 processing 路径。</param>
        /// <param name="storageException">StorageError 时的底层存储异常；其它结果为空。</param>
        /// <returns>当前调用的详细 claim 结果。</returns>
        YokiFrameFileBridgeClaimResult TryClaim(
            string pendingPath,
            out string claimedPath,
            out Exception storageException);

        /// <summary>
        /// 删除过期 processing marker。
        /// </summary>
        /// <param name="cutoffUtc">过期时间边界。</param>
        void RemoveExpiredMarkers(DateTime cutoffUtc);

        /// <summary>
        /// 获取文件最后写入时间。
        /// </summary>
        /// <param name="path">文件路径。</param>
        /// <returns>文件最后写入 UTC 时间。</returns>
        DateTime GetLastWriteTimeUtc(string path);

        /// <summary>
        /// 刷新已 claim 命令的 processing lease 起点。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <param name="claimedAtUtc">本次 claim 时间。</param>
        void RefreshProcessingLease(string commandPath, DateTime claimedAtUtc);

        /// <summary>
        /// 判断 processing 命令是否已经成功写入 terminal response。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <returns>已有 terminal response 时返回 true。</returns>
        bool HasTerminalResponse(string commandPath);

        /// <summary>
        /// 原子写入 terminal response。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="responseJson">响应 JSON。</param>
        void WriteResponse(string requestId, string responseJson);

        /// <summary>
        /// 将已产生 terminal response 的命令归档。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        void Archive(string commandPath);

        /// <summary>
        /// 将无法完成的命令写入 deadletter 并移动原请求。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        void MoveToDeadletter(string commandPath, string errorCode, string errorMessage);

        /// <summary>
        /// deadletter 写入失败时，在 processing 命令旁持久化失败证据。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        void WriteProcessingFailureEvidence(string commandPath, string errorCode, string errorMessage);

        /// <summary>
        /// 批次完成后的可选存储清理。
        /// </summary>
        void PruneAfterBatch();

        /// <summary>
        /// commands 根目录缺失时的可选存储清理。
        /// </summary>
        void PruneWhenPendingRootMissing();
    }

    /// <summary>
    /// 编排 FileBridge 命令从 claim 到 terminal evidence 的公共生命周期。
    /// </summary>
    internal sealed class YokiFrameHostCommandCoordinator
    {
        private const string PROCESSING_EXPIRED_ERROR = "ProcessingExpired";
        private const string COMMAND_PROCESSING_FAILED_ERROR = "CommandProcessingFailed";
        private const string COMMAND_EXECUTION_UNKNOWN_ERROR = "CommandExecutionUnknown";
        private const string PROCESSING_EXPIRED_MESSAGE =
            "The processing lease expired; the mutation was not replayed automatically.";

        private readonly IYokiFrameHostCommandStore mStore;
        private readonly Func<string, YokiFrameHostCommandExecution> mExecutor;
        private readonly Action<Exception> mOnProcessingError;
        private readonly TimeSpan mProcessingLease;
        private readonly Func<DateTime> mUtcNow;
        private readonly int mMaxCommandsPerBatch;
        private readonly TimeSpan mMaxBatchDuration;
        private bool mIsProcessing;

        /// <summary>获取本轮是否因批次预算而提前让出。</summary>
        public bool LastBatchWasLimited { get; private set; }

        /// <summary>获取本轮让出原因；无让出时为空。</summary>
        public string LastBatchLimitReason { get; private set; } = string.Empty;

        /// <summary>获取本轮已完成 claim 的命令数量。</summary>
        public int LastBatchProcessedCount { get; private set; }

        /// <summary>
        /// 创建 Host 命令生命周期协调器。
        /// </summary>
        /// <param name="store">FileBridge 存储适配器。</param>
        /// <param name="executor">宿主解析、校验、dispatch 和 response 序列化回调。</param>
        /// <param name="processingLease">processing 文件允许保留的最长时间。</param>
        /// <param name="onProcessingError">宿主记录最后错误的可选回调。</param>
        /// <param name="utcNow">用于测试和单调判断的 UTC 时钟。</param>
        public YokiFrameHostCommandCoordinator(
            IYokiFrameHostCommandStore store,
            Func<string, YokiFrameHostCommandExecution> executor,
            TimeSpan processingLease,
            Action<Exception> onProcessingError = null,
            Func<DateTime> utcNow = null,
            int maxCommandsPerBatch = 32,
            TimeSpan? maxBatchDuration = null)
        {
            mStore = store ?? throw new ArgumentNullException(nameof(store));
            mExecutor = executor ?? throw new ArgumentNullException(nameof(executor));
            if (processingLease <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(processingLease));
            }

            mProcessingLease = processingLease;
            mOnProcessingError = onProcessingError;
            mUtcNow = utcNow ?? (() => DateTime.UtcNow);
            if (maxCommandsPerBatch <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCommandsPerBatch));
            }

            mMaxCommandsPerBatch = maxCommandsPerBatch;
            mMaxBatchDuration = maxBatchDuration ?? TimeSpan.FromMilliseconds(25);
            if (mMaxBatchDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchDuration));
            }
        }

        /// <summary>
        /// 消费当前批次的 pending 命令，并返回成功 claim 的数量。
        /// </summary>
        /// <returns>本轮成功 claim 的命令数量。</returns>
        public int ProcessPendingCommands()
        {
            if (mIsProcessing)
            {
                return 0;
            }

            mStore.EnsureReady();
            if (!mStore.PendingRootExists)
            {
                mStore.PruneWhenPendingRootMissing();
                return 0;
            }

            RecoverExpiredProcessingCommands();
            mIsProcessing = true;
            LastBatchWasLimited = false;
            LastBatchLimitReason = string.Empty;
            LastBatchProcessedCount = 0;
            long batchStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                IReadOnlyList<string> commandPaths = mStore.ReadPendingCommandPaths();
                var claimedCount = 0;
                for (var index = 0; index < commandPaths.Count; index++)
                {
                    if (HasReachedBatchLimit(batchStartTimestamp, claimedCount))
                    {
                        break;
                    }

                    var claimResult = mStore.TryClaim(
                        commandPaths[index],
                        out var claimedPath,
                        out var storageException);
                    if (claimResult == YokiFrameFileBridgeClaimResult.StorageError)
                    {
                        mOnProcessingError?.Invoke(
                            storageException
                            ?? new IOException("FileBridge claim storage failed."));
                        continue;
                    }

                    if (claimResult != YokiFrameFileBridgeClaimResult.Claimed)
                    {
                        continue;
                    }

                    claimedCount++;
                    LastBatchProcessedCount = claimedCount;
                    try
                    {
                        mStore.RefreshProcessingLease(claimedPath, mUtcNow());
                    }
                    catch (Exception exception)
                    {
                        mOnProcessingError?.Invoke(exception);
                        MoveToDeadletterSafely(
                            claimedPath,
                            COMMAND_PROCESSING_FAILED_ERROR,
                            exception.Message);
                        continue;
                    }

                    ProcessCommandFile(claimedPath);
                }

                return claimedCount;
            }
            finally
            {
                mIsProcessing = false;
                mStore.PruneAfterBatch();
            }
        }

        /// <summary>
        /// 将超过 lease 的 processing 请求转为 deadletter，禁止自动重放 mutation。
        /// </summary>
        private void RecoverExpiredProcessingCommands()
        {
            IReadOnlyList<string> processingPaths = mStore.ReadProcessingCommandPaths();
            if (processingPaths.Count == 0)
            {
                return;
            }

            DateTime cutoffUtc = mUtcNow() - mProcessingLease;
            mStore.RemoveExpiredMarkers(cutoffUtc);
            for (var index = 0; index < processingPaths.Count; index++)
            {
                string processingPath = processingPaths[index];
                if (mStore.HasTerminalResponse(processingPath))
                {
                    TryArchiveTerminalResponse(processingPath);
                    continue;
                }

                if (mStore.GetLastWriteTimeUtc(processingPath) >= cutoffUtc)
                {
                    continue;
                }

                MoveToDeadletterSafely(
                    processingPath,
                    PROCESSING_EXPIRED_ERROR,
                    PROCESSING_EXPIRED_MESSAGE);
            }
        }

        /// <summary>
        /// 处理单个已 claim 命令，保证成功路径先写 response 再 archive。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        private void ProcessCommandFile(string commandPath)
        {
            YokiFrameHostCommandExecution execution;
            try
            {
                execution = mExecutor(commandPath);
                if (execution == null)
                {
                    throw new InvalidOperationException("Host command executor returned no execution result.");
                }
            }
            catch (Exception exception)
            {
                mOnProcessingError?.Invoke(exception);
                MoveToDeadletterSafely(
                    commandPath,
                    COMMAND_PROCESSING_FAILED_ERROR,
                    exception.Message);
                return;
            }

            try
            {
                mStore.WriteResponse(execution.RequestId, execution.ResponseJson);
            }
            catch (Exception exception)
            {
                mOnProcessingError?.Invoke(exception);
                MoveToDeadletterSafely(
                    commandPath,
                    COMMAND_EXECUTION_UNKNOWN_ERROR,
                    exception.Message);
                return;
            }

            try
            {
                mStore.Archive(commandPath);
            }
            catch (Exception exception)
            {
                // response 已提交后，archive 只是证据保留维护；不能再生成失败 deadletter 覆盖业务终态。
                mOnProcessingError?.Invoke(exception);
            }
        }

        /// <summary>
        /// 在 response 已存在但归档失败时重试旁路归档，不改变已提交业务终态。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        private void TryArchiveTerminalResponse(string commandPath)
        {
            try
            {
                mStore.Archive(commandPath);
            }
            catch (Exception exception)
            {
                mOnProcessingError?.Invoke(exception);
            }
        }

        /// <summary>
        /// 判断本轮是否达到命令数量或墙钟时间预算。
        /// </summary>
        /// <param name="batchStartTimestamp">本轮开始的 Stopwatch 时间戳。</param>
        /// <param name="claimedCount">已处理命令数。</param>
        /// <returns>达到任一预算时返回 true。</returns>
        private bool HasReachedBatchLimit(long batchStartTimestamp, int claimedCount)
        {
            if (claimedCount >= mMaxCommandsPerBatch)
            {
                LastBatchWasLimited = true;
                LastBatchLimitReason = "maxCommandsPerBatch";
                return true;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - batchStartTimestamp;
            var elapsed = TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
            if (elapsed < mMaxBatchDuration)
            {
                return false;
            }

            LastBatchWasLimited = true;
            LastBatchLimitReason = "maxBatchDuration";
            return true;
        }

        /// <summary>
        /// 尽力写入 deadletter；失败时保留 processing 旁路证据，避免已 claim 命令没有持久终态线索。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        private void MoveToDeadletterSafely(string commandPath, string errorCode, string errorMessage)
        {
            try
            {
                mStore.MoveToDeadletter(commandPath, errorCode, errorMessage);
            }
            catch (Exception deadletterException)
            {
                mOnProcessingError?.Invoke(deadletterException);
                try
                {
                    // 复用 claim marker 的 processing 目录位置；marker 不会被命令枚举器当作 JSON 命令，
                    // 下一轮 lease 回收仍可重试标准 deadletter 流程。
                    mStore.WriteProcessingFailureEvidence(
                        commandPath,
                        errorCode,
                        errorMessage + " Deadletter evidence write failed: " + deadletterException.Message);
                }
                catch (Exception evidenceException)
                {
                    // 两级证据都失败时只能暴露诊断；processing 原文件仍保留，等待下一轮 lease 回收。
                    mOnProcessingError?.Invoke(evidenceException);
                }
            }
        }
    }
}

#endif
