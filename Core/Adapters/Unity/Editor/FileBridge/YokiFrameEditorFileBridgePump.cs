#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 在 Unity Editor 中驱动最小 FileBridge 注册、心跳、snapshot 和命令消费。
    /// </summary>
    [InitializeOnLoad]
    internal static partial class YokiFrameEditorFileBridgePump
    {
        // 心跳仅承担低频 FileBridge 存活证明；实时 Kit 状态由 Shared Memory 承载，避免机械盘被高频写入。
        private const double HEARTBEAT_INTERVAL_SECONDS = 5.0d;
        private const double COMMAND_POLL_INTERVAL_SECONDS = 0.2d;
        private static readonly string[] sHostStateKits = { "System" };
        private static long sToolProviderRevision;
        private static YokiFrameKitInteractionRegistry sKitInteractions =
            CreateKitInteractions();
        private static YokiFrameCommandDispatcher sCommandDispatcher = CreateCommandDispatcher();
        private static readonly Dictionary<string, long> sKitTelemetryVersions = new();
        private static readonly Dictionary<string, long> sKitSnapshotVersions = new();
        private static readonly HashSet<string> sTelemetryFallbackKits = new();
        private static string sSessionId = Guid.NewGuid().ToString("N");
        private static long sGeneration = DateTimeOffset.UtcNow.Ticks;
        private static string sStartedAtUtc = DateTimeOffset.UtcNow.ToString("O");
#if UNITY_EDITOR_WIN
        private static YokiFrameEditorNamedPipeFastChannelHost sFastChannelHost;
#endif
        private static string sFastChannelStartError = string.Empty;
        private static double sNextHeartbeatTime;
        private static double sNextCommandPollTime;
        private static long sSequence;
        private static bool sIsProcessingCommands;

        /// <summary>
        /// 注册 Editor update 回调，并立即写入首帧 FileBridge 文件。
        /// </summary>
        static YokiFrameEditorFileBridgePump()
        {
            if (!ShouldOwnBridge(AssetDatabase.IsAssetImportWorkerProcess()))
            {
                return;
            }

            YokiFrameEditorTelemetryWriter.RegisterLifecycleHooks();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            RegisterFastChannelLifecycleHooks();
            EnsureBridgeDirectories();
            if (IsFastChannelTransitionPending())
            {
                PublishDisconnectedState();
            }
            else
            {
                StartFastChannelHost();
                WriteCompleteBridgeState();
                sNextHeartbeatTime = EditorApplication.timeSinceStartup + HEARTBEAT_INTERVAL_SECONDS;
                ProcessPendingCommands();
            }
        }

        /// <summary>
        /// 只允许 Unity 主 Editor 进程拥有项目级 FileBridge，防止 AssetImportWorker 覆盖主会话心跳。
        /// </summary>
        /// <param name="isAssetImportWorkerProcess">当前进程是否为 Unity 资源导入 worker。</param>
        /// <returns>当前进程可以注册 FileBridge 生命周期时返回 true。</returns>
        private static bool ShouldOwnBridge(bool isAssetImportWorkerProcess)
        {
            return !isAssetImportWorkerProcess;
        }

        /// <summary>
        /// 在 Editor 主线程定期刷新心跳并消费命令队列。
        /// </summary>
        private static void OnEditorUpdate()
        {
            RefreshToolKitInteractions();
            ProcessFastChannelRequestsSafely();
            var now = EditorApplication.timeSinceStartup;
            if (now >= sNextHeartbeatTime)
            {
                WriteHeartbeatStateSafely();
                sNextHeartbeatTime = now + HEARTBEAT_INTERVAL_SECONDS;
            }

            WriteChangedKitInteractionTelemetrySafely();

            if (now >= sNextCommandPollTime)
            {
                ProcessPendingCommandsSafely();
                sNextCommandPollTime = now + COMMAND_POLL_INTERVAL_SECONDS;
            }
        }

        /// <summary>
        /// 在 Tool Provider 集合变化时重建 Registry、策略和发布缓存，使宿主无需引用具体 Tool。
        /// </summary>
        private static void RefreshToolKitInteractions()
        {
            long revision = YokiFrameToolKitInteractionCatalog.Revision;
            if (revision == sToolProviderRevision)
            {
                return;
            }

            YokiFrameKitInteractionRegistry interactions =
                CreateKitInteractions(out long capturedRevision);
            sKitInteractions = interactions;
            sCommandDispatcher = CreateCommandDispatcher();
            sKitTelemetryVersions.Clear();
            sKitSnapshotVersions.Clear();
            sTelemetryFallbackKits.Clear();
            ClearNamedTelemetryVersions();
            sToolProviderRevision = capturedRevision;
            WriteCompleteBridgeStateSafely();
        }

        /// <summary>
        /// 创建初始 Registry，并原子保存与其 Tool Provider 快照对应的版本。
        /// </summary>
        /// <returns>当前完整 Kit Interaction Registry。</returns>
        private static YokiFrameKitInteractionRegistry CreateKitInteractions()
        {
            return CreateKitInteractions(out sToolProviderRevision);
        }

        /// <summary>
        /// 创建 Unity Editor 当前完整 Interaction Registry，并追加宿主公共上下文 Provider。
        /// </summary>
        /// <param name="toolProviderRevision">捕获的 Tool Provider catalog 版本。</param>
        /// <returns>包含 Core、Tool 和 UnityEditor Context Provider 的 Registry。</returns>
        private static YokiFrameKitInteractionRegistry CreateKitInteractions(out long toolProviderRevision)
        {
            YokiFrameKitInteractionRegistry registry =
                YokiFrameCoreKitInteractions.CreateDefault(out toolProviderRevision);
            registry.Register(new UnityEditorContextInteractionProvider());
            return registry;
        }

        /// <summary>
        /// 捕获命令处理异常，避免单个损坏命令影响后续 Editor update。
        /// </summary>
        private static void ProcessPendingCommandsSafely()
        {
            try
            {
                ProcessPendingCommands();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame FileBridge command processing failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 创建最小 snapshot payload，说明当前 Unity Editor bridge 在线状态。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>snapshot payload。</returns>
        private static YokiFrameEditorStatePayload CreateStatePayload(string kit)
        {
            YokiFrameEditorStatePayload payload = new YokiFrameEditorStatePayload
            {
                kit = kit,
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                mode = GetEditorMode()
            };

            return payload;
        }

        /// <summary>
        /// 获取当前 Editor 模式，用于 heartbeat、snapshot 和命令响应。
        /// </summary>
        /// <returns>当前模式名称。</returns>
        private static string GetEditorMode()
        {
            return EditorApplication.isPlaying ? "PlayMode" : "EditMode";
        }

        /// <summary>
        /// 消费 commands 目录顶层所有待处理 JSON 命令。
        /// </summary>
        private static void ProcessPendingCommands()
        {
            if (sIsProcessingCommands || !Directory.Exists(YokiFrameEditorFileBridgePaths.GetCommandsRoot()))
            {
                return;
            }

            sIsProcessingCommands = true;
            try
            {
                foreach (var commandPath in Directory.GetFiles(
                             YokiFrameEditorFileBridgePaths.GetCommandsRoot(),
                             "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                             SearchOption.TopDirectoryOnly))
                {
                    ProcessCommandFile(commandPath);
                }
            }
            finally
            {
                sIsProcessingCommands = false;
            }
        }

        /// <summary>
        /// 读取并处理单个命令文件，确保成功或失败都会产生终态证据。
        /// </summary>
        /// <param name="commandPath">命令文件路径。</param>
        private static void ProcessCommandFile(string commandPath)
        {
            try
            {
                var envelope = ReadCommandEnvelope(commandPath);
                var commandFileBytes = new FileInfo(commandPath).Length;
                var response = ExecuteCommand(envelope, commandFileBytes);
                WriteResponse(envelope.requestId, response);
                ArchiveCommand(commandPath);
            }
            catch (Exception exception)
            {
                MoveToDeadletter(commandPath, "CommandProcessingFailed", exception.Message);
            }
        }

        /// <summary>
        /// 读取并校验命令信封，拒绝路径不安全或非当前 engine 的命令。
        /// </summary>
        /// <param name="commandPath">命令文件路径。</param>
        /// <returns>已校验的命令信封。</returns>
        private static YokiFrameEditorCommandEnvelope ReadCommandEnvelope(string commandPath)
        {
            YokiFrameEditorCommandPolicy.EnsureCommandFileSize(commandPath);
            var json = File.ReadAllText(commandPath);
            var envelope = YokiFrameEditorFileBridgeJson.FromJson<YokiFrameEditorCommandEnvelope>(json);
            ValidateEnvelope(envelope);
            return envelope;
        }

        /// <summary>
        /// 校验命令信封中的路径字段和协议版本。
        /// </summary>
        /// <param name="envelope">待校验命令信封。</param>
        private static void ValidateEnvelope(YokiFrameEditorCommandEnvelope envelope)
        {
            if (envelope == null
                || envelope.protocolVersion != YokiFrameFileBridgeContract.PROTOCOL_VERSION
                || envelope.engineId != YokiFrameEditorFileBridgePaths.ENGINE_ID)
            {
                throw new InvalidDataException("Command envelope protocolVersion or engineId is invalid.");
            }

            if (!YokiFrameEditorFileBridgeJson.IsSafeId(envelope.requestId)
                || !YokiFrameEditorFileBridgeJson.IsSafeId(envelope.kit)
                || !YokiFrameEditorFileBridgeJson.IsSafeId(envelope.action))
            {
                throw new InvalidDataException("Command envelope contains unsafe requestId, kit or action.");
            }
        }

        /// <summary>
        /// 根据 Kit/action 执行最小命令集。
        /// </summary>
        /// <param name="envelope">已校验命令信封。</param>
        /// <param name="commandFileBytes">命令文件字节数，供 Runtime policy 复核文件大小边界。</param>
        /// <returns>命令响应。</returns>
        private static YokiFrameEditorCommandResponse ExecuteCommand(
            YokiFrameEditorCommandEnvelope envelope,
            long commandFileBytes)
        {
            var result = sCommandDispatcher.Dispatch(CreateCommandRequest(envelope, commandFileBytes));
            if (result.IsSuccess)
            {
                return CreateSuccessResponse(envelope.requestId, result.ResultJson);
            }

            return CreateErrorResponse(envelope.requestId, result.ErrorCode, result.ErrorMessage);
        }

        /// <summary>
        /// 创建 ping 命令结果。
        /// </summary>
        /// <returns>ping 业务结果。</returns>
        private static YokiFrameEditorPingResult CreatePingResult()
        {
            return new YokiFrameEditorPingResult
            {
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence
            };
        }

        /// <summary>
        /// 创建 bridge_status 命令结果。
        /// </summary>
        /// <returns>bridge_status 业务结果。</returns>
        private static YokiFrameEditorBridgeStatusResult CreateBridgeStatusResult()
        {
            YokiFrameEditorProtocolStorageInfo storage = YokiFrameEditorFileBridgeJson.ReadProtocolStorageDiagnostics(YokiFrameEditorFileBridgePaths.GetEngineRoot());
            return new YokiFrameEditorBridgeStatusResult
            {
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                pending = YokiFrameEditorFileBridgeJson.CountJsonFiles(YokiFrameEditorFileBridgePaths.GetCommandsRoot()),
                archive = YokiFrameEditorFileBridgeJson.CountJsonFiles(YokiFrameEditorFileBridgePaths.GetArchiveRoot()),
                deadletter = YokiFrameEditorFileBridgeJson.CountJsonFiles(YokiFrameEditorFileBridgePaths.GetDeadletterRoot()),
                results = YokiFrameEditorFileBridgeJson.CountJsonFiles(YokiFrameEditorFileBridgePaths.GetResultsRoot()),
                protocolFileCount = storage.fileCount,
                protocolBytes = storage.totalBytes,
                oldestProtocolFileUtc = storage.oldestFileUtc,
                backpressureActive = false,
                lastPollLimitReason = string.Empty,
                bridgeBusyCount = 0,
                lastError = string.Empty
            };
        }

        /// <summary>
        /// 创建 refresh_snapshots 命令结果，说明本轮强制刷新覆盖了哪些首批 Kit。
        /// </summary>
        /// <returns>refresh_snapshots 业务结果。</returns>
        private static YokiFrameEditorRefreshSnapshotsResult CreateRefreshSnapshotsResult()
        {
            var refreshedKits = CreatePublishedKitNames();
            return new YokiFrameEditorRefreshSnapshotsResult
            {
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                refreshedKits = refreshedKits,
                snapshotCount = refreshedKits.Length,
                telemetry = Application.platform == RuntimePlatform.WindowsEditor ? "shared-memory-v1" : "snapshot-only"
            };
        }

        /// <summary>创建当前宿主发布 state Snapshot 的 Kit 清单，供 refresh_snapshots 结果审计。</summary>
        /// <returns>遗留宿主状态与已迁移 Provider 的合并清单。</returns>
        private static string[] CreatePublishedKitNames()
        {
            var providers = sKitInteractions.Providers;
            string[] names = new string[sHostStateKits.Length + providers.Count];
            Array.Copy(sHostStateKits, names, sHostStateKits.Length);
            for (var index = 0; index < providers.Count; index++)
            {
                names[sHostStateKits.Length + index] = providers[index].Kit;
            }

            return names;
        }

        /// <summary>
        /// 创建 get_environment 命令结果，供 CLI 和 Workbench 诊断当前 Editor 运行环境。
        /// </summary>
        /// <returns>get_environment 业务结果。</returns>
        private static YokiFrameEditorEnvironmentResult CreateEnvironmentResult()
        {
            return new YokiFrameEditorEnvironmentResult
            {
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                isBatchMode = Application.isBatchMode,
                projectPath = YokiFrameEditorFileBridgePaths.GetProjectRoot(),
                dataPath = Application.dataPath,
                persistentDataPath = Application.persistentDataPath,
                temporaryCachePath = Application.temporaryCachePath,
                yokiFrameRoot = YokiFrameEditorFileBridgePaths.GetYokiFrameRoot(),
                engineRoot = YokiFrameEditorFileBridgePaths.GetEngineRoot(),
                telemetry = Application.platform == RuntimePlatform.WindowsEditor ? "shared-memory-v1" : "snapshot-only"
            };
        }

    }
}

#endif
