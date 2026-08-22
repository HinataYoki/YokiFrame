#if GODOT && TOOLS
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>承载 Godot Host 的版本化与命名 Kit Telemetry 发布。</summary>
    public sealed partial class GodotFileBridgeHost
    {
        // 三宿主共享的 Kit 状态版本簿；失败回落策略仍由本宿主循环保留。
        private readonly YokiFrameKitStateVersionTracker mStateVersions =
            new YokiFrameKitStateVersionTracker();

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
                if (versioned == null || !mStateVersions.HasTelemetryVersionChanged(versioned))
                {
                    continue;
                }

                try
                {
                    mSequence++;
                    PublishTelemetryState(versioned.Kit, versioned.CreateSnapshot("state"));
                    PublishNamedTelemetry(versioned);
                    mStateVersions.RememberTelemetryVersion(versioned);
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
            Dictionary<string, long> publishedVersions =
                mStateVersions.GetOrCreateNamedVersions(provider.Kit, versionedProvider);
            for (var index = 0; index < names.Count; index++)
            {
                PublishNamedTelemetryFrameSafely(
                    namedProvider,
                    versionedProvider,
                    publishedVersions,
                    names[index]);
            }

            writer.RetainNamedStates(provider.Kit, names);
            mStateVersions.RetainNamedVersions(provider.Kit, names);
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
                mStateVersions.RememberNamedVersion(publishedVersions, name, version);
            }
            catch (Exception exception)
            {
                mLastError = "Named telemetry payload failed for "
                    + provider.Kit + "/" + name + ": " + exception.Message;
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
                if (!mStateVersions.ShouldWriteSnapshot(versioned, mTelemetryAvailable))
                {
                    continue;
                }

                WriteSnapshot(
                    versioned.Kit,
                    "state",
                    versioned.CreateSnapshot("state"),
                    versioned is IYokiFrameVersionedKitInteractionProvider);
                mStateVersions.RememberSnapshotVersion(versioned);
                wroteSnapshot = true;
            }

            return wroteSnapshot;
        }

        /// <summary>记录一次完整 state 发布后的 telemetry 与 snapshot 版本。</summary>
        /// <param name="provider">刚完成发布的 Provider。</param>
        internal void RememberPublishedStateVersions(IYokiFrameKitInteractionProvider provider)
        {
            var versionedForTelemetry = provider as IYokiFrameVersionedKitInteractionProvider;
            if (versionedForTelemetry != null)
            {
                mStateVersions.RememberTelemetryVersion(versionedForTelemetry);
            }

            var versionedForSnapshot = provider as IYokiFrameSnapshotVersionedKitInteractionProvider;
            if (versionedForSnapshot != null)
            {
                mStateVersions.RememberSnapshotVersion(versionedForSnapshot);
            }
        }

        /// <summary>清空命名版本缓存；宿主释放遥测 writer 时调用，保留 state 版本避免重复全量发布。</summary>
        private void ClearNamedTelemetryVersions()
        {
            mStateVersions.ClearNamedVersions();
        }
    }
}
#endif
