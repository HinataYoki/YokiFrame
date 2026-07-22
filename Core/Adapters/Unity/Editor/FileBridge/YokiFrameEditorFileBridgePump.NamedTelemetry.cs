#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>承载 Unity Editor 对通用命名 Kit Telemetry Provider 的发布适配。</summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
        private static readonly Dictionary<string, Dictionary<string, long>> sNamedTelemetryVersions = new();

        /// <summary>
        /// 发布 Provider 当前声明的全部命名 latest frame，并释放已经不活动的映射。
        /// </summary>
        /// <param name="provider">当前 Kit Interaction Provider。</param>
        private static void WriteNamedTelemetry(IYokiFrameKitInteractionProvider provider)
        {
            var namedProvider = provider as IYokiFrameNamedTelemetryProvider;
            if (namedProvider == null)
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
                WriteNamedTelemetryFrameSafely(
                    namedProvider,
                    versionedProvider,
                    publishedVersions,
                    names[index]);
            }

            YokiFrameEditorTelemetryWriter.RetainNamedStates(provider.Kit, names);
            RetainNamedTelemetryVersions(provider.Kit, names);
        }

        /// <summary>
        /// 隔离单个命名 frame 的创建或写入异常，避免一个已失效实例阻断同 Kit 其它实例。
        /// </summary>
        /// <param name="provider">命名 Telemetry Provider。</param>
        /// <param name="name">本轮声明的安全名称。</param>
        private static void WriteNamedTelemetryFrameSafely(
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

                string payloadJson = provider.CreateTelemetry(name);
                YokiFrameEditorTelemetryWriter.WriteState(
                    provider.Kit,
                    name,
                    payloadJson,
                    sGeneration,
                    sSequence);
                if (versionedProvider != null)
                {
                    publishedVersions[name] = version;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "YokiFrame named telemetry write failed for "
                    + provider.Kit + "/" + name + ": " + exception.Message);
            }
        }

        /// <summary>释放已经不活动实例的版本记录，保持缓存与 writer 当前命名映射一致。</summary>
        /// <param name="kit">命名 Telemetry 所属 Kit。</param>
        /// <param name="activeNames">当前仍活动的安全名称。</param>
        private static void RetainNamedTelemetryVersions(string kit, IReadOnlyList<string> activeNames)
        {
            if (!sNamedTelemetryVersions.TryGetValue(kit, out var publishedVersions)
                || publishedVersions.Count == activeNames.Count)
            {
                return;
            }

            List<string> staleKeys = new();
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
        private static Dictionary<string, long> GetOrCreateNamedTelemetryVersions(string kit)
        {
            if (!sNamedTelemetryVersions.TryGetValue(kit, out var versions))
            {
                versions = new Dictionary<string, long>(StringComparer.Ordinal);
                sNamedTelemetryVersions.Add(kit, versions);
            }

            return versions;
        }

        /// <summary>清空实例版本缓存，使新 session/generation 强制重新发布全部命名帧。</summary>
        private static void ClearNamedTelemetryVersions()
        {
            sNamedTelemetryVersions.Clear();
        }
    }
}

#endif
