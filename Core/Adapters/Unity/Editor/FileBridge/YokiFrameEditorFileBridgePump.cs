#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const double STORAGE_CLEANUP_INTERVAL_SECONDS = 300.0d;
        private static readonly TimeSpan PROCESSING_LEASE = TimeSpan.FromSeconds(60);
        private static readonly string[] sHostStateKits = { "System" };
        private static long sToolProviderRevision;
        private static YokiFrameKitInteractionRegistry sKitInteractions =
            CreateKitInteractions();
        // 声明顺序早于 sCommandDispatcher，保证 CreateCommandDispatcher 写入的策略缓存不会被后续字段初始化器覆盖。
        private static YokiFrameCommandPolicy sHostCommandPolicy;
        private static YokiFrameCommandDispatcher sCommandDispatcher = CreateCommandDispatcher();
        private static readonly Dictionary<string, long> sKitTelemetryVersions = new();
        private static readonly Dictionary<string, long> sKitSnapshotVersions = new();
        private static readonly HashSet<string> sTelemetryFallbackKits = new();
        private static string sCommandProcessingError = string.Empty;
        private static string sSessionId = Guid.NewGuid().ToString("N");
        private static long sGeneration = DateTimeOffset.UtcNow.Ticks;
        private static string sStartedAtUtc = DateTimeOffset.UtcNow.ToString("O");
#if UNITY_EDITOR_WIN
        private static YokiFrameEditorNamedPipeFastChannelHost sFastChannelHost;
#endif
        private static YokiFrameHostAdmissionLease sAdmissionLease;
        private static string sFastChannelStartError = string.Empty;
        private static double sNextHeartbeatTime;
        private static double sNextCommandPollTime;
        private static double sNextStorageCleanupTime;
        private static long sSequence;
        private static YokiFrameHostCommandCoordinator sCommandCoordinator;

        /// <summary>
        /// 注册 Editor update 回调，并立即写入首帧 FileBridge 文件。
        /// </summary>
        static YokiFrameEditorFileBridgePump()
        {
            if (!ShouldOwnBridge(AssetDatabase.IsAssetImportWorkerProcess()))
            {
                return;
            }

            YokiFrameHostAdmissionResult admissionResult;
            Exception admissionError;
            try
            {
                admissionResult = YokiFrameHostAdmissionLease.TryAcquire(
                    YokiFrameEditorFileBridgePaths.GetAdmissionLockPath(),
                    out sAdmissionLease,
                    out admissionError);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame unity-editor Host admission path validation failed: " + exception.Message);
                sAdmissionLease = null;
                return;
            }

            if (admissionResult != YokiFrameHostAdmissionResult.Acquired)
            {
                var message = admissionResult == YokiFrameHostAdmissionResult.AlreadyOwned
                    ? "YokiFrame unity-editor Host is already owned by another process."
                    : "YokiFrame unity-editor Host admission failed: " + admissionError?.Message;
                Debug.LogWarning(message);
                sAdmissionLease = null;
                return;
            }

            YokiFrameEditorTelemetryWriter.RegisterLifecycleHooks();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            RegisterFastChannelLifecycleHooks();
            EnsureBridgeDirectories();
            TryPruneProjectStorage();
            sNextStorageCleanupTime = EditorApplication.timeSinceStartup + STORAGE_CLEANUP_INTERVAL_SECONDS;
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

            if (now >= sNextStorageCleanupTime)
            {
                TryPruneProjectStorage();
                sNextStorageCleanupTime = now + STORAGE_CLEANUP_INTERVAL_SECONDS;
            }

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
        private static int ProcessPendingCommands()
        {
            return GetCommandCoordinator().ProcessPendingCommands();
        }

        /// <summary>
        /// 创建并缓存 Unity Editor 的命令生命周期协调器。
        /// </summary>
        /// <returns>共享命令协调器。</returns>
        private static YokiFrameHostCommandCoordinator GetCommandCoordinator()
        {
            if (sCommandCoordinator == null)
            {
                sCommandCoordinator = new YokiFrameHostCommandCoordinator(
                    new UnityEditorHostCommandStore(),
                    ExecuteCommandForCoordinator,
                    PROCESSING_LEASE,
                    exception => sCommandProcessingError = exception.Message);
            }

            return sCommandCoordinator;
        }

        /// <summary>
        /// 解析、执行并序列化 Unity Editor 命令，供公共协调器写入 terminal response。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <returns>已序列化的命令执行结果。</returns>
        private static YokiFrameHostCommandExecution ExecuteCommandForCoordinator(string commandPath)
        {
            var envelope = ReadCommandEnvelope(commandPath);
            var response = ExecuteCommand(envelope, new FileInfo(commandPath).Length);
            return new YokiFrameHostCommandExecution(
                envelope.requestId,
                YokiFrameEditorFileBridgeJson.ToJson(response));
        }

        /// <summary>
        /// Unity Editor 的 FileBridge 存储适配器，保留 Unity 路径与 JSON 规则。
        /// </summary>
        private sealed class UnityEditorHostCommandStore : IYokiFrameHostCommandStore
        {
            /// <summary>
            /// 复核 Unity Editor 的 FileBridge 根路径。
            /// </summary>
            public void EnsureReady()
            {
                YokiFrameEditorFileBridgePaths.EnsureBridgeRootsAreSafe();
            }

            /// <summary>
            /// 获取 commands 根目录是否存在。
            /// </summary>
            public bool PendingRootExists => Directory.Exists(YokiFrameEditorFileBridgePaths.GetCommandsRoot());

            /// <summary>
            /// 读取 Unity 原有枚举顺序的 pending 命令。
            /// </summary>
            public IReadOnlyList<string> ReadPendingCommandPaths()
            {
                return Directory.GetFiles(
                    YokiFrameEditorFileBridgePaths.GetCommandsRoot(),
                    "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                    SearchOption.TopDirectoryOnly);
            }

            /// <summary>
            /// 读取 processing 目录中的命令。
            /// </summary>
            public IReadOnlyList<string> ReadProcessingCommandPaths()
            {
                var processingRoot = YokiFrameEditorFileBridgePaths.GetProcessingRoot();
                return Directory.Exists(processingRoot)
                    ? Directory.GetFiles(processingRoot, "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION, SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();
            }

            /// <summary>
            /// 原子 claim Unity pending 命令。
            /// </summary>
            public YokiFrameFileBridgeClaimResult TryClaim(
                string pendingPath,
                out string claimedPath,
                out Exception storageException)
            {
                return YokiFrameFileBridgeClaim.TryClaim(
                    pendingPath,
                    YokiFrameEditorFileBridgePaths.GetProcessingRoot(),
                    out claimedPath,
                    out storageException);
            }

            /// <summary>
            /// 删除 Unity processing marker。
            /// </summary>
            public void RemoveExpiredMarkers(DateTime cutoffUtc)
            {
                YokiFrameFileBridgeClaim.RemoveExpiredMarkers(
                    YokiFrameEditorFileBridgePaths.GetProcessingRoot(),
                    cutoffUtc);
            }

            /// <summary>
            /// 获取 Unity processing 文件最后写入时间。
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
            /// 判断 Unity processing 命令是否已经存在对应 terminal response。
            /// </summary>
            /// <param name="commandPath">processing 命令路径。</param>
            /// <returns>response 已存在时返回 true。</returns>
            public bool HasTerminalResponse(string commandPath)
            {
                try
                {
                    return File.Exists(YokiFrameEditorFileBridgePaths.GetResponsePath(
                        Path.GetFileNameWithoutExtension(commandPath)));
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            /// <summary>
            /// 原子写入 Unity terminal response JSON。
            /// </summary>
            public void WriteResponse(string requestId, string responseJson)
            {
                YokiFrameEditorFileBridgeJson.WriteAtomic(
                    YokiFrameEditorFileBridgePaths.GetResponsePath(requestId),
                    responseJson);
            }

            /// <summary>
            /// 归档 Unity 已完成命令。
            /// </summary>
            public void Archive(string commandPath)
            {
                ArchiveCommand(commandPath);
            }

            /// <summary>
            /// 将 Unity 失败命令写入 deadletter。
            /// </summary>
            public void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
            {
                YokiFrameEditorFileBridgePump.MoveToDeadletter(commandPath, errorCode, errorMessage);
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
                YokiFrameEditorFileBridgePump.WriteProcessingFailureEvidence(
                    commandPath,
                    errorCode,
                    errorMessage);
            }

            /// <summary>
            /// Unity 由外层定时器负责清理，不在每个命令批次重复清理。
            /// </summary>
            public void PruneAfterBatch()
            {
            }

            /// <summary>
            /// Unity 缺少 commands 根目录时不改变原有清理策略。
            /// </summary>
            public void PruneWhenPendingRootMissing()
            {
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
            if (!string.Equals(
                    Path.GetFileNameWithoutExtension(commandPath),
                    envelope.requestId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Command file name does not match envelope requestId.");
            }

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

            if (!YokiFrameEditorFileBridgeJson.IsSafeId(envelope.source)
                || !YokiFrameEditorFileBridgeJson.IsSafeId(envelope.requestId)
                || !YokiFrameEditorFileBridgeJson.IsSafeId(envelope.kit)
                || !YokiFrameEditorFileBridgeJson.IsSafeId(envelope.action))
            {
                throw new InvalidDataException("Command envelope contains unsafe requestId, kit or action.");
            }

            if (envelope.timeoutMs < YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MIN_MS
                || envelope.timeoutMs > YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MAX_MS
                || !DateTimeOffset.TryParse(
                    envelope.createdAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                throw new InvalidDataException("Command envelope timeoutMs or createdAtUtc is invalid.");
            }

            try
            {
                JsonHelper.EnsureValidJson(envelope.payloadJson);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Command envelope payloadJson is invalid.", exception);
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
                backpressureActive = sCommandCoordinator != null && sCommandCoordinator.LastBatchWasLimited,
                lastPollLimitReason = sCommandCoordinator == null
                    ? string.Empty
                    : sCommandCoordinator.LastBatchLimitReason,
                bridgeBusyCount = 0,
                lastError = CreateBridgeLastError()
            };
        }

        /// <summary>
        /// 汇总当前会话最近一次 FastChannel 故障原因；无故障时返回空字符串。
        /// </summary>
        /// <returns>启动失败原因优先，其次为 listener 记录的最近错误。</returns>
        private static string CreateBridgeLastError()
        {
            if (!string.IsNullOrEmpty(sCommandProcessingError))
            {
                return sCommandProcessingError;
            }

            if (!string.IsNullOrEmpty(sFastChannelStartError))
            {
                return sFastChannelStartError;
            }

#if UNITY_EDITOR_WIN
            // sFastChannelHost 仅在 Windows Editor 下声明，宿主为普通 C# 对象故显式判空。
            var host = sFastChannelHost;
            if (host != null && !string.IsNullOrEmpty(host.LastError))
            {
                return host.LastError;
            }
#endif
            return string.Empty;
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
