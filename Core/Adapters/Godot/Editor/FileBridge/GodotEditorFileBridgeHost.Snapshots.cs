#if GODOT && TOOLS
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Godot Editor 宿主的 Kit state 快照发布：把 catalog 注册 Provider 的状态写成
    /// <c>snapshots/&lt;kit&gt;/&lt;name&gt;.json</c>，使 Workbench / CLI 的 snapshot read 在 godot-editor 上同样可用。
    /// </summary>
    /// <remarks>
    /// 快照信封字段与 Godot Runtime 宿主保持一致（engineId / kit / name / generation / sequence / writtenAtUtc / payloadJson），
    /// 但类型各自持有：两侧是不同程序集，契约是 JSON 形状而不是 CLR 类型。
    /// 版本化 Provider 只在 StateVersion 变化时落盘；未声明版本的 Provider 跟随 catalog revision 写一次。
    /// </remarks>
    public sealed partial class GodotEditorFileBridgeHost
    {
        private sealed class EditorStateSnapshot
        {
            public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
            public string EngineId { get; set; } = ENGINE_ID;
            public string Kit { get; set; } = string.Empty;
            public string Name { get; set; } = "state";
            public long Generation { get; set; }
            public long Sequence { get; set; }
            public string WrittenAtUtc { get; set; } = string.Empty;
            public string PayloadJson { get; set; } = "{}";
        }

        private readonly YokiFrameKitStateVersionTracker mStateVersions = new YokiFrameKitStateVersionTracker();
        private long mSnapshotsPublishedRevision = -1;

        /// <summary>
        /// 发布发生变化或尚未发布的 Kit 快照；版本未变时不重写文件。
        /// </summary>
        private void PublishChangedKitSnapshots()
        {
            IReadOnlyList<IYokiFrameKitInteractionProvider> providers = mKitInteractions == null
                ? Array.Empty<IYokiFrameKitInteractionProvider>()
                : mKitInteractions.Providers;
            bool revisionChanged = mSnapshotsPublishedRevision != mToolProviderRevision;
            for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                IYokiFrameKitInteractionProvider provider = providers[providerIndex];
                IReadOnlyList<string> snapshotNames = provider.SnapshotNames;
                for (var snapshotIndex = 0; snapshotIndex < snapshotNames.Count; snapshotIndex++)
                {
                    string snapshotName = snapshotNames[snapshotIndex];
                    if (provider is IYokiFrameSnapshotVersionedKitInteractionProvider versioned)
                    {
                        // 编辑器没有共享内存遥测，因此 telemetryAvailable 恒为 false：版本化 Provider 仍需落盘快照。
                        if (!mStateVersions.ShouldWriteSnapshot(versioned, telemetryAvailable: false))
                        {
                            continue;
                        }

                        WriteSnapshot(provider.Kit, snapshotName, provider.CreateSnapshot(snapshotName));
                        mStateVersions.RememberSnapshotVersion(versioned);
                        continue;
                    }

                    if (revisionChanged)
                    {
                        WriteSnapshot(provider.Kit, snapshotName, provider.CreateSnapshot(snapshotName));
                    }
                }
            }

            mSnapshotsPublishedRevision = mToolProviderRevision;
        }

        /// <summary>写入单个快照文件。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="snapshotName">快照名称。</param>
        /// <param name="payloadJson">Provider 产出的 payload。</param>
        private void WriteSnapshot(string kit, string snapshotName, string payloadJson)
        {
            var snapshot = new EditorStateSnapshot
            {
                EngineId = ENGINE_ID,
                Kit = kit,
                Name = snapshotName,
                Generation = mGeneration,
                Sequence = ++mSequence,
                WrittenAtUtc = DateTimeOffset.UtcNow.ToString("o"),
                PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson
            };
            GodotFileBridgeJson.WriteAtomic(mPaths.GetSnapshotPath(kit, snapshotName), GodotFileBridgeJson.Serialize(snapshot));
        }
    }
}
#endif
