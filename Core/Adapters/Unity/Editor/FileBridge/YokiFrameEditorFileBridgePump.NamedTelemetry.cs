#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>承载 Unity Editor 对通用命名 Kit Telemetry Provider 的发布适配。</summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
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
            Dictionary<string, long> publishedVersions =
                sStateVersions.GetOrCreateNamedVersions(provider.Kit, versionedProvider);
            for (var index = 0; index < names.Count; index++)
            {
                WriteNamedTelemetryFrameSafely(
                    namedProvider,
                    versionedProvider,
                    publishedVersions,
                    names[index]);
            }

            YokiFrameEditorTelemetryWriter.RetainNamedStates(provider.Kit, names);
            sStateVersions.RetainNamedVersions(provider.Kit, names);
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
                sStateVersions.RememberNamedVersion(publishedVersions, name, version);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "YokiFrame named telemetry write failed for "
                    + provider.Kit + "/" + name + ": " + exception.Message);
            }
        }

        /// <summary>清空实例版本缓存，使新 session/generation 强制重新发布全部命名帧。</summary>
        private static void ClearNamedTelemetryVersions()
        {
            sStateVersions.ClearNamedVersions();
        }
    }
}

#endif
