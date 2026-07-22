#if UNITY_EDITOR

using System;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>只读采集 Unity 当前脚本编译状态。</summary>
    internal static class YokiFrameUnityValidationStatus
    {
        private const string COMPILATION_SOURCE = "UnityEditor.EditorApplication.isCompiling+EditorUtility.scriptCompilationFailed";

        /// <summary>从 Unity Editor 公开 API 创建编译状态快照。</summary>
        /// <param name="context">当前 FileBridge 会话身份。</param>
        /// <returns>带会话身份的编译观察结果。</returns>
        public static YokiFrameUnityValidationObservation Inspect(YokiFrameUnityHarnessContext context)
        {
            return Inspect(context, new UnityValidationProbeProvider());
        }

        /// <summary>使用注入事实源创建编译状态快照。</summary>
        /// <param name="context">当前 FileBridge 会话身份。</param>
        /// <param name="provider">编译事实源。</param>
        /// <returns>编译观察结果。</returns>
        internal static YokiFrameUnityValidationObservation Inspect(
            YokiFrameUnityHarnessContext context,
            IYokiFrameUnityValidationProbeProvider provider)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var probe = provider.ReadCompilation() ?? new YokiFrameUnityCompilationProbe();
            var result = new YokiFrameUnityValidationObservation
            {
                observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                compilation = new YokiFrameUnityCompilationObservation
                {
                    state = probe.IsCompiling ? "Compiling" : probe.ScriptCompilationFailed ? "Failed" : "Idle",
                    isCompiling = probe.IsCompiling,
                    scriptCompilationFailed = probe.ScriptCompilationFailed,
                    isUpdating = probe.IsUpdating,
                    source = COMPILATION_SOURCE
                }
            };
            result.ApplyContext(context);
            return result;
        }

        /// <summary>读取 Unity Editor 当前编译和资源更新状态。</summary>
        private sealed class UnityValidationProbeProvider : IYokiFrameUnityValidationProbeProvider
        {
            /// <summary>仅读取公开状态，不请求编译。</summary>
            /// <returns>当前编译事实。</returns>
            public YokiFrameUnityCompilationProbe ReadCompilation()
            {
                return new YokiFrameUnityCompilationProbe
                {
                    IsCompiling = EditorApplication.isCompiling,
                    ScriptCompilationFailed = EditorUtility.scriptCompilationFailed,
                    IsUpdating = EditorApplication.isUpdating
                };
            }
        }
    }
}

#endif
