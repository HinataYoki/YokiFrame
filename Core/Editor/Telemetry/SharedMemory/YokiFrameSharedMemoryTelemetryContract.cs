#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义 Shared Memory Telemetry v1 的二进制 header、容量和提交状态。
    /// </summary>
    public static class YokiFrameSharedMemoryTelemetryContract
    {
        /// <summary>
        /// 小端序 ASCII `YFTM` magic。
        /// </summary>
        public const uint MAGIC = 0x4D544659u;

        /// <summary>
        /// Shared Memory Telemetry 协议版本。
        /// </summary>
        public const int PROTOCOL_VERSION = 1;

        /// <summary>
        /// v1 header 固定字节数，也是 payload 起始偏移。
        /// </summary>
        public const int HEADER_SIZE = 52;

        /// <summary>
        /// 默认 payload 最大字节数。
        /// </summary>
        public const int DEFAULT_MAX_PAYLOAD_BYTES = 64 * 1024;

        /// <summary>
        /// magic 字段偏移。
        /// </summary>
        public const int MAGIC_OFFSET = 0;

        /// <summary>
        /// protocolVersion 字段偏移。
        /// </summary>
        public const int PROTOCOL_VERSION_OFFSET = 4;

        /// <summary>
        /// engineIdHash 字段偏移。
        /// </summary>
        public const int ENGINE_ID_HASH_OFFSET = 8;

        /// <summary>
        /// generation 字段偏移。
        /// </summary>
        public const int GENERATION_OFFSET = 16;

        /// <summary>
        /// sequence 字段偏移。
        /// </summary>
        public const int SEQUENCE_OFFSET = 24;

        /// <summary>
        /// writtenAtUtcTicks 字段偏移。
        /// </summary>
        public const int WRITTEN_AT_UTC_TICKS_OFFSET = 32;

        /// <summary>
        /// payloadLength 字段偏移。
        /// </summary>
        public const int PAYLOAD_LENGTH_OFFSET = 40;

        /// <summary>
        /// payloadCrc32 字段偏移。
        /// </summary>
        public const int PAYLOAD_CRC32_OFFSET = 44;

        /// <summary>
        /// writeState 字段偏移。
        /// </summary>
        public const int WRITE_STATE_OFFSET = 48;

        /// <summary>
        /// payload 起始偏移。
        /// </summary>
        public const int PAYLOAD_OFFSET = HEADER_SIZE;

        /// <summary>
        /// 尚未写入的保留状态。
        /// </summary>
        public const int WRITE_STATE_EMPTY = 0;

        /// <summary>
        /// writer 正在更新 header 或 payload。
        /// </summary>
        public const int WRITE_STATE_WRITING = 1;

        /// <summary>
        /// writer 已完成并发布稳定帧。
        /// </summary>
        public const int WRITE_STATE_COMMITTED = 2;
    }
}
#endif
