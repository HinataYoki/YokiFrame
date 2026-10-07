#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot Editor Host 的命令读取、策略执行、terminal response、archive 和 deadletter。
    /// </summary>
    public sealed partial class GodotEditorFileBridgeHost
    {
        private static readonly TimeSpan PROCESSING_LEASE = TimeSpan.FromSeconds(60);

        private long mToolProviderRevision = -1;
        private YokiFrameKitInteractionRegistry mKitInteractions;

        /// <summary>
        /// 消费 commands 顶层全部 JSON，确保每个文件进入 response/archive 或 deadletter 终态。
        /// </summary>
        /// <returns>本轮尝试处理的命令文件数量。</returns>
        public int ProcessPendingCommands()
        {
            EnsureRunning();
            RefreshCatalogKitsIfNeeded();
            var processed = mCommandCoordinator.ProcessPendingCommands();
            PublishChangedKitSnapshots();
            return processed;
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
                GodotFileBridgeJson.Serialize(response));
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

            mNextStorageCleanupUtc = nowUtc.AddSeconds(YokiFrameFileBridgePumpSchedule.STORAGE_CLEANUP_INTERVAL_SECONDS);
        }

        /// <summary>
        /// 创建 Editor dispatcher：System 只读命令 + catalog 显式注册的 Kit Provider（例如 Engine）。
        /// </summary>
        /// <returns>Editor 命令 dispatcher。</returns>
        private YokiFrameCommandDispatcher CreateCommandDispatcher()
        {
            // 命令面的唯一来源是各 Provider 自己声明的描述符；这里只做聚合，不硬编码任何 Kit。
            mToolProviderRevision = YokiFrameToolKitInteractionCatalog.Revision;
            mKitInteractions = new YokiFrameKitInteractionRegistry();
            YokiFrameToolKitInteractionCatalog.RegisterProviders(mKitInteractions);
            IReadOnlyList<IYokiFrameKitInteractionProvider> providers = mKitInteractions.Providers;

            var descriptors = new List<YokiFrameCommandDescriptor>(GodotEditorSystemCommandHandler.CommandDescriptors.Length);
            descriptors.AddRange(GodotEditorSystemCommandHandler.CommandDescriptors);
            for (var index = 0; index < providers.Count; index++)
            {
                descriptors.AddRange(providers[index].Commands);
            }

            YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(descriptors.ToArray());
            var handlers = new List<IYokiFrameCommandHandler>(providers.Count + 1)
            {
                new GodotEditorSystemCommandHandler(
                    CreatePingResultJson,
                    CreateBridgeStatusResultJson,
                    // System 分组只列 System 自己的命令；其余 Kit 由 CreateCatalogKits 按 Provider 分组列出。
                    () => CreateCommandCatalogJson(GodotEditorSystemCommandHandler.CommandDescriptors),
                    CreateEnvironmentResultJson)
            };
            for (var index = 0; index < providers.Count; index++)
            {
                handlers.Add(providers[index]);
            }

            return new YokiFrameCommandDispatcher(policy, handlers.ToArray());
        }

        /// <summary>
        /// catalog 版本变化时重建命令策略，使后注册的 Kit 同样能被服务（插件启动顺序无关）。
        /// </summary>
        private void RefreshCatalogKitsIfNeeded()
        {
            if (YokiFrameToolKitInteractionCatalog.Revision == mToolProviderRevision)
            {
                return;
            }

            mDispatcher = CreateCommandDispatcher();
        }

        /// <summary>
        /// 根据当前 Editor policy 创建稳定排序的实时命令目录。
        /// </summary>
        /// <param name="commands">当前 policy 允许的命令。</param>
        /// <returns>命令目录 JSON。</returns>
        private string CreateCommandCatalogJson(IReadOnlyList<YokiFrameCommandDescriptor> commands)
        {
            List<YokiFrameFileBridgeCommandCatalogAction> actions = new List<YokiFrameFileBridgeCommandCatalogAction>();
            for (var index = 0; index < commands.Count; index++)
            {
                actions.Add(new YokiFrameFileBridgeCommandCatalogAction
                {
                    Action = commands[index].Action,
                    Kind = commands[index].Kind.ToString()
                });
            }

            actions.Sort(static (left, right) => string.CompareOrdinal(left.Action, right.Action));
            return GodotFileBridgeJson.Serialize(new YokiFrameFileBridgeCommandCatalogResult
            {
                EngineId = ENGINE_ID,
                Mode = EDITOR_MODE,
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence,
                Kits = CreateCatalogKits(actions)
            });
        }

        /// <summary>
        /// 组装按 Kit 分组的命令目录：System 之后追加 catalog 注册的 Kit（例如 Engine）。
        /// </summary>
        /// <param name="systemActions">System 命令。</param>
        /// <returns>目录分组。</returns>
        private YokiFrameFileBridgeCommandCatalogKit[] CreateCatalogKits(
            List<YokiFrameFileBridgeCommandCatalogAction> systemActions)
        {
            var kits = new List<YokiFrameFileBridgeCommandCatalogKit>
            {
                new YokiFrameFileBridgeCommandCatalogKit { Kit = "System", Actions = systemActions.ToArray() }
            };
            IReadOnlyList<IYokiFrameKitInteractionProvider> providers = mKitInteractions == null
                ? Array.Empty<IYokiFrameKitInteractionProvider>()
                : mKitInteractions.Providers;
            for (var index = 0; index < providers.Count; index++)
            {
                IReadOnlyList<YokiFrameCommandDescriptor> commands = providers[index].Commands;
                var kitActions = new List<YokiFrameFileBridgeCommandCatalogAction>(commands.Count);
                for (var commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    kitActions.Add(new YokiFrameFileBridgeCommandCatalogAction
                    {
                        Action = commands[commandIndex].Action,
                        Kind = commands[commandIndex].Kind.ToString()
                    });
                }

                kitActions.Sort(static (left, right) => string.CompareOrdinal(left.Action, right.Action));
                kits.Add(new YokiFrameFileBridgeCommandCatalogKit
                {
                    Kit = providers[index].Kit,
                    Actions = kitActions.ToArray()
                });
            }

            return kits.ToArray();
        }

        /// <summary>
        /// 读取命令文件并执行文件大小、JSON 和信封校验。
        /// </summary>
        /// <param name="commandPath">命令完整路径。</param>
        /// <returns>已校验命令信封。</returns>
        private static YokiFrameFileBridgeCommandEnvelope ReadCommandEnvelope(string commandPath)
        {
            FileInfo fileInfo = new FileInfo(commandPath);
            if (fileInfo.Length > YokiFrameFileBridgeContract.COMMAND_FILE_MAX_BYTES)
            {
                throw new InvalidDataException("Command file exceeds the Editor FileBridge byte limit.");
            }

            var envelope = GodotFileBridgeJson.Deserialize<YokiFrameFileBridgeCommandEnvelope>(
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
        private static void ValidateEnvelope(YokiFrameFileBridgeCommandEnvelope envelope)
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
        private YokiFrameFileBridgeCommandResponse ExecuteCommand(
            YokiFrameFileBridgeCommandEnvelope envelope,
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
            return GodotFileBridgeJson.Serialize(new YokiFrameFileBridgePingResult
            {
                EngineId = ENGINE_ID,
                Mode = EDITOR_MODE,
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
            var storage = GodotFileBridgeJson.ReadStorageDiagnostics(mPaths.EngineRoot);
            return GodotFileBridgeJson.Serialize(new YokiFrameFileBridgeStatusResult
            {
                EngineId = ENGINE_ID,
                Mode = EDITOR_MODE,
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
                FastChannel = "filebridge-only"
            });
        }

        /// <summary>创建 Godot Editor 当前用户数据目录环境结果。</summary>
        /// <returns>供 Workbench 解析的 Godot 用户数据根目录。</returns>
        private static string CreateEnvironmentResultJson()
        {
            return GodotFileBridgeJson.Serialize(new GodotEditorEnvironmentResult
            {
                UserDataDir = OS.GetUserDataDir()
            });
        }

        /// <summary>
        /// 创建成功 terminal response。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="resultJson">业务结果 JSON。</param>
        /// <returns>成功响应。</returns>
        private static YokiFrameFileBridgeCommandResponse CreateSuccessResponse(
            string requestId,
            string resultJson)
        {
            return new YokiFrameFileBridgeCommandResponse
            {
                EngineId = ENGINE_ID,
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
        private static YokiFrameFileBridgeCommandResponse CreateErrorResponse(
            string requestId,
            string errorCode,
            string errorMessage)
        {
            return new YokiFrameFileBridgeCommandResponse
            {
                EngineId = ENGINE_ID,
                RequestId = requestId,
                Status = "Error",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 序列化与既有 wire 格式一致的 deadletter 诊断 JSON，供共享命令存储写入证据。
        /// </summary>
        /// <param name="sourcePath">无法消费的原始命令路径。</param>
        /// <param name="errorCode">拒绝原因错误码。</param>
        /// <param name="errorMessage">拒绝原因说明。</param>
        /// <returns>deadletter 诊断 JSON。</returns>
        private static string SerializeDeadletterInfo(string sourcePath, string errorCode, string errorMessage)
        {
            return GodotFileBridgeJson.Serialize(new YokiFrameFileBridgeDeadletterInfo
            {
                EngineId = ENGINE_ID,
                SourcePath = sourcePath,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
        }
    }
}
#endif
