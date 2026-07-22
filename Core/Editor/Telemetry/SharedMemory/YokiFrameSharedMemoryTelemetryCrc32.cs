#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Shared Memory Telemetry payload 使用的 IEEE CRC32 算法。
    /// </summary>
    public static class YokiFrameSharedMemoryTelemetryCrc32
    {
        private const uint CRC_SEED = 0xFFFFFFFFu;
        private const uint CRC_POLYNOMIAL = 0xEDB88320u;

        /// <summary>
        /// 计算完整字节数组的 CRC32，供不支持 Span 调用的宿主适配器使用。
        /// </summary>
        /// <param name="payload">待校验 payload；不能为 null。</param>
        /// <returns>IEEE CRC32 校验值。</returns>
        public static uint Compute(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return Compute(new ReadOnlySpan<byte>(payload));
        }

        /// <summary>
        /// 计算只读字节范围的 CRC32，供 Tool reader 热路径无分配复用。
        /// </summary>
        /// <param name="payload">待校验 payload。</param>
        /// <returns>IEEE CRC32 校验值。</returns>
        public static uint Compute(ReadOnlySpan<byte> payload)
        {
            var crc = CRC_SEED;
            for (var index = 0; index < payload.Length; index++)
            {
                crc ^= payload[index];
                for (var bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    var mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (CRC_POLYNOMIAL & mask);
                }
            }

            return ~crc;
        }
    }
}
#endif
