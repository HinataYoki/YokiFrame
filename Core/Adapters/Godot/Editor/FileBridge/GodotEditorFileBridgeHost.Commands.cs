#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot Editor Host 的命令读取、策略执行、terminal response、archive 和 deadletter。
    /// </summary>
    public sealed partial class GodotEditorFileBridgeHost
    {
        private static readonly TimeSpan PROCESSING_LEASE = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 消费 commands 顶层全部 JSON，确保每个文件进入 response/archive 或 deadletter 终态。
        /// </summary>
        /// <returns>本轮尝试处理的命令文件数量。</returns>
        public int ProcessPendingCommands()
        {
            EnsureRunning();
            return mCommandCoordinator.ProcessPendingCommands();
        }

        /// <summary>
        /// 解析、执行并序列化 Godot Editor 命令，供公共协调器写入 terminal response。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <returns>已序列化的命令执行结果。</returns>
        private YokiFrameHostCommandExecution ExecuteCommandForCoordinator(string commandPath)
        {
            var envelope = ReadCommandEnvelope(commandPath);
            var response = ExecuteCommand(envelope, new FileInfo(commandPath).Length);
            return new YokiFrameHostCommandExecution(
                envelope.RequestId,
                GodotEditorFileBridgeJson.Serialize(response));
        }

        /// <summary>
        /// Godot Editor 的 FileBridge 存储适配器，保留排序、路径和清理语义。
        /// </summary>
        private sealed class GodotEditorHostCommandStore : IYokiFrameHostCommandStore
        {
            private readonly GodotEditorFileBridgeHost mHost;

            /// <summary>
            /// 创建绑定 Editor Host 的存储适配器。
            /// </summary>
            /// <param name="host">Godot Editor Host。</param>
            public GodotEditorHostCommandStore(GodotEditorFileBridgeHost host)
            {
                mHost = host;
            }

            /// <summary>
            /// Editor Host 已在 Start 中准备目录；此处保持统一入口幂等。
            /// </summary>
            public void EnsureReady()
            {
                mHost.mPaths.EnsureReady();
            }

            /// <summary>
            /// 获取 commands 根目录是否存在。
            /// </summary>
            public bool PendingRootExists => Directory.Exists(mHost.mPaths.CommandsRoot);

            /// <summary>
            /// 读取并稳定排序 Editor pending 命令。
            /// </summary>
            public IReadOnlyList<string> ReadPendingCommandPaths()
            {
                var commandPaths = Directory.GetFiles(
                    mHost.mPaths.CommandsRoot,
                    "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                    SearchOption.TopDirectoryOnly);
                Array.Sort(commandPaths, StringComparer.OrdinalIgnoreCase);
                return commandPaths;
            }

            /// <summary>
            /// 读取 Editor processing 命令。
            /// </summary>
            public IReadOnlyList<string> ReadProcessingCommandPaths()
            {
                return Directory.Exists(mHost.mPaths.ProcessingRoot)
                    ? Directory.GetFiles(mHost.mPaths.ProcessingRoot, "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION, SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();
            }

            /// <summary>
            /// 原子 claim Editor pending 命令。
            /// </summary>
            public YokiFrameFileBridgeClaimResult TryClaim(
                string pendingPath,
                out string claimedPath,
                out Exception storageException)
            {
                return YokiFrameFileBridgeClaim.TryClaim(
                    pendingPath,
                    mHost.mPaths.ProcessingRoot,
                    out claimedPath,
                    out storageException);
            }

            /// <summary>
            /// 删除 Editor processing marker。
            /// </summary>
            public void RemoveExpiredMarkers(DateTime cutoffUtc)
            {
                YokiFrameFileBridgeClaim.RemoveExpiredMarkers(mHost.mPaths.ProcessingRoot, cutoffUtc);
            }

            /// <summary>
            /// 获取 Editor processing 文件最后写入时间。
            /// </summary>
            public DateTime GetLastWriteTimeUtc(string path)
            {
                return File.GetLastWriteTimeUtc(path);
            }

            /// <summary>
            /// 成功 claim 后刷新 processing 文件时间，避免老 pending 的原始 mtime 立即触发过期回收。
            /// </summary>
            /// <param name="commandPath">processing 命令路径。</param>
            /// <param name="claimedAtUtc">本次 claim 时间。</param>
            public void RefreshProcessingLease(string commandPath, DateTime claimedAtUtc)
            {
                File.SetLastWriteTimeUtc(commandPath, claimedAtUtc);
            }

            /// <summary>
            /// 判断 Editor processing 命令是否已经存在对应 terminal response。
            /// </summary>
            /// <param name="commandPath">processing 命令路径。</param>
            /// <returns>response 已存在时返回 true。</returns>
            public bool HasTerminalResponse(string commandPath)
            {
                try
                {
                    return File.Exists(mHost.mPaths.GetResponsePath(
                        Path.GetFileNameWithoutExtension(commandPath)));
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            /// <summary>
            /// 写入 Editor terminal response。
            /// </summary>
            public void WriteResponse(string requestId, string responseJson)
            {
                GodotEditorFileBridgeJson.WriteAtomic(mHost.mPaths.GetResponsePath(requestId), responseJson);
            }

            /// <summary>
            /// 归档 Editor 已完成命令。
            /// </summary>
            public void Archive(string commandPath)
            {
                mHost.ArchiveCommand(commandPath);
            }

            /// <summary>
            /// 将 Editor 失败命令写入 deadletter。
            /// </summary>
            public void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
            {
                mHost.MoveToDeadletter(commandPath, errorCode, errorMessage);
            }

            /// <summary>
            /// deadletter 目录不可写时，在 processing 命令旁原子保留失败证据；该 marker 不会进入命令枚举。
            /// </summary>
            /// <param name="commandPath">processing 命令路径。</param>
            /// <param name="errorCode">错误码。</param>
            /// <param name="errorMessage">错误说明。</param>
            public void WriteProcessingFailureEvidence(
                string commandPath,
                string errorCode,
                string errorMessage)
            {
                mHost.WriteProcessingFailureEvidence(commandPath, errorCode, errorMessage);
            }

            /// <summary>
            /// 保留 Editor 原有批次结束清理策略。
            /// </summary>
            public void PruneAfterBatch()
            {
                mHost.TryPruneStorage();
            }

            /// <summary>
            /// commands 根目录缺失时保留 Editor 原有清理策略。
            /// </summary>
            public void PruneWhenPendingRootMissing()
            {
                mHost.TryPruneStorage();
            }
        }

        /// <summary>
        /// 按五分钟节流回收终态 FileBridge 证据；清理失败不影响 Editor Host 继续服务。
        /// </summary>
        private void TryPruneStorage()
        {
            var nowUtc = DateTime.UtcNow;
            if (nowUtc < mNextStorageCleanupUtc)
            {
                return;
            }

            try
            {
                YokiFrameFileBridgePruner.Prune(mPaths.ProjectRoot);
            }
            catch (Exception exception)
            {
                // 清理失败不阻断 Host；记录到 bridge_status，供工具侧区分维护失败与正常空闲。
                mLastError = "Godot Editor FileBridge storage cleanup failed: " + exception.Message;
            }

            mNextStorageCleanupUtc = nowUtc.AddMinutes(5.0d);
        }

        /// <summary>
        /// 创建只允许三个 Editor System 只读命令的共享 dispatcher。
        /// </summary>
        /// <returns>Editor 命令 dispatcher。</returns>
        private YokiFrameCommandDispatcher CreateCommandDispatcher()
        {
            YokiFrameCommandDescriptor[] commands =
            {
                new YokiFrameCommandDescriptor("System", "ping", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor("System", "bridge_status", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor("System", "list_commands", YokiFrameCommandKind.ReadOnly)
            };
            YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(commands);
            return new YokiFrameCommandDispatcher(
                policy,
                new IYokiFrameCommandHandler[]
                {
                    new GodotEditorSystemCommandHandler(
                        CreatePingResultJson,
                        CreateBridgeStatusResultJson,
                        () => CreateCommandCatalogJson(policy.AllowedCommands))
                });
        }

        /// <summary>
        /// 根据当前 Editor policy 创建稳定排序的实时命令目录。
        /// </summary>
        /// <param name="commands">当前 policy 允许的命令。</param>
        /// <returns>命令目录 JSON。</returns>
        private string CreateCommandCatalogJson(IReadOnlyList<YokiFrameCommandDescriptor> commands)
        {
            List<GodotEditorCommandCatalogAction> actions = new List<GodotEditorCommandCatalogAction>();
            for (var index = 0; index < commands.Count; index++)
            {
                actions.Add(new GodotEditorCommandCatalogAction
                {
                    Action = commands[index].Action,
                    Kind = commands[index].Kind.ToString()
                });
            }

            actions.Sort(static (left, right) => string.CompareOrdinal(left.Action, right.Action));
            return GodotEditorFileBridgeJson.Serialize(new GodotEditorCommandCatalogResult
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence,
                Kits = new[]
                {
                    new GodotEditorCommandCatalogKit { Kit = "System", Actions = actions.ToArray() }
                }
            });
        }

        /// <summary>
        /// 读取命令文件并执行文件大小、JSON 和信封校验。
        /// </summary>
        /// <param name="commandPath">命令完整路径。</param>
        /// <returns>已校验命令信封。</returns>
        private static GodotEditorCommandEnvelope ReadCommandEnvelope(string commandPath)
        {
            FileInfo fileInfo = new FileInfo(commandPath);
            if (fileInfo.Length > YokiFrameFileBridgeContract.COMMAND_FILE_MAX_BYTES)
            {
                throw new InvalidDataException("Command file exceeds the Editor FileBridge byte limit.");
            }

            var envelope = GodotEditorFileBridgeJson.Deserialize<GodotEditorCommandEnvelope>(
                File.ReadAllText(commandPath));
            ValidateEnvelope(envelope);
            if (!string.Equals(
                    Path.GetFileNameWithoutExtension(commandPath),
                    envelope.RequestId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Command file name does not match envelope requestId.");
            }

            return envelope;
        }

        /// <summary>
        /// 校验协议、engine、SafeId 与 payload JSON，不接受 Runtime engine 命令。
        /// </summary>
        /// <param name="envelope">待校验信封。</param>
        private static void ValidateEnvelope(GodotEditorCommandEnvelope envelope)
        {
            if (envelope.ProtocolVersion != YokiFrameFileBridgeContract.PROTOCOL_VERSION
                || envelope.EngineId != ENGINE_ID)
            {
                throw new InvalidDataException("Command envelope protocolVersion or engineId is invalid.");
            }

            if (!YokiFrameSafeIdContract.IsSafeId(envelope.Source)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.RequestId)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.Kit)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.Action))
            {
                throw new InvalidDataException("Command envelope contains an unsafe identifier.");
            }

            if (envelope.TimeoutMs < YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MIN_MS
                || envelope.TimeoutMs > YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MAX_MS
                || !DateTimeOffset.TryParse(
                    envelope.CreatedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                throw new InvalidDataException("Command envelope timeoutMs or createdAtUtc is invalid.");
            }

            GodotEditorFileBridgeJson.ValidatePayloadJson(envelope.PayloadJson);
        }

        /// <summary>
        /// 把已校验信封交给共享 dispatcher 并转换为 terminal response。
        /// </summary>
        /// <param name="envelope">已校验命令。</param>
        /// <param name="commandFileBytes">命令文件字节数。</param>
        /// <returns>terminal response。</returns>
        private GodotEditorCommandResponse ExecuteCommand(
            GodotEditorCommandEnvelope envelope,
            long commandFileBytes)
        {
            YokiFrameCommandRequest request = new YokiFrameCommandRequest(
                envelope.Source,
                envelope.Kit,
                envelope.Action,
                envelope.PayloadJson,
                envelope.TimeoutMs,
                commandFileBytes,
                envelope.RequestId,
                ParseCreatedAtUtc(envelope.CreatedAtUtc));
            var result = mDispatcher.Dispatch(request);
            return result.IsSuccess
                ? CreateSuccessResponse(envelope.RequestId, result.ResultJson)
                : CreateErrorResponse(envelope.RequestId, result.ErrorCode, result.ErrorMessage);
        }

        /// <summary>
        /// 把已通过信封校验的创建时间转换为 UTC，供 dispatcher 计算执行 deadline。
        /// </summary>
        /// <param name="createdAtUtc">信封创建时间文本。</param>
        /// <returns>UTC 创建时间。</returns>
        private static DateTimeOffset ParseCreatedAtUtc(string createdAtUtc)
        {
            if (!DateTimeOffset.TryParse(
                    createdAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var value))
            {
                throw new InvalidDataException("Command envelope createdAtUtc is invalid.");
            }

            return value.ToUniversalTime();
        }

        /// <summary>
        /// 创建 System/ping 的当前 Editor 身份 JSON。
        /// </summary>
        /// <returns>ping 结果 JSON。</returns>
        private string CreatePingResultJson()
        {
            return GodotEditorFileBridgeJson.Serialize(new GodotEditorPingResult
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence
            });
        }

        /// <summary>
        /// 创建 System/bridge_status 的队列、存储与 FileBridge-only 诊断。
        /// </summary>
        /// <returns>bridge_status 结果 JSON。</returns>
        private string CreateBridgeStatusResultJson()
        {
            var storage = GodotEditorFileBridgeJson.ReadStorageDiagnostics(mPaths.EngineRoot);
            return GodotEditorFileBridgeJson.Serialize(new GodotEditorBridgeStatusResult
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence,
                Pending = GodotEditorFileBridgeJson.CountJsonFiles(mPaths.CommandsRoot),
                Archive = GodotEditorFileBridgeJson.CountJsonFiles(mPaths.ArchiveRoot),
                Deadletter = GodotEditorFileBridgeJson.CountJsonFiles(mPaths.DeadletterRoot),
                Results = GodotEditorFileBridgeJson.CountJsonFiles(mPaths.ResultsRoot),
                ProtocolFileCount = storage.FileCount,
                ProtocolBytes = storage.TotalBytes,
                OldestProtocolFileUtc = storage.OldestFileUtc,
                BackpressureActive = mCommandCoordinator.LastBatchWasLimited,
                LastPollLimitReason = mCommandCoordinator.LastBatchLimitReason,
                LastError = mLastError
            });
        }

        /// <summary>
        /// 创建成功 terminal response。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="resultJson">业务结果 JSON。</param>
        /// <returns>成功响应。</returns>
        private static GodotEditorCommandResponse CreateSuccessResponse(
            string requestId,
            string resultJson)
        {
            return new GodotEditorCommandResponse
            {
                RequestId = requestId,
                Status = "Success",
                ResultJson = resultJson,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 创建错误 terminal response，避免策略拒绝表现为调用侧超时。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        /// <returns>错误响应。</returns>
        private static GodotEditorCommandResponse CreateErrorResponse(
            string requestId,
            string errorCode,
            string errorMessage)
        {
            return new GodotEditorCommandResponse
            {
                RequestId = requestId,
                Status = "Error",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 将成功处理的命令移动到 archive，冲突时追加 UTC 毫秒后缀。
        /// </summary>
        /// <param name="commandPath">原命令路径。</param>
        private void ArchiveCommand(string commandPath)
        {
            var archivePath = mPaths.GetArchivePath(commandPath);
            if (File.Exists(archivePath))
            {
                archivePath += "." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            File.Move(commandPath, archivePath);
        }

        /// <summary>
        /// 写入 deadletter 诊断并移动损坏或无法消费的原请求。
        /// </summary>
        /// <param name="commandPath">原命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        private void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
        {
            var deadletterId = CreateDeadletterId(commandPath);
            GodotEditorDeadletterInfo info = new GodotEditorDeadletterInfo
            {
                SourcePath = commandPath,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            GodotEditorFileBridgeJson.WriteAtomic(
                mPaths.GetDeadletterInfoPath(deadletterId),
                GodotEditorFileBridgeJson.Serialize(info));
            MoveDeadletterRequest(commandPath, deadletterId);
        }

        /// <summary>
        /// deadletter 写入失败时，在 processing 命令旁原子保留失败证据。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        private void WriteProcessingFailureEvidence(
            string commandPath,
            string errorCode,
            string errorMessage)
        {
            GodotEditorDeadletterInfo evidence = new GodotEditorDeadletterInfo
            {
                SourcePath = commandPath,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            GodotEditorFileBridgeJson.WriteAtomic(
                commandPath + ".claim",
                GodotEditorFileBridgeJson.Serialize(evidence));
        }

        /// <summary>
        /// 根据原文件名创建安全 deadletter 标识。
        /// </summary>
        /// <param name="commandPath">原命令路径。</param>
        /// <returns>安全 deadletter ID。</returns>
        private static string CreateDeadletterId(string commandPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(commandPath);
            return YokiFrameSafeIdContract.IsSafeId(fileName)
                ? fileName
                : "invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    + "-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 移动 deadletter 原请求，目标冲突时追加 UTC 毫秒后缀。
        /// </summary>
        /// <param name="commandPath">原命令路径。</param>
        /// <param name="deadletterId">安全 deadletter ID。</param>
        private void MoveDeadletterRequest(string commandPath, string deadletterId)
        {
            if (!File.Exists(commandPath))
            {
                return;
            }

            var requestPath = mPaths.GetDeadletterRequestPath(deadletterId);
            if (File.Exists(requestPath))
            {
                requestPath += "." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            File.Move(commandPath, requestPath);
        }
    }
}
#endif
