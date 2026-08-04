#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 创建 Shared Memory Telemetry v1 的跨宿主逻辑 segment 名称。
    /// </summary>
    public static class YokiFrameSharedMemoryTelemetrySegmentName
    {
        /// <summary>
        /// 跨平台共享内存名称的保守长度上限；宿主与 Workbench 必须使用同一限制。
        /// </summary>
        public const int MAX_SEGMENT_NAME_LENGTH = 240;

        private const string SEGMENT_PREFIX = "YokiFrame.Telemetry.";
        private const string SEGMENT_SUFFIX = ".v1";

        /// <summary>
        /// 根据项目作用域、engine、Kit 和状态名创建标准 segment 名称。
        /// </summary>
        /// <param name="projectScopeId">项目根生成的安全作用域。</param>
        /// <param name="engineId">安全 engine 标识。</param>
        /// <param name="kit">安全 Kit 标识。</param>
        /// <param name="name">安全 telemetry 名称。</param>
        /// <returns>标准逻辑 segment 名称。</returns>
        public static string Create(string projectScopeId, string engineId, string kit, string name)
        {
            EnsureSafeId(projectScopeId, nameof(projectScopeId));
            EnsureSafeId(engineId, nameof(engineId));
            EnsureSafeId(kit, nameof(kit));
            EnsureSafeId(name, nameof(name));
            var segmentName = SEGMENT_PREFIX + projectScopeId + "." + engineId + "." + kit + "." + name + SEGMENT_SUFFIX;
            if (segmentName.Length > MAX_SEGMENT_NAME_LENGTH)
            {
                throw new ArgumentException(
                    "Telemetry segment name must not exceed " + MAX_SEGMENT_NAME_LENGTH + " characters.",
                    nameof(name));
            }

            return segmentName;
        }

        /// <summary>
        /// 校验 segment 组成部分，避免宿主创建不可预测或越界名称。
        /// </summary>
        /// <param name="value">待检查值。</param>
        /// <param name="parameterName">参数名。</param>
        private static void EnsureSafeId(string value, string parameterName)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(value))
            {
                throw new ArgumentException("Telemetry segment parts must be safe YokiFrame IDs.", parameterName);
            }
        }
    }
}
#endif
