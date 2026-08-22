#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 集中管理三宿主重复的 Kit 状态版本簿：state telemetry 版本、state snapshot 版本与命名 telemetry 版本。
    /// 本类只做纯版本判定与记录，不执行任何 IO 或遥测写入；失败回落策略（Unity 按 Kit 回落快照、
    /// Godot 全局撤销 capability）仍由宿主循环保留。
    /// </summary>
    internal sealed class YokiFrameKitStateVersionTracker
    {
        private readonly Dictionary<string, long> mTelemetryVersions =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> mSnapshotVersions =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, long>> mNamedVersions =
            new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        private readonly HashSet<string> mTelemetryFallbackKits =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>判断版本化 Provider 的 state 是否自上次 telemetry 发布后发生变化。</summary>
        /// <param name="provider">待检查的版本化 Provider。</param>
        /// <returns>首次发布或版本不同返回 true。</returns>
        public bool HasTelemetryVersionChanged(IYokiFrameVersionedKitInteractionProvider provider)
        {
            return !mTelemetryVersions.TryGetValue(provider.Kit, out var publishedVersion)
                || publishedVersion != provider.StateVersion;
        }

        /// <summary>判断 Provider 是否需要写入新的文件快照。版本化 Provider 在遥测可用且未进入按 Kit 回落时跳过落盘。</summary>
        /// <param name="provider">快照版本化 Provider；为空时不需要。</param>
        /// <param name="telemetryAvailable">当前宿主实时遥测是否可用。</param>
        /// <returns>需要写入当前 FileBridge state 快照时返回 true。</returns>
        public bool ShouldWriteSnapshot(IYokiFrameSnapshotVersionedKitInteractionProvider provider, bool telemetryAvailable)
        {
            if (provider == null
                || (mSnapshotVersions.TryGetValue(provider.Kit, out var publishedVersion)
                    && publishedVersion == provider.StateVersion))
            {
                return false;
            }

            return !(provider is IYokiFrameVersionedKitInteractionProvider)
                || !telemetryAvailable
                || mTelemetryFallbackKits.Contains(provider.Kit);
        }

        /// <summary>标记 Kit 遥测写入成功并退出按 Kit 回落。</summary>
        /// <param name="kit">Kit 标识。</param>
        public void MarkTelemetrySucceeded(string kit)
        {
            mTelemetryFallbackKits.Remove(kit);
        }

        /// <summary>标记 Kit 遥测写入失败；首次进入回落时返回 true，供宿主只写一次回落快照。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>本次失败使 Kit 新进入回落状态时返回 true。</returns>
        public bool MarkTelemetryFailed(string kit)
        {
            return mTelemetryFallbackKits.Add(kit);
        }

        /// <summary>记录一次完整发布后的 telemetry 版本。</summary>
        /// <param name="provider">刚完成发布的 Provider。</param>
        public void RememberTelemetryVersion(IYokiFrameVersionedKitInteractionProvider provider)
        {
            mTelemetryVersions[provider.Kit] = provider.StateVersion;
        }

        /// <summary>记录一次完整发布后的 snapshot 版本。</summary>
        /// <param name="provider">刚完成发布的 Provider。</param>
        public void RememberSnapshotVersion(IYokiFrameSnapshotVersionedKitInteractionProvider provider)
        {
            mSnapshotVersions[provider.Kit] = provider.StateVersion;
        }

        /// <summary>获取指定 Kit 的命名版本表；仅对版本化命名 Provider 创建。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="versionedNamedProvider">命名 Provider 的版本化视图；可为空。</param>
        /// <returns>需要跟踪版本时返回版本表；否则返回 null。</returns>
        public Dictionary<string, long> GetOrCreateNamedVersions(
            string kit,
            IYokiFrameVersionedNamedTelemetryProvider versionedNamedProvider)
        {
            if (versionedNamedProvider == null)
            {
                return null;
            }

            if (!mNamedVersions.TryGetValue(kit, out var versions))
            {
                versions = new Dictionary<string, long>(StringComparer.Ordinal);
                mNamedVersions.Add(kit, versions);
            }

            return versions;
        }

        /// <summary>记录单个命名帧已发布的版本。</summary>
        /// <param name="publishedVersions">目标 Kit 版本表；可为空（非版本化 Provider）。</param>
        /// <param name="name">安全 telemetry 名称。</param>
        /// <param name="version">已发布版本。</param>
        public void RememberNamedVersion(Dictionary<string, long> publishedVersions, string name, long version)
        {
            if (publishedVersions != null)
            {
                publishedVersions[name] = version;
            }
        }

        /// <summary>
        /// 释放已经不活动实例的命名版本记录。必须逐键比较而不是数量相等短路：
        /// 本轮某个新名称写入失败时数量仍可相等，会漏删已被释放段的旧名称版本，导致其复现时被跳过。
        /// </summary>
        /// <param name="kit">命名 telemetry 所属 Kit。</param>
        /// <param name="activeNames">本轮仍活动的安全名称集合。</param>
        public void RetainNamedVersions(string kit, IReadOnlyList<string> activeNames)
        {
            if (!mNamedVersions.TryGetValue(kit, out var publishedVersions))
            {
                return;
            }

            List<string> staleKeys = null;
            foreach (var name in publishedVersions.Keys)
            {
                if (!ContainsName(activeNames, name))
                {
                    // 稳态轮次没有失效键，延迟到首个失效键出现时才分配列表。
                    if (staleKeys == null)
                    {
                        staleKeys = new List<string>();
                    }

                    staleKeys.Add(name);
                }
            }

            if (staleKeys == null)
            {
                return;
            }

            for (var index = 0; index < staleKeys.Count; index++)
            {
                publishedVersions.Remove(staleKeys[index]);
            }
        }

        /// <summary>仅清空命名版本簿；宿主释放遥测 writer 时调用，保留 state 版本避免重复全量发布。</summary>
        public void ClearNamedVersions()
        {
            mNamedVersions.Clear();
        }

        /// <summary>清空全部版本簿；在 Provider 集合重建或 session/generation 变更时调用。</summary>
        public void Clear()
        {
            mTelemetryVersions.Clear();
            mSnapshotVersions.Clear();
            mNamedVersions.Clear();
        }

        /// <summary>判断活动名称集合是否包含指定实例。</summary>
        /// <param name="activeNames">活动名称集合。</param>
        /// <param name="name">待匹配名称。</param>
        /// <returns>仍活动时返回 true。</returns>
        private static bool ContainsName(IReadOnlyList<string> activeNames, string name)
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
    }
}
#endif
