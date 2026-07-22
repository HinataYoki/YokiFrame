#if GODOT && TOOLS
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>承载 Godot Host 的版本化与命名 Kit Telemetry 发布。</summary>
    public sealed partial class GodotFileBridgeHost
    {
        private readonly Dictionary<string, long> mKitTelemetryVersions =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> mKitSnapshotVersions =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, long>> mNamedTelemetryVersions =
            new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);

        /// <summary>
        /// 每帧只检查轻量版本号；领域状态变化时立即发布 Shared Memory，不写任何 FileBridge 文件。
        /// </summary>
        public void RefreshChangedTelemetry()
        {
            RefreshToolKitInteractions();
            if (!IsRunning || !mTelemetryAvailable)
            {
                return;
            }

            var providers = mKitInteractions.Providers;
            for (var index = 0; index < providers.Count; index++)
            {
                var versioned = providers[index] as IYokiFrameVersionedKitInteractionProvider;
                if (versioned == null || !HasTelemetryVersionChanged(versioned))
                {
                    continue;
                }

                try
                {
                    mSequence++;
                    PublishTelemetryState(versioned.Kit, versioned.CreateSnapshot("state"));
                    PublishNamedTelemetry(versioned);
                    mKitTelemetryVersions[versioned.Kit] = versioned.StateVersion;
                }
                catch (Exception exception)
                {
                    mLastError = "Versioned telemetry refresh failed for "
                        + versioned.Kit + ": " + exception.Message;
                }
            }
        }

        /// <summary>发布 Provider 当前声明的命名 latest frame，并释放已注销实例的映射。</summary>
        /// <param name="provider">当前 Kit Interaction Provider。</param>
        private void PublishNamedTelemetry(IYokiFrameKitInteractionProvider provider)
        {
            var namedProvider = provider as IYokiFrameNamedTelemetryProvider;
            var writer = mTelemetryWriter;
            if (namedProvider == null || writer == null)
            {
                return;
            }

            IReadOnlyList<string> names = namedProvider.TelemetryNames;
            var versionedProvider = namedProvider as IYokiFrameVersionedNamedTelemetryProvider;
            Dictionary<string, long> publishedVersions = versionedProvider == null
                ? null
                : GetOrCreateNamedTelemetryVersions(provider.Kit);
            for (var index = 0; index < names.Count; index++)
            {
                PublishNamedTelemetryFrameSafely(
                    namedProvider,
                    versionedProvider,
                    publishedVersions,
                    names[index]);
            }

            writer.RetainNamedStates(provider.Kit, names);
            RetainNamedTelemetryVersions(provider.Kit, names);
        }

        /// <summary>隔离单个命名 payload 创建失败，避免一个已注销实例阻断其它 latest frame。</summary>
        /// <param name="provider">命名 Telemetry Provider。</param>
        /// <param name="name">本轮声明的安全名称。</param>
        private void PublishNamedTelemetryFrameSafely(
            IYokiFrameNamedTelemetryProvider provider,
            IYokiFrameVersionedNamedTelemetryProvider versionedProvider,
            Dictionary<string, long> publishedVersions,
            string name)
        {
            try
            {
                long version = versionedProvider == null
                    ? 0L
                    : versionedProvider.GetTelemetryVersion(name);
                if (versionedProvider != null
                    && publishedVersions.TryGetValue(name, out var publishedVersion)
                    && publishedVersion == version)
                {
                    return;
                }

                PublishTelemetryState(provider.Kit, name, provider.CreateTelemetry(name));
                if (versionedProvider != null)
                {
                    publishedVersions[name] = version;
                }
            }
            catch (Exception exception)
            {
                mLastError = "Named telemetry payload failed for "
                    + provider.Kit + "/" + name + ": " + exception.Message;
            }
        }

        /// <summary>释放已经不活动实例的版本记录，保持缓存与 writer 当前命名映射一致。</summary>
        /// <param name="kit">命名 Telemetry 所属 Kit。</param>
        /// <param name="activeNames">当前仍活动的安全名称。</param>
        private void RetainNamedTelemetryVersions(string kit, IReadOnlyList<string> activeNames)
        {
            if (!mNamedTelemetryVersions.TryGetValue(kit, out var publishedVersions)
                || publishedVersions.Count == activeNames.Count)
            {
                return;
            }

            List<string> staleKeys = new List<string>();
            foreach (var name in publishedVersions.Keys)
            {
                if (!ContainsTelemetryName(activeNames, name))
                {
                    staleKeys.Add(name);
                }
            }

            for (var index = 0; index < staleKeys.Count; index++)
            {
                publishedVersions.Remove(staleKeys[index]);
            }
        }

        /// <summary>判断 Provider 当前名称集合是否仍包含指定实例。</summary>
        /// <param name="activeNames">当前活动名称集合。</param>
        /// <param name="name">待匹配实例名称。</param>
        /// <returns>仍活动时返回 true。</returns>
        private static bool ContainsTelemetryName(IReadOnlyList<string> activeNames, string name)
        {
            for (var index = 0; index < activeNames.Count; index++)
            {
                if (string.Equals(activeNames[index], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>获取指定 Kit 的实例版本表，并在首次发布时创建。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>仅以 Provider 安全名称为键的实例版本表。</returns>
        private Dictionary<string, long> GetOrCreateNamedTelemetryVersions(string kit)
        {
            if (!mNamedTelemetryVersions.TryGetValue(kit, out var versions))
            {
                versions = new Dictionary<string, long>(StringComparer.Ordinal);
                mNamedTelemetryVersions.Add(kit, versions);
            }

            return versions;
        }

        /// <summary>清空实例版本缓存，使新 session/generation 强制重新发布全部命名帧。</summary>
        private void ClearNamedTelemetryVersions()
        {
            mNamedTelemetryVersions.Clear();
        }

        /// <summary>记录一次完整 state 发布后的 Provider 版本。</summary>
        /// <param name="provider">刚完成发布的 Provider。</param>
        private void RememberTelemetryVersion(IYokiFrameKitInteractionProvider provider)
        {
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            if (versioned != null)
            {
                mKitTelemetryVersions[provider.Kit] = versioned.StateVersion;
            }
        }

        /// <summary>记录完整 FileBridge snapshot 对应的版本，避免增量发布重复落盘。</summary>
        /// <param name="provider">刚完成完整状态发布的 Provider。</param>
        private void RememberSnapshotVersion(IYokiFrameKitInteractionProvider provider)
        {
            var versioned = provider as IYokiFrameSnapshotVersionedKitInteractionProvider;
            if (versioned != null)
            {
                mKitSnapshotVersions[provider.Kit] = versioned.StateVersion;
            }
        }

        /// <summary>只写发生变化且需要 FileBridge 传输的 Provider state，并让调用方同步 registry。</summary>
        /// <returns>至少写入一个增量 snapshot 时返回 true。</returns>
        private bool RefreshChangedSnapshots()
        {
            var wroteSnapshot = false;
            var providers = mKitInteractions.Providers;
            for (var index = 0; index < providers.Count; index++)
            {
                var versioned = providers[index] as IYokiFrameSnapshotVersionedKitInteractionProvider;
                if (!ShouldWriteSnapshot(versioned))
                {
                    continue;
                }

                WriteSnapshot(
                    versioned.Kit,
                    "state",
                    versioned.CreateSnapshot("state"),
                    versioned is IYokiFrameVersionedKitInteractionProvider);
                mKitSnapshotVersions[versioned.Kit] = versioned.StateVersion;
                wroteSnapshot = true;
            }

            return wroteSnapshot;
        }

        /// <summary>判断 Provider 是否需要文件快照，并让 Telemetry Provider 保持原有回落策略。</summary>
        /// <param name="provider">待检查的 Snapshot 版本化 Provider。</param>
        /// <returns>需要写入当前 FileBridge state 时返回 true。</returns>
        private bool ShouldWriteSnapshot(IYokiFrameSnapshotVersionedKitInteractionProvider provider)
        {
            if (provider == null
                || (mKitSnapshotVersions.TryGetValue(provider.Kit, out var publishedVersion)
                    && publishedVersion == provider.StateVersion))
            {
                return false;
            }

            return !(provider is IYokiFrameVersionedKitInteractionProvider) || !mTelemetryAvailable;
        }

        /// <summary>判断版本化 Provider 是否自上次发布后发生变化。</summary>
        /// <param name="provider">待检查 Provider。</param>
        /// <returns>首次发布或版本不同返回 true。</returns>
        private bool HasTelemetryVersionChanged(IYokiFrameVersionedKitInteractionProvider provider)
        {
            return !mKitTelemetryVersions.TryGetValue(provider.Kit, out var publishedVersion)
                || publishedVersion != provider.StateVersion;
        }
    }
}
#endif
