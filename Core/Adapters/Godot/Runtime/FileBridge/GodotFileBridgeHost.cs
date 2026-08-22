#if GODOT && TOOLS
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 驱动 Godot Runtime 的 FileBridge 注册、心跳、snapshot、命令终态和会话释放。
    /// </summary>
    public sealed partial class GodotFileBridgeHost : IDisposable
    {
        /// <summary>
        /// Godot Runtime 在共享 FileBridge registry 中的稳定 engine ID。
        /// </summary>
        public const string ENGINE_ID = "godot-runtime";

        private const string RUNTIME_MODE = "Runtime";
        private static readonly string[] sHostStateKits = { "System" };

        private readonly string mEngineVersion;
        private YokiFrameKitInteractionRegistry mKitInteractions;
        private readonly string mProjectScopeId;
        private string[] mStateKits;
        private readonly GodotFileBridgePaths mPaths;
        private YokiFrameHostAdmissionLease mAdmissionLease;
        private YokiFrameCommandDispatcher mDispatcher;
        private readonly YokiFrameHostCommandCoordinator mCommandCoordinator;
        private string mLastError = string.Empty;
        private string mSessionId = string.Empty;
        private string mStartedAtUtc = string.Empty;
        private long mGeneration;
        private long mSequence;
        private long mToolProviderRevision;
        private DateTime mNextStorageCleanupUtc;

        /// <summary>
        /// 创建只依赖项目路径、Godot 版本和纯 Core dispatcher 的可测试 Runtime Host。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根目录。</param>
        /// <param name="engineVersion">当前 Godot 版本文本。</param>
        public GodotFileBridgeHost(string projectRoot, string engineVersion)
        {
            if (string.IsNullOrWhiteSpace(engineVersion))
            {
                throw new ArgumentException("Godot engine version is required.", nameof(engineVersion));
            }

            mEngineVersion = engineVersion;
            mKitInteractions = YokiFrameCoreKitInteractions.CreateDefault(out mToolProviderRevision);
            mStateKits = CreateStateKitNames(mKitInteractions);
            mPaths = new GodotFileBridgePaths(projectRoot);
            mProjectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(mPaths.ProjectRoot);
            mDispatcher = CreateCommandDispatcher();
            // 共享命令存储承载三宿主一致的枚举、认领、终态与 deadletter 移动逻辑；
            // Runtime 宿主保持 OrdinalIgnoreCase 稳定排序与五分钟节流清理语义。
            mCommandCoordinator = new YokiFrameHostCommandCoordinator(
                new YokiFrameFileBridgeHostStore(
                    mPaths,
                    (path, json) => GodotFileBridgeJson.WriteAtomic(path, json),
                    SerializeDeadletterInfo,
                    () => TryPruneStorage(),
                    () => TryPruneStorage(),
                    true),
                ExecuteCommandForCoordinator,
                PROCESSING_LEASE,
                exception => mLastError = exception.Message);
        }

        /// <summary>
        /// 获取当前会话是否已经启动且允许发布状态或消费命令。
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 获取当前启动会话的安全标识；停止后保留最后值用于诊断。
        /// </summary>
        public string SessionId => mSessionId;

        /// <summary>
        /// 获取当前启动代际；同一 Host 对象每次重启都严格递增。
        /// </summary>
        public long Generation => mGeneration;

        /// <summary>
        /// 获取当前状态发布序号；每个新 generation 从 1 开始。
        /// </summary>
        public long Sequence => mSequence;

        /// <summary>
        /// 记录 Runtime 帧阶段异常，供 bridge_status 暴露最近一次通信故障。
        /// </summary>
        /// <param name="exception">当前阶段捕获的异常。</param>
        internal void RecordRuntimeError(Exception exception)
        {
            mLastError = exception == null ? "Unknown Runtime FileBridge error." : exception.Message;
        }

        /// <summary>
        /// 创建新 session/generation，并立即发布首帧 heartbeat、四个 snapshot 和当前 capability 对应的 registry。
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            RefreshToolKitInteractions();
            var now = DateTimeOffset.UtcNow;
            mSessionId = Guid.NewGuid().ToString("N");
            mGeneration = CreateNextGeneration(now.UtcDateTime.Ticks);
            mSequence = 0;
            mLastError = string.Empty;
            mStartedAtUtc = now.ToString("O");
            try
            {
                var admissionResult = YokiFrameHostAdmissionLease.TryAcquire(
                    mPaths.AdmissionLockPath,
                    out mAdmissionLease,
                    out var admissionError);
                if (admissionResult == YokiFrameHostAdmissionResult.AlreadyOwned)
                {
                    throw new YokiFrameHostAlreadyOwnedException(ENGINE_ID);
                }

                if (admissionResult == YokiFrameHostAdmissionResult.StorageError)
                {
                    throw admissionError ?? new IOException("Godot Runtime Host admission failed.");
                }

                IsRunning = true;
                mPaths.EnsureDirectories();
                TryPruneStorage();
                InitializeTelemetry();
                StartFastChannel();
                RefreshState();
            }
            catch
            {
                IsRunning = false;
                StopFastChannel();
                DisposeTelemetry();
                ReleaseActiveState();
                mAdmissionLease?.Dispose();
                mAdmissionLease = null;
                throw;
            }
        }

        /// <summary>
        /// 递增 sequence，并依次原子刷新 heartbeat、四个 Kit state snapshot 与当前 capability 的 registry。
        /// </summary>
        public void RefreshState()
        {
            EnsureRunning();
            mSequence++;
            mPaths.EnsureDirectories();
            WriteHeartbeat();
            for (var index = 0; index < sHostStateKits.Length; index++)
            {
                WriteSnapshot(sHostStateKits[index]);
            }

            WriteKitInteractionSnapshots();

            WriteEngineRegistry();
        }

        /// <summary>
        /// 只更新在线心跳；按 Provider 能力提交发生变化的 FileBridge Snapshot。
        /// </summary>
        public void RefreshHeartbeat()
        {
            EnsureRunning();
            mSequence++;
            mPaths.EnsureDirectories();
            WriteHeartbeat();
            RefreshChangedSnapshots();
            // Registry 同时承载 FastChannel listener 健康状态；即使 Snapshot 未变化，也必须
            // 在每次低频 heartbeat 重新发布 disabled/enabled endpoint，避免陈旧连接声明。
            WriteEngineRegistry();
        }

        /// <summary>
        /// 停止当前会话并删除活动 registry/heartbeat，保留 snapshot、result 和 deadletter 证据。
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            StopFastChannel();
            DisposeTelemetry();
            ReleaseActiveState();
            mAdmissionLease?.Dispose();
            mAdmissionLease = null;
        }

        /// <summary>
        /// 释放 Host，语义等同于幂等 Stop。
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 创建严格大于上一 generation 的新代际，避免同一 tick 内重启复用旧值。
        /// </summary>
        /// <param name="utcTicks">当前 UTC ticks。</param>
        /// <returns>单调递增 generation。</returns>
        private long CreateNextGeneration(long utcTicks)
        {
            return utcTicks > mGeneration ? utcTicks : mGeneration + 1;
        }

        /// <summary>
        /// 写入 Godot Runtime engine registry，只声明当前实际可用的 telemetry capability。
        /// </summary>
        private void WriteEngineRegistry()
        {
            GodotEngineRegistry registry = new GodotEngineRegistry
            {
                Version = mEngineVersion,
                ProjectPath = mPaths.ProjectRoot.Replace('\\', '/'),
                SessionId = mSessionId,
                Generation = mGeneration,
                Mode = RUNTIME_MODE,
                StartedAtUtc = mStartedAtUtc,
                RegisteredAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                Capabilities = GetCapabilities(),
                FastChannels = GetFastChannelEndpoints()
            };
            GodotFileBridgeJson.WriteAtomic(mPaths.RegistryPath, GodotFileBridgeJson.Serialize(registry));
        }

        /// <summary>
        /// 写入与当前 session、generation 和 sequence 对齐的 heartbeat。
        /// </summary>
        private void WriteHeartbeat()
        {
            var nowUtc = DateTimeOffset.UtcNow.ToString("O");
            GodotHeartbeat heartbeat = new GodotHeartbeat
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Mode = RUNTIME_MODE,
                Sequence = mSequence,
                CreatedAtUtc = nowUtc,
                WrittenAtUtc = nowUtc
            };
            GodotFileBridgeJson.WriteAtomic(mPaths.HeartbeatPath, GodotFileBridgeJson.Serialize(heartbeat));
        }

        /// <summary>
        /// 写入指定遗留 Kit 的 state snapshot；已迁移 Kit 由 Registry 的统一发布路径处理。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        private void WriteSnapshot(string kit)
        {
            var payloadJson = CreateSnapshotPayloadJson(kit);
            WriteSnapshot(kit, "state", payloadJson);
        }

        /// <summary>写入全部已注册 Kit Provider 当前声明的 Snapshot。</summary>
        private void WriteKitInteractionSnapshots()
        {
            var providers = mKitInteractions.Providers;
            for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                var provider = providers[providerIndex];
                for (var snapshotIndex = 0; snapshotIndex < provider.SnapshotNames.Count; snapshotIndex++)
                {
                    var snapshotName = provider.SnapshotNames[snapshotIndex];
                    bool publishTelemetry = provider is IYokiFrameVersionedKitInteractionProvider;
                    WriteSnapshot(
                        provider.Kit,
                        snapshotName,
                        provider.CreateSnapshot(snapshotName),
                        publishTelemetry);
                }

                PublishNamedTelemetry(provider);
                RememberPublishedStateVersions(provider);
            }
        }

        /// <summary>把 Kit payload 包装为 Godot Snapshot 信封，并仅为版本化 Provider 同步 state telemetry。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="snapshotName">Snapshot 名称。</param>
        /// <param name="payloadJson">Kit 自有 schema payload。</param>
        private void WriteSnapshot(string kit, string snapshotName, string payloadJson)
        {
            WriteSnapshot(kit, snapshotName, payloadJson, publishTelemetry: true);
        }

        /// <summary>写入一个 Snapshot，并按 Provider 能力决定是否同步 Shared Memory。</summary>
        private void WriteSnapshot(
            string kit,
            string snapshotName,
            string payloadJson,
            bool publishTelemetry)
        {
            GodotStateSnapshot snapshot = new GodotStateSnapshot
            {
                Kit = kit,
                Generation = mGeneration,
                Sequence = mSequence,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                PayloadJson = payloadJson
            };
            GodotFileBridgeJson.WriteAtomic(mPaths.GetSnapshotPath(kit, snapshotName), GodotFileBridgeJson.Serialize(snapshot));
            if (publishTelemetry && snapshotName == "state")
            {
                PublishTelemetryState(kit, snapshot.PayloadJson);
            }
        }

        /// <summary>
        /// 为已迁移 Kit 读取 Registry Snapshot，其它遗留 Kit 保留与当前会话绑定的宿主状态。
        /// </summary>
        /// <param name="kit">目标 Kit 标识。</param>
        /// <returns>可直接写入 snapshot 与 telemetry 的 JSON 根对象文本。</returns>
        private string CreateSnapshotPayloadJson(string kit)
        {
            if (mKitInteractions.TryCreateSnapshot(kit, "state", out var payloadJson))
            {
                return payloadJson;
            }

            GodotStatePayload payload = new GodotStatePayload
            {
                Kit = kit,
                SessionId = mSessionId,
                Generation = mGeneration,
                Sequence = mSequence,
                Mode = RUNTIME_MODE,
                FastChannel = "filebridge-fallback"
            };
            return GodotFileBridgeJson.Serialize(payload);
        }

        /// <summary>创建 Shared Memory state 通道需要的合并 Kit 清单。</summary>
        /// <param name="registry">当前 Runtime Registry。</param>
        /// <returns>遗留状态与已迁移 Provider 的 Kit 名称。</returns>
        private static string[] CreateStateKitNames(YokiFrameKitInteractionRegistry registry)
        {
            var providers = registry.Providers;
            string[] names = new string[sHostStateKits.Length + providers.Count];
            Array.Copy(sHostStateKits, names, sHostStateKits.Length);
            for (var index = 0; index < providers.Count; index++)
            {
                names[sHostStateKits.Length + index] = providers[index].Kit;
            }

            return names;
        }

        /// <summary>
        /// 在 Tool Provider 集合变化时重建 Registry、命令策略和 Telemetry 通道。
        /// </summary>
        private void RefreshToolKitInteractions()
        {
            long revision = YokiFrameToolKitInteractionCatalog.Revision;
            if (revision == mToolProviderRevision)
            {
                return;
            }

            YokiFrameKitInteractionRegistry interactions =
                YokiFrameCoreKitInteractions.CreateDefault(out long capturedRevision);
            mKitInteractions = interactions;
            mStateKits = CreateStateKitNames(mKitInteractions);
            mDispatcher = CreateCommandDispatcher();
            mStateVersions.Clear();
            mToolProviderRevision = capturedRevision;
            if (IsRunning)
            {
                InitializeTelemetry();
                RefreshState();
            }
        }

        /// <summary>
        /// 删除当前活动 registry 和 heartbeat，使工具不会把已退出进程当作在线 engine。
        /// </summary>
        private void ReleaseActiveState()
        {
            DeleteIfOwned(mPaths.RegistryPath, IsOwnedRegistry);
            DeleteIfOwned(mPaths.HeartbeatPath, IsOwnedHeartbeat);
        }

        /// <summary>
        /// 只删除仍属于当前 session/generation 的活动状态文件，避免停止旧 Host 时误删新 Host 状态。
        /// </summary>
        /// <param name="path">待删除文件路径。</param>
        /// <param name="isOwned">读取并校验当前文件 owner 的函数。</param>
        private void DeleteIfOwned(string path, Func<string, bool> isOwned)
        {
            if (File.Exists(path) && isOwned(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>判断 Runtime registry 是否仍属于当前 Host。</summary>
        /// <param name="path">registry 文件路径。</param>
        /// <returns>当前文件仍属于本 Host 时返回 true。</returns>
        private bool IsOwnedRegistry(string path)
        {
            try
            {
                var registry = GodotFileBridgeJson.Deserialize<GodotEngineRegistry>(File.ReadAllText(path));
                return registry != null
                    && registry.SessionId == mSessionId
                    && registry.Generation == mGeneration;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>判断 Runtime heartbeat 是否仍属于当前 Host。</summary>
        /// <param name="path">heartbeat 文件路径。</param>
        /// <returns>当前文件仍属于本 Host 时返回 true。</returns>
        private bool IsOwnedHeartbeat(string path)
        {
            try
            {
                var heartbeat = GodotFileBridgeJson.Deserialize<GodotHeartbeat>(File.ReadAllText(path));
                return heartbeat != null
                    && heartbeat.SessionId == mSessionId
                    && heartbeat.Generation == mGeneration;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 拒绝在未启动或已停止会话上发布状态、消费命令。
        /// </summary>
        private void EnsureRunning()
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Godot FileBridge Host is not running.");
            }
        }
    }
}
#endif
