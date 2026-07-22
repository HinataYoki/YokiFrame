#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot Editor Host 的命令读取、策略执行、terminal response、archive 和 deadletter。
    /// </summary>
    public sealed partial class GodotEditorFileBridgeHost
    {
        /// <summary>
        /// 消费 commands 顶层全部 JSON，确保每个文件进入 response/archive 或 deadletter 终态。
        /// </summary>
        /// <returns>本轮尝试处理的命令文件数量。</returns>
        public int ProcessPendingCommands()
        {
            EnsureRunning();
            if (mIsProcessingCommands || !Directory.Exists(mPaths.CommandsRoot))
            {
                return 0;
            }

            mIsProcessingCommands = true;
            try
            {
                var commandPaths = ReadPendingCommandPaths();
                for (var index = 0; index < commandPaths.Length; index++)
                {
                    ProcessCommandFile(commandPaths[index]);
                }

                return commandPaths.Length;
            }
            finally
            {
                mIsProcessingCommands = false;
            }
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
        /// 读取并稳定排序 commands 顶层的 JSON 路径。
        /// </summary>
        /// <returns>待处理命令路径。</returns>
        private string[] ReadPendingCommandPaths()
        {
            var commandPaths = Directory.GetFiles(
                mPaths.CommandsRoot,
                "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                SearchOption.TopDirectoryOnly);
            Array.Sort(commandPaths, StringComparer.OrdinalIgnoreCase);
            return commandPaths;
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
        /// 处理单个命令文件；异常转换为 deadletter 后继续后续请求。
        /// </summary>
        /// <param name="commandPath">命令完整路径。</param>
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

            if (!YokiFrameSafeIdContract.IsSafeId(envelope.RequestId)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.Kit)
                || !YokiFrameSafeIdContract.IsSafeId(envelope.Action))
            {
                throw new InvalidDataException("Command envelope contains an unsafe identifier.");
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
                commandFileBytes);
            var result = mDispatcher.Dispatch(request);
            return result.IsSuccess
                ? CreateSuccessResponse(envelope.RequestId, result.ResultJson)
                : CreateErrorResponse(envelope.RequestId, result.ErrorCode, result.ErrorMessage);
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
        /// 原子写入指定请求的 terminal response。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="response">完整响应。</param>
        private void WriteResponse(string requestId, GodotEditorCommandResponse response)
        {
            GodotEditorFileBridgeJson.WriteAtomic(
                mPaths.GetResponsePath(requestId),
                GodotEditorFileBridgeJson.Serialize(response));
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
