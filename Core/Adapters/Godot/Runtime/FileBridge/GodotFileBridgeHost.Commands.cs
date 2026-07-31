#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot Runtime Host 的命令读取、dispatcher、terminal response、archive 和 deadletter。
    /// </summary>
    public sealed partial class GodotFileBridgeHost
    {
        /// <summary>
        /// 消费 commands 顶层全部 JSON，确保每个文件进入 response/archive 或 deadletter 终态。
        /// </summary>
        /// <returns>本轮尝试处理的命令文件数量。</returns>
        public int ProcessPendingCommands()
        {
            EnsureRunning();
            if (mIsProcessingCommands)
            {
                return 0;
            }

            if (!Directory.Exists(mPaths.CommandsRoot))
            {
                TryPruneStorage();
                return 0;
            }

            mIsProcessingCommands = true;
            try
            {
                var commandPaths = Directory.GetFiles(
                    mPaths.CommandsRoot,
                    "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                    SearchOption.TopDirectoryOnly);
                Array.Sort(commandPaths, StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < commandPaths.Length; index++)
                {
                    ProcessCommandFile(commandPaths[index]);
                }

                return commandPaths.Length;
            }
            finally
            {
                mIsProcessingCommands = false;
                TryPruneStorage();
            }
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
            catch (Exception)
            {
                // 清理是旁路维护，任何权限或并发异常都留待下一轮重试。
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
            YokiFrameCommandDescriptor[] commands = new YokiFrameCommandDescriptor[3 + kitCommands.Length];
            commands[0] = new YokiFrameCommandDescriptor("System", "ping", YokiFrameCommandKind.ReadOnly);
            commands[1] = new YokiFrameCommandDescriptor("System", "bridge_status", YokiFrameCommandKind.ReadOnly);
            commands[2] = new YokiFrameCommandDescriptor("System", "list_commands", YokiFrameCommandKind.ReadOnly);
            Array.Copy(kitCommands, 0, commands, 3, kitCommands.Length);
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
        /// 处理单个命令文件；异常会转换为 deadletter，不阻塞后续命令。
        /// </summary>
        /// <param name="commandPath">命令文件完整路径。</param>
        private void ProcessCommandFile(string commandPath)
        {
            try
            {
                var envelope = ReadCommandEnvelope(commandPath);
                var response = ExecuteCommand(envelope, new FileInfo(commandPath).Length);
                WriteResponse(envelope.RequestId, response);
                ArchiveCommand(commandPath);
            }
            catch (Exception exception)
            {
                mLastError = exception.Message;
                MoveToDeadletter(commandPath, "CommandProcessingFailed", exception.Message);
            }
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
            return envelope;
        }

        /// <summary>
        /// 校验命令信封的共享协议字段和路径标识，并拒绝损坏 payload JSON。
        /// </summary>
        /// <param name="envelope">待校验命令信封。</param>
        private static void ValidateEnvelope(GodotCommandEnvelope envelope)
        {
            if (envelope.ProtocolVersion != YokiFrameFileBridgeContract.PROTOCOL_VERSION
                || envelope.EngineId != ENGINE_ID)
            {
                throw new InvalidDataException("Command envelope protocolVersion or engineId is invalid.");
            }

            if (!YokiFrameSafeIdContract.IsSafeId(envelope.RequestId)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.Kit)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.Action))
            {
                throw new InvalidDataException("Command envelope contains unsafe requestId, kit or action.");
            }

            GodotFileBridgeJson.ValidatePayloadJson(envelope.PayloadJson);
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
                commandFileBytes);
            var result = mDispatcher.Dispatch(request);
            return result.IsSuccess
                ? CreateSuccessResponse(envelope.RequestId, result.ResultJson)
                : CreateErrorResponse(envelope.RequestId, result.ErrorCode, result.ErrorMessage);
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
                BackpressureActive = false,
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
        /// 原子写入指定请求的 terminal response。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="response">响应 DTO。</param>
        private void WriteResponse(string requestId, GodotCommandResponse response)
        {
            GodotFileBridgeJson.WriteAtomic(
                mPaths.GetResponsePath(requestId),
                GodotFileBridgeJson.Serialize(response));
        }

        /// <summary>
        /// 将成功处理的命令移动到 archive，冲突时追加 UTC 毫秒后缀。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
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
        /// 写入 deadletter 诊断，并移动原始请求作为不可丢失证据。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        private void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
        {
            var deadletterId = CreateDeadletterId(commandPath);
            GodotDeadletterInfo info = new GodotDeadletterInfo
            {
                SourcePath = commandPath,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            GodotFileBridgeJson.WriteAtomic(
                mPaths.GetDeadletterInfoPath(deadletterId),
                GodotFileBridgeJson.Serialize(info));
            MoveDeadletterRequest(commandPath, deadletterId);
        }

        /// <summary>
        /// 根据原文件名创建安全 deadletter ID，不安全时使用时间和随机后缀。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        /// <returns>安全 deadletter ID。</returns>
        private static string CreateDeadletterId(string commandPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(commandPath);
            return YokiFrameSafeIdContract.IsSafeId(fileName)
                ? fileName
                : "invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 移动 deadletter 原始请求，目标冲突时追加 UTC 毫秒后缀。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
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
