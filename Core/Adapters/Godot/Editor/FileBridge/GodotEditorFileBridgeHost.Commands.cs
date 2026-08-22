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
            // 命令面唯一声明在 GodotEditorSystemCommandHandler.CommandDescriptors，策略直接聚合。
            YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(
                GodotEditorSystemCommandHandler.CommandDescriptors);
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
        /// <summary>
        /// 序列化与既有 wire 格式一致的 deadletter 诊断 JSON，供共享命令存储写入证据。
        /// </summary>
        private static string SerializeDeadletterInfo(string sourcePath, string errorCode, string errorMessage)
        {
            return GodotEditorFileBridgeJson.Serialize(new GodotEditorDeadletterInfo
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
