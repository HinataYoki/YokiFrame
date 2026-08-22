#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot Runtime Host 的命令读取、dispatcher、terminal response、archive 和 deadletter。
    /// </summary>
    public sealed partial class GodotFileBridgeHost
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
        /// 解析、执行并序列化 Godot Runtime 命令，供公共协调器写入 terminal response。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <returns>已序列化的命令执行结果。</returns>
        private YokiFrameHostCommandExecution ExecuteCommandForCoordinator(string commandPath)
        {
            var envelope = ReadCommandEnvelope(commandPath);
            var response = ExecuteCommand(envelope, new FileInfo(commandPath).Length);
            return new YokiFrameHostCommandExecution(
                envelope.RequestId,
                GodotFileBridgeJson.Serialize(response));
        }


        /// <summary>
        /// 按五分钟节流回收终态 FileBridge 证据；清理失败不影响 Runtime Host 继续服务。
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
                mLastError = "Godot Runtime FileBridge storage cleanup failed: " + exception.Message;
            }

            mNextStorageCleanupUtc = nowUtc.AddMinutes(5.0d);
        }

        /// <summary>
        /// 创建允许 System 与当前 Kit Registry 命令的 Runtime policy。
        /// </summary>
        /// <returns>共享 Runtime dispatcher。</returns>
        private YokiFrameCommandDispatcher CreateCommandDispatcher()
        {
            var kitCommands = mKitInteractions.GetCommandDescriptors();
            var systemCommands = GodotSystemCommandHandler.CommandDescriptors;
            YokiFrameCommandDescriptor[] commands = new YokiFrameCommandDescriptor[systemCommands.Length + kitCommands.Length];
            Array.Copy(systemCommands, 0, commands, 0, systemCommands.Length);
            Array.Copy(kitCommands, 0, commands, systemCommands.Length, kitCommands.Length);
            YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(commands);
            return new YokiFrameCommandDispatcher(
                policy,
                new IYokiFrameCommandHandler[]
                {
                    new GodotSystemCommandHandler(
                        CreatePingResultJson,
                        CreateBridgeStatusResultJson,
                        () => CreateCommandCatalogJson(policy.AllowedCommands)),
                    mKitInteractions
                });
        }

        /// <summary>
        /// 根据当前 Godot Runtime policy 创建稳定排序的实时命令目录。
        /// </summary>
        /// <param name="commands">当前 policy 允许的命令描述。</param>
        /// <returns>可写入 terminal response 的命令目录 JSON。</returns>
        private string CreateCommandCatalogJson(IReadOnlyList<YokiFrameCommandDescriptor> commands)
        {
            var groups = new Dictionary<string, List<GodotCommandCatalogAction>>(StringComparer.Ordinal);
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (!groups.TryGetValue(command.Kit, out var actions))
                {
                    actions = new List<GodotCommandCatalogAction>();
                    groups.Add(command.Kit, actions);
                }

                actions.Add(new GodotCommandCatalogAction
                {
                    Action = command.Action,
                    Kind = command.Kind.ToString()
                });
            }

            List<GodotCommandCatalogKit> kits = new List<GodotCommandCatalogKit>();
            foreach (var group in groups)
            {
                group.Value.Sort(static (left, right) => string.CompareOrdinal(left.Action, right.Action));
                kits.Add(new GodotCommandCatalogKit
                {
                    Kit = group.Key,
                    Actions = group.Value.ToArray()
                });
            }

            kits.Sort(static (left, right) => string.CompareOrdinal(left.Kit, right.Kit));
            return GodotFileBridgeJson.Serialize(new GodotCommandCatalogResult
            {
                EngineId = ENGINE_ID,
                Mode = RUNTIME_MODE,
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence,
                Kits = kits.ToArray()
            });
        }

        /// <summary>
        /// 读取命令文件，执行文件大小、JSON、协议、engine、safe ID 和 payload 语法校验。
        /// </summary>
        /// <param name="commandPath">命令文件完整路径。</param>
        /// <returns>已校验命令信封。</returns>
        private static GodotCommandEnvelope ReadCommandEnvelope(string commandPath)
        {
            FileInfo fileInfo = new FileInfo(commandPath);
            if (fileInfo.Length > YokiFrameFileBridgeContract.COMMAND_FILE_MAX_BYTES)
            {
                throw new InvalidDataException("Command file exceeds the Runtime FileBridge byte limit.");
            }

            var envelope = GodotFileBridgeJson.Deserialize<GodotCommandEnvelope>(File.ReadAllText(commandPath));
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
        /// 校验命令信封的共享协议字段和路径标识，并拒绝损坏 payload JSON。
        /// </summary>
        /// <param name="envelope">待校验命令信封。</param>
        private static void ValidateEnvelope(GodotCommandEnvelope envelope)
        {
            var error = YokiFrameCommandEnvelopeValidator.Validate(
                envelope.ProtocolVersion,
                envelope.EngineId,
                ENGINE_ID,
                envelope.Source,
                envelope.RequestId,
                envelope.Kit,
                envelope.Action,
                envelope.TimeoutMs,
                envelope.CreatedAtUtc,
                envelope.PayloadJson);
            if (error != null)
            {
                throw new InvalidDataException(error);
            }
        }

        /// <summary>
        /// 把已校验信封转换为共享请求，经 Runtime dispatcher 返回可落盘终态响应。
        /// </summary>
        /// <param name="envelope">已校验命令信封。</param>
        /// <param name="commandFileBytes">命令文件字节数。</param>
        /// <returns>terminal response。</returns>
        private GodotCommandResponse ExecuteCommand(GodotCommandEnvelope envelope, long commandFileBytes)
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
        /// 创建 System/ping 的会话结果 JSON。
        /// </summary>
        /// <returns>ping 结果 JSON。</returns>
        private string CreatePingResultJson()
        {
            return GodotFileBridgeJson.Serialize(new GodotPingResult
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence
            });
        }

        /// <summary>
        /// 创建 System/bridge_status 的队列、存储和 fallback 诊断 JSON。
        /// </summary>
        /// <returns>bridge_status 结果 JSON。</returns>
        private string CreateBridgeStatusResultJson()
        {
            var storage = GodotFileBridgeJson.ReadStorageDiagnostics(mPaths.EngineRoot);
            return GodotFileBridgeJson.Serialize(new GodotBridgeStatusResult
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence,
                Pending = GodotFileBridgeJson.CountJsonFiles(mPaths.CommandsRoot),
                Archive = GodotFileBridgeJson.CountJsonFiles(mPaths.ArchiveRoot),
                Deadletter = GodotFileBridgeJson.CountJsonFiles(mPaths.DeadletterRoot),
                Results = GodotFileBridgeJson.CountJsonFiles(mPaths.ResultsRoot),
                ProtocolFileCount = storage.FileCount,
                ProtocolBytes = storage.TotalBytes,
                OldestProtocolFileUtc = storage.OldestFileUtc,
                BackpressureActive = mCommandCoordinator.LastBatchWasLimited,
                LastPollLimitReason = mCommandCoordinator.LastBatchLimitReason,
                LastError = mLastError,
                FastChannel = "filebridge-fallback"
            });
        }

        /// <summary>
        /// 创建成功响应。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="resultJson">业务结果 JSON。</param>
        /// <returns>成功 terminal response。</returns>
        private static GodotCommandResponse CreateSuccessResponse(string requestId, string resultJson)
        {
            return new GodotCommandResponse
            {
                RequestId = requestId,
                Status = "Success",
                ResultJson = resultJson,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 创建错误响应，保证策略或 handler 错误不会让调用侧超时。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        /// <returns>错误 terminal response。</returns>
        private static GodotCommandResponse CreateErrorResponse(
            string requestId,
            string errorCode,
            string errorMessage)
        {
            return new GodotCommandResponse
            {
                RequestId = requestId,
                Status = "Error",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>

        /// <summary>
        /// 序列化与既有 wire 格式一致的 deadletter 诊断 JSON，供共享命令存储写入证据。
        /// </summary>
        private static string SerializeDeadletterInfo(string sourcePath, string errorCode, string errorMessage)
        {
            return GodotFileBridgeJson.Serialize(new GodotDeadletterInfo
            {
                SourcePath = sourcePath,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
        }
    }
}
#endif
