using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 文件命令桥核心的命令处理与轮询片段。
    /// </summary>
    public sealed partial class YokiCommandBridgeCore
    {
        private void ProcessClaimedCommand(string processingPath)
        {
            var commandName = ResolveSafeFallbackRequestId(Path.GetFileNameWithoutExtension(processingPath));
            var responseRequestId = commandName;
            var resultPath = BuildResultPath(responseRequestId);
            var commandJson = string.Empty;
            var shouldDeadletter = false;
            var deadletterReason = string.Empty;

            try
            {
                mActiveProcessingPath = Path.GetFullPath(processingPath);
                commandJson = FileBridgeFileSystem.ReadAllTextInRoot(mRootDir, processingPath);
                string invalidRequestId;
                responseRequestId = ResolveResponseRequestId(commandName, commandJson, out invalidRequestId);
                resultPath = BuildResultPath(responseRequestId);
                if (HasTerminalResponse(resultPath))
                {
                    mDuplicateCommandCount++;
                    RecordLastError("Duplicate requestId reused existing terminal response: " + responseRequestId);
                    return;
                }

                var resultJson = BuildPreDispatchErrorResponse(responseRequestId, commandJson, invalidRequestId)
                                 ?? DispatchWithCurrentEngine(commandJson);
                resultJson = EnforceResultSizeLimit(responseRequestId, commandJson, resultJson);
                if (!TryWriteTextOrRecord(resultPath, resultJson, "result"))
                {
                    shouldDeadletter = true;
                    deadletterReason = string.IsNullOrEmpty(mLastErrorMessage)
                        ? "Failed to write result for requestId " + responseRequestId
                        : mLastErrorMessage;
                    return;
                }

                if (resultJson.IndexOf("\"status\":\"error\"", StringComparison.Ordinal) >= 0)
                    RecordLastError("Command returned error response: " + responseRequestId);
                else
                    mProcessedCommandCount++;
            }
            catch (Exception ex)
            {
                string invalidRequestId;
                responseRequestId = ResolveResponseRequestId(commandName, commandJson, out invalidRequestId);
                resultPath = BuildResultPath(responseRequestId);
                var resultJson = string.IsNullOrEmpty(invalidRequestId)
                    ? BuildExceptionResponse(responseRequestId, commandJson, ex)
                    : BuildInvalidRequestIdResponse(responseRequestId, commandJson, invalidRequestId);
                if (!TryWriteTextOrRecord(resultPath, resultJson, "exception result"))
                {
                    shouldDeadletter = true;
                    deadletterReason = string.IsNullOrEmpty(mLastErrorMessage)
                        ? "Failed to write exception result for requestId " + responseRequestId
                        : mLastErrorMessage;
                }
                WriteError(processingPath, ex);
                RecordLastError(ex.Message);
            }
            finally
            {
                if (shouldDeadletter)
                    DeadletterProcessedCommand(processingPath, deadletterReason);
                else
                    ArchiveProcessedCommand(processingPath);
                mActiveProcessingPath = null;
            }
        }

        private void ApplyPendingQueueLimit(string[] files)
        {
            if (mOptions.MaxPendingCommands <= 0 || files == null ||
                files.Length <= mOptions.MaxPendingCommands)
                return;

            mBackpressureActive = true;
            mLastPollLimitReason = "MaxPendingCommands";
            for (var i = mOptions.MaxPendingCommands; i < files.Length; i++)
            {
                if (!File.Exists(files[i]))
                    continue;

                var processingPath = Path.Combine(mProcessingDir, Path.GetFileName(files[i]));
                if (!FileBridgeFileSystem.TryClaimFileInRoot(mRootDir, files[i], processingPath))
                    continue;

                ProcessBridgeBusyCommand(processingPath);
            }
        }

        private void ProcessBridgeBusyCommand(string processingPath)
        {
            var commandName = ResolveSafeFallbackRequestId(Path.GetFileNameWithoutExtension(processingPath));
            var commandJson = TryReadAllText(processingPath);
            string invalidRequestId;
            var requestId = ResolveResponseRequestId(commandName, commandJson, out invalidRequestId);
            var resultPath = BuildResultPath(requestId);
            var resultJson = string.IsNullOrEmpty(invalidRequestId)
                ? BuildBridgeBusyResponse(requestId, commandJson)
                : BuildInvalidRequestIdResponse(requestId, commandJson, invalidRequestId);

            if (!HasTerminalResponse(resultPath))
                TryWriteTextOrRecord(resultPath, resultJson, "bridge busy result");
            else
                mDuplicateCommandCount++;

            mBridgeBusyCount++;
            ArchiveProcessedCommand(processingPath);
        }

        private void RecoverStaleProcessingCommands()
        {
            if (!Directory.Exists(mProcessingDir))
                return;

            var files = FileBridgeFileSystem.GetFilesInRoot(mRootDir, mProcessingDir, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (!IsProcessingFileStale(file))
                    continue;

                DeadletterStaleProcessingCommand(file);
            }
        }

        private void DeadletterStaleProcessingCommand(string processingPath)
        {
            var commandName = ResolveSafeFallbackRequestId(Path.GetFileNameWithoutExtension(processingPath));
            var commandJson = TryReadAllText(processingPath);
            string invalidRequestId;
            var requestId = ResolveResponseRequestId(commandName, commandJson, out invalidRequestId);
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var message = "Command '" + commandName + "' exceeded stale processing timeout (" +
                          mOptions.ProcessingTimeout.TotalSeconds.ToString("0.###") + "s)";
            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;
            var resultJson = string.IsNullOrEmpty(invalidRequestId)
                ? JsonHelper.BuildError(requestId, kit, action, message, engineId, "StaleProcessingTimeout", false)
                : BuildInvalidRequestIdResponse(requestId, commandJson, invalidRequestId);
            var resultPath = BuildResultPath(requestId);
            if (HasTerminalResponse(resultPath))
            {
                mDuplicateCommandCount++;
                RecordLastError("Duplicate stale requestId reused existing terminal response: " + requestId);
            }
            else
            {
                TryWriteTextOrRecord(resultPath, resultJson, "stale result");
            }

            TryWriteTextOrRecord(
                Path.Combine(mErrorDir, commandName + "-error.txt"),
                "[" + DateTime.UtcNow.ToString("O") + "] " + message,
                "stale error log");

            var deadletterPath = GetUniquePath(mDeadletterDir, Path.GetFileName(processingPath));
            try
            {
                FileBridgeFileSystem.ReplaceFileInRoot(mRootDir, processingPath, deadletterPath);
                mDeadletterCommandCount++;
                RecordLastError("Deadlettered stale processing command: " + commandName);
            }
            catch (Exception ex)
            {
                RecordLastError("Failed to deadletter stale command '" + commandName + "': " + ex.Message);
            }
        }

        private void ArchiveProcessedCommand(string processingPath)
        {
            var archivePath = GetUniquePath(mArchiveDir, Path.GetFileName(processingPath));
            try
            {
                FileBridgeFileSystem.ReplaceFileInRoot(mRootDir, processingPath, archivePath);
            }
            catch
            {
                FileBridgeFileSystem.TryDeleteInRoot(mRootDir, processingPath);
            }
        }

        private void DeadletterProcessedCommand(string processingPath, string reason)
        {
            var deadletterPath = GetUniquePath(mDeadletterDir, Path.GetFileName(processingPath));
            try
            {
                FileBridgeFileSystem.ReplaceFileInRoot(mRootDir, processingPath, deadletterPath);
                mDeadletterCommandCount++;
                RecordLastError("Deadlettered command: " + reason);
            }
            catch (Exception ex)
            {
                RecordLastError("Failed to deadletter command '" +
                                Path.GetFileNameWithoutExtension(processingPath) + "': " + ex.Message);
            }
        }

        private bool IsProcessingFileStale(string path)
        {
            try
            {
                var timeout = mOptions.ProcessingTimeout;
                if (timeout < TimeSpan.Zero)
                    return false;

                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, path);
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
                return age >= timeout;
            }
            catch
            {
                return false;
            }
        }

        private int CountStaleProcessingCommands()
        {
            return CountFiles(mProcessingDir, "*.json",
                path => ShouldCountAsVisibleProcessing(path) && IsProcessingFileStale(path));
        }

        private bool ShouldCountAsVisibleProcessing(string path)
        {
            if (string.IsNullOrEmpty(mActiveProcessingPath))
                return true;

            return !string.Equals(Path.GetFullPath(path), mActiveProcessingPath, StringComparison.Ordinal);
        }

        private bool IsCommandReadyForClaim(string path)
        {
            if (mOptions.IncompleteCommandGracePeriod <= TimeSpan.Zero)
                return true;

            string commandJson;
            try
            {
                commandJson = FileBridgeFileSystem.ReadAllTextInRoot(mRootDir, path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception ex)
            {
                RecordLastError("Failed to inspect pending command '" +
                                Path.GetFileNameWithoutExtension(path) + "': " + ex.Message);
                return false;
            }

            if (!IsLikelyIncompleteJsonDocument(commandJson))
                return true;

            try
            {
                FileBridgeFileSystem.EnsurePathWithinRoot(mRootDir, path);
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
                return age >= mOptions.IncompleteCommandGracePeriod;
            }
            catch (Exception ex)
            {
                RecordLastError("Failed to check pending command age '" +
                                Path.GetFileNameWithoutExtension(path) + "': " + ex.Message);
                return false;
            }
        }

        private bool IsPollTimeBudgetExceeded(DateTime pollStartUtc)
        {
            if (mOptions.PollTimeBudgetMs <= 0)
                return false;

            return (DateTime.UtcNow - pollStartUtc).TotalMilliseconds >= mOptions.PollTimeBudgetMs;
        }
    }
}
