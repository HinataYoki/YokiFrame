#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 创建项目级 Shared Memory Telemetry 变化通知名称。
    /// </summary>
    public static class YokiFrameSharedMemoryTelemetryNotificationName
    {
        private const string NOTIFICATION_PREFIX = "YokiFrame.Telemetry.Notify.";
        private const string NOTIFICATION_SUFFIX = ".v1";

        /// <summary>
        /// 根据项目作用域和 engine 标识创建同机唯一的通知名称。
        /// </summary>
        /// <param name="projectScopeId">由规范化项目根计算的安全作用域。</param>
        /// <param name="engineId">当前宿主的安全 engine 标识。</param>
        /// <returns>可用于 Windows Named Event 的稳定名称。</returns>
        public static string Create(string projectScopeId, string engineId)
        {
            EnsureSafeId(projectScopeId, nameof(projectScopeId));
            EnsureSafeId(engineId, nameof(engineId));
            return NOTIFICATION_PREFIX + projectScopeId + "." + engineId + NOTIFICATION_SUFFIX;
        }

        /// <summary>
        /// 校验通知名称组成部分，避免跨项目或非法字符造成通道冲突。
        /// </summary>
        /// <param name="value">待校验的名称片段。</param>
        /// <param name="parameterName">参数名。</param>
        private static void EnsureSafeId(string value, string parameterName)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(value))
            {
                throw new ArgumentException("Telemetry notification parts must be safe YokiFrame IDs.", parameterName);
            }
        }
    }
}
#endif
