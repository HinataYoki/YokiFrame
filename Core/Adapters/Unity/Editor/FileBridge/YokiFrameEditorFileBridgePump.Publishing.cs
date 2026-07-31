#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>承载 Unity Editor FileBridge 的按需文件发布与 Shared Memory 回落策略。</summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
        /// <summary>
        /// 回收已完成的协议证据；清理失败只记录警告，不影响 Unity Editor 主循环。
        /// </summary>
        private static void TryPruneProjectStorage()
        {
            try
            {
                YokiFrameFileBridgePruner.Prune(YokiFrameEditorFileBridgePaths.GetProjectRoot());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame storage cleanup failed: " + exception.Message);
            }
        }

        /// <summary>捕获完整状态写入异常，避免辅助泵打断 Editor update。</summary>
        private static void WriteCompleteBridgeStateSafely()
        {
            try
            {
                WriteCompleteBridgeState();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame FileBridge state write failed: " + exception.Message);
            }
        }

        /// <summary>只更新在线心跳；周期保活不重写 registry 或 snapshot。</summary>
        private static void WriteHeartbeatStateSafely()
        {
            try
            {
                WriteHeartbeatState();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame FileBridge heartbeat write failed: " + exception.Message);
            }
        }

        /// <summary>写入当前 session 的 registry、heartbeat 和完整初始 snapshot。</summary>
        private static void WriteCompleteBridgeState()
        {
            sSequence++;
            EnsureBridgeDirectories();
            WriteEngineRegistry();
            WriteHeartbeat();
            foreach (var kit in sHostStateKits)
            {
                WriteSnapshot(kit);
            }

            WriteKitInteractionSnapshots();
        }

        /// <summary>推进保活序号，并只为发生变化的版本化 Provider 写入 Snapshot。</summary>
        private static void WriteHeartbeatState()
        {
            sSequence++;
            EnsureBridgeDirectories();
            WriteHeartbeat();
            WriteChangedSnapshots();
        }

        /// <summary>创建 FileBridge 所需目录，保证 CLI 可以直接读取状态和队列。</summary>
        private static void EnsureBridgeDirectories()
        {
            // 心跳与完整状态写入都经过此处，故每轮落盘前复核一次固定根的重解析点防护。
            YokiFrameEditorFileBridgePaths.EnsureBridgeRootsAreSafe();
            Directory.CreateDirectory(YokiFrameEditorFileBridgePaths.GetCommandsRoot());
            Directory.CreateDirectory(YokiFrameEditorFileBridgePaths.GetArchiveRoot());
            Directory.CreateDirectory(YokiFrameEditorFileBridgePaths.GetDeadletterRoot());
            Directory.CreateDirectory(YokiFrameEditorFileBridgePaths.GetResultsRoot());
            Directory.CreateDirectory(Path.GetDirectoryName(YokiFrameEditorFileBridgePaths.GetHeartbeatPath()));
        }

        /// <summary>写入 Unity Editor engine registry，供 `engine list` 读取。</summary>
        private static void WriteEngineRegistry()
        {
            var registry = new YokiFrameEditorEngineRegistry
            {
                version = Application.unityVersion,
                projectPath = YokiFrameEditorFileBridgePaths.GetProjectRoot(),
                sessionId = sSessionId,
                generation = sGeneration,
                mode = GetEditorMode(),
                startedAtUtc = sStartedAtUtc,
                registeredAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                capabilities = CreateBridgeCapabilities(),
                fastChannels = CreateFastChannelEndpoints()
            };
            YokiFrameEditorFileBridgeJson.WriteAtomic(
                YokiFrameEditorFileBridgePaths.GetEngineRegistryPath(),
                YokiFrameEditorFileBridgeJson.ToJson(registry));
        }

        /// <summary>写入 heartbeat，供 CLI 判断 Unity Editor FileBridge 是否 stale。</summary>
        private static void WriteHeartbeat()
        {
            var nowUtc = DateTimeOffset.UtcNow.ToString("O");
            var heartbeat = new YokiFrameEditorHeartbeat
            {
                sessionId = sSessionId,
                generation = sGeneration,
                mode = GetEditorMode(),
                sequence = sSequence,
                createdAtUtc = nowUtc,
                writtenAtUtc = nowUtc
            };
            YokiFrameEditorFileBridgeJson.WriteAtomic(
                YokiFrameEditorFileBridgePaths.GetHeartbeatPath(),
                YokiFrameEditorFileBridgeJson.ToJson(heartbeat));
        }

        /// <summary>写入指定 Kit 的 state snapshot。</summary>
        /// <param name="kit">Kit 标识。</param>
        private static void WriteSnapshot(string kit)
        {
            var payloadJson = CreateStatePayloadJson(kit);
            WriteSnapshot(kit, "state", payloadJson);
        }

        /// <summary>写入全部 Provider 声明的初始 Snapshot，不对具体 Kit 做宿主特判。</summary>
        private static void WriteKitInteractionSnapshots()
        {
            var providers = sKitInteractions.Providers;
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
                    RememberVersionedTelemetryState(provider, snapshotName);
                    RememberVersionedSnapshotState(provider, snapshotName);
                }

                WriteNamedTelemetry(provider);
            }
        }

        /// <summary>捕获版本化 Kit 的高速 Telemetry 刷新异常。</summary>
        private static void WriteChangedKitInteractionTelemetrySafely()
        {
            try
            {
                WriteChangedKitInteractionTelemetry();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame realtime Kit telemetry write failed: " + exception.Message);
            }
        }

        /// <summary>只为版本变化的 Kit 创建 state payload，并立即写入 Shared Memory。</summary>
        private static void WriteChangedKitInteractionTelemetry()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var providers = sKitInteractions.Providers;
            for (var index = 0; index < providers.Count; index++)
            {
                var versioned = providers[index] as IYokiFrameVersionedKitInteractionProvider;
                if (versioned == null || !HasVersionChanged(versioned))
                {
                    continue;
                }

                if (sKitInteractions.TryCreateSnapshot(versioned.Kit, "state", out var payloadJson))
                {
                    sSequence++;
                    PublishChangedTelemetry(versioned, payloadJson);
                }
            }
        }

        /// <summary>发布一个变化 Kit，并在首次内存失败时写一次 snapshot 回落。</summary>
        private static void PublishChangedTelemetry(
            IYokiFrameVersionedKitInteractionProvider provider,
            string payloadJson)
        {
            if (WriteStateTelemetrySafely(provider.Kit, payloadJson))
            {
                sTelemetryFallbackKits.Remove(provider.Kit);
            }
            else if (sTelemetryFallbackKits.Add(provider.Kit))
            {
                WriteSnapshotFile(provider.Kit, "state", payloadJson);
                sKitSnapshotVersions[provider.Kit] = provider.StateVersion;
            }

            WriteNamedTelemetry(provider);
            sKitTelemetryVersions[provider.Kit] = provider.StateVersion;
        }

        /// <summary>判断版本化 Kit 是否需要发布新一帧 Telemetry。</summary>
        private static bool HasVersionChanged(IYokiFrameVersionedKitInteractionProvider provider)
        {
            return !sKitTelemetryVersions.TryGetValue(provider.Kit, out var publishedVersion)
                   || publishedVersion != provider.StateVersion;
        }

        /// <summary>记录完整 Snapshot 已同步到 Telemetry 的领域版本。</summary>
        private static void RememberVersionedTelemetryState(
            IYokiFrameKitInteractionProvider provider,
            string snapshotName)
        {
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            if (versioned != null && snapshotName == "state")
            {
                sKitTelemetryVersions[provider.Kit] = versioned.StateVersion;
            }
        }

        /// <summary>记录完整文件 snapshot 的领域版本，供后续增量落盘。</summary>
        private static void RememberVersionedSnapshotState(
            IYokiFrameKitInteractionProvider provider,
            string snapshotName)
        {
            var versioned = provider as IYokiFrameSnapshotVersionedKitInteractionProvider;
            if (versioned != null && snapshotName == "state")
            {
                sKitSnapshotVersions[provider.Kit] = versioned.StateVersion;
            }
        }

        /// <summary>只在领域状态变化时重写需要 FileBridge Snapshot 的 Provider。</summary>
        private static void WriteChangedSnapshots()
        {
            var providers = sKitInteractions.Providers;
            for (var index = 0; index < providers.Count; index++)
            {
                var versioned = providers[index] as IYokiFrameSnapshotVersionedKitInteractionProvider;
                if (!ShouldWriteSnapshot(versioned))
                {
                    continue;
                }

                WriteSnapshotFile(versioned.Kit, "state", versioned.CreateSnapshot("state"));
                sKitSnapshotVersions[versioned.Kit] = versioned.StateVersion;
            }
        }

        /// <summary>判断 Provider 是否需要写入新的文件帧，并让 Telemetry Provider 保持原有回落策略。</summary>
        private static bool ShouldWriteSnapshot(IYokiFrameSnapshotVersionedKitInteractionProvider provider)
        {
            if (provider == null
                || (sKitSnapshotVersions.TryGetValue(provider.Kit, out var publishedVersion)
                    && publishedVersion == provider.StateVersion))
            {
                return false;
            }

            return !(provider is IYokiFrameVersionedKitInteractionProvider)
                   || Application.platform != RuntimePlatform.WindowsEditor
                   || sTelemetryFallbackKits.Contains(provider.Kit);
        }

        /// <summary>把 Kit payload 包装为 Snapshot 信封，并仅为版本化 Provider 同步 state telemetry。</summary>
        private static void WriteSnapshot(string kit, string snapshotName, string payloadJson)
        {
            WriteSnapshot(kit, snapshotName, payloadJson, publishTelemetry: true);
        }

        /// <summary>写入一个 Snapshot，并按 Provider 能力决定是否同步 Shared Memory。</summary>
        private static void WriteSnapshot(
            string kit,
            string snapshotName,
            string payloadJson,
            bool publishTelemetry)
        {
            WriteSnapshotFile(kit, snapshotName, payloadJson);
            if (!publishTelemetry || snapshotName != "state")
            {
                return;
            }

            if (WriteStateTelemetrySafely(kit, payloadJson))
            {
                sTelemetryFallbackKits.Remove(kit);
            }
            else
            {
                sTelemetryFallbackKits.Add(kit);
            }
        }

        /// <summary>只提交 FileBridge snapshot 信封，不递归触发 Shared Memory 写入。</summary>
        private static void WriteSnapshotFile(string kit, string snapshotName, string payloadJson)
        {
            var snapshot = new YokiFrameEditorSnapshot
            {
                kit = kit,
                name = snapshotName,
                generation = sGeneration,
                sequence = sSequence,
                writtenAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                payloadJson = payloadJson
            };
            YokiFrameEditorFileBridgeJson.WriteAtomic(
                YokiFrameEditorFileBridgePaths.GetSnapshotPath(kit, snapshotName),
                YokiFrameEditorFileBridgeJson.ToJson(snapshot));
        }

        /// <summary>创建指定 Kit 的 state payload JSON。</summary>
        private static string CreateStatePayloadJson(string kit)
        {
            if (sKitInteractions.TryCreateSnapshot(kit, "state", out var payloadJson))
            {
                return payloadJson;
            }

            return YokiFrameEditorFileBridgeJson.ToJson(CreateStatePayload(kit));
        }

        /// <summary>写入 Kit/state telemetry；失败时由调用方决定是否提交文件回落。</summary>
        private static bool WriteStateTelemetrySafely(string kit, string payloadJson)
        {
            try
            {
                YokiFrameEditorTelemetryWriter.WriteState(kit, payloadJson, sGeneration, sSequence);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame telemetry write failed: " + exception.Message);
                return false;
            }
        }
    }
}

#endif
