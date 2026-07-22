#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 为 Shared Memory Telemetry v1 的安全 engine 标识生成跨宿主一致的 FNV-1a 64 hash。
    /// </summary>
    public static class YokiFrameSharedMemoryTelemetryEngineIdHash
    {
        private const ulong FNV1A_64_OFFSET_BASIS = 14695981039346656037UL;
        private const ulong FNV1A_64_PRIME = 1099511628211UL;

        /// <summary>
        /// 计算 SafeId 限制的 ASCII engine 标识的 UTF-8 FNV-1a 64 值；禁止使用进程随机的 string hash。
        /// </summary>
        /// <param name="engineId">符合 YokiFrame SafeId 约束的 engine 标识。</param>
        /// <returns>稳定的 Telemetry engine 标识 hash。</returns>
        public static ulong Compute(string engineId)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(engineId))
            {
                throw new ArgumentException("Telemetry engineId must be a safe YokiFrame ID.", nameof(engineId));
            }

            unchecked
            {
                var hash = FNV1A_64_OFFSET_BASIS;
                for (var index = 0; index < engineId.Length; index++)
                {
                    hash ^= (byte)engineId[index];
                    hash *= FNV1A_64_PRIME;
                }

                return hash;
            }
        }
    }
}
#endif
