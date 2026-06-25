using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 引擎 Adapter 与 Tauri 共享的跨进程遥测帧格式。
    /// 该帧只定义纯字节布局，平台专用共享内存 owner 负责包裹这块缓冲区。
    /// </summary>
    public sealed class SharedMemoryTelemetryFrame
    {
        /// <summary>
        /// 获取帧魔数，内容为 "YKTM" 的小端序整数。
        /// </summary>
        public const int MAGIC = 0x4D544B59;

        /// <summary>
        /// 获取当前共享内存遥测帧格式版本。
        /// </summary>
        public const int FORMAT_VERSION = 1;

        /// <summary>
        /// 获取固定帧头字节数。
        /// </summary>
        public const int HEADER_SIZE = 512;

        /// <summary>
        /// 获取 engineId、kit 和 name 标识符的最大 UTF-8 字节数。
        /// </summary>
        public const int MAX_IDENTIFIER_BYTES = 128;

        /// <summary>
        /// 获取帧序号在帧头中的偏移。
        /// </summary>
        public const int SEQUENCE_OFFSET = 16;

        private const int MAGIC_OFFSET = 0;
        private const int VERSION_OFFSET = 4;
        private const int HEADER_SIZE_OFFSET = 8;
        private const int PAYLOAD_CAPACITY_OFFSET = 12;
        private const int TIMESTAMP_UNIX_MS_OFFSET = 24;
        private const int PAYLOAD_LENGTH_OFFSET = 32;
        private const int ENGINE_ID_LENGTH_OFFSET = 36;
        private const int KIT_LENGTH_OFFSET = 40;
        private const int NAME_LENGTH_OFFSET = 44;
        private const int ENGINE_ID_OFFSET = 64;
        private const int KIT_OFFSET = 192;
        private const int NAME_OFFSET = 320;

        private readonly byte[] mBuffer;
        private readonly int mPayloadCapacity;

        /// <summary>
        /// 创建指定载荷容量的共享内存遥测帧。
        /// </summary>
        /// <param name="payloadCapacity">载荷区域字节容量。</param>
        public SharedMemoryTelemetryFrame(int payloadCapacity)
        {
            if (payloadCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(payloadCapacity));

            mPayloadCapacity = payloadCapacity;
            mBuffer = new byte[HEADER_SIZE + payloadCapacity];
            WriteInt32(mBuffer, MAGIC_OFFSET, MAGIC);
            WriteInt32(mBuffer, VERSION_OFFSET, FORMAT_VERSION);
            WriteInt32(mBuffer, HEADER_SIZE_OFFSET, HEADER_SIZE);
            WriteInt32(mBuffer, PAYLOAD_CAPACITY_OFFSET, payloadCapacity);
        }

        /// <summary>
        /// 获取载荷区域字节容量。
        /// </summary>
        public int PayloadCapacity => mPayloadCapacity;

        /// <summary>
        /// 获取帧总字节数。
        /// </summary>
        public int TotalSize => mBuffer.Length;

        /// <summary>
        /// 获取当前已提交帧序号。
        /// </summary>
        public ulong Sequence
        {
            get
            {
                return ReadUInt64(mBuffer, SEQUENCE_OFFSET) / 2UL;
            }
        }

        /// <summary>
        /// 获取当前帧的引擎实例标识。
        /// </summary>
        public string EngineId => ReadIdentifier(ENGINE_ID_OFFSET, ReadInt32(mBuffer, ENGINE_ID_LENGTH_OFFSET));

        /// <summary>
        /// 获取当前帧的 Kit 名称。
        /// </summary>
        public string Kit => ReadIdentifier(KIT_OFFSET, ReadInt32(mBuffer, KIT_LENGTH_OFFSET));

        /// <summary>
        /// 获取当前帧的遥测通道名称。
        /// </summary>
        public string Name => ReadIdentifier(NAME_OFFSET, ReadInt32(mBuffer, NAME_LENGTH_OFFSET));

        /// <summary>
        /// 获取当前帧的载荷 JSON。
        /// </summary>
        public string PayloadJson
        {
            get
            {
                var length = ReadInt32(mBuffer, PAYLOAD_LENGTH_OFFSET);
                if (length <= 0)
                    return string.Empty;

                return Encoding.UTF8.GetString(mBuffer, HEADER_SIZE, length);
            }
        }

        /// <summary>
        /// 写入最新遥测帧。
        /// </summary>
        /// <param name="engineId">发布该帧的引擎实例标识。</param>
        /// <param name="kit">发布该帧的 Kit 名称。</param>
        /// <param name="name">遥测通道名称。</param>
        /// <param name="payloadJson">遥测载荷 JSON。</param>
        public void WriteLatest(string engineId, string kit, string name, string payloadJson)
        {
            EnsureIdentifier(engineId, nameof(engineId));
            EnsureIdentifier(kit, nameof(kit));
            EnsureIdentifier(name, nameof(name));

            var engineIdBytes = EncodeIdentifier(engineId, nameof(engineId));
            var kitBytes = EncodeIdentifier(kit, nameof(kit));
            var nameBytes = EncodeIdentifier(name, nameof(name));
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson ?? "{}");
            if (payloadBytes.Length > mPayloadCapacity)
                throw new InvalidOperationException("Telemetry payload exceeds shared memory frame capacity.");

            var previousPayloadLength = ReadInt32(mBuffer, PAYLOAD_LENGTH_OFFSET);
            if (previousPayloadLength < 0 || previousPayloadLength > mPayloadCapacity)
                previousPayloadLength = 0;

            var nextSequence = Sequence + 1UL;
            WriteUInt64(mBuffer, SEQUENCE_OFFSET, nextSequence * 2UL - 1UL);

            Array.Clear(mBuffer, ENGINE_ID_OFFSET, MAX_IDENTIFIER_BYTES);
            Array.Clear(mBuffer, KIT_OFFSET, MAX_IDENTIFIER_BYTES);
            Array.Clear(mBuffer, NAME_OFFSET, MAX_IDENTIFIER_BYTES);
            if (payloadBytes.Length < previousPayloadLength)
                Array.Clear(mBuffer, HEADER_SIZE + payloadBytes.Length, previousPayloadLength - payloadBytes.Length);

            Buffer.BlockCopy(engineIdBytes, 0, mBuffer, ENGINE_ID_OFFSET, engineIdBytes.Length);
            Buffer.BlockCopy(kitBytes, 0, mBuffer, KIT_OFFSET, kitBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, mBuffer, NAME_OFFSET, nameBytes.Length);
            Buffer.BlockCopy(payloadBytes, 0, mBuffer, HEADER_SIZE, payloadBytes.Length);

            WriteInt64(mBuffer, TIMESTAMP_UNIX_MS_OFFSET, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            WriteInt32(mBuffer, PAYLOAD_LENGTH_OFFSET, payloadBytes.Length);
            WriteInt32(mBuffer, ENGINE_ID_LENGTH_OFFSET, engineIdBytes.Length);
            WriteInt32(mBuffer, KIT_LENGTH_OFFSET, kitBytes.Length);
            WriteInt32(mBuffer, NAME_LENGTH_OFFSET, nameBytes.Length);
            WriteUInt64(mBuffer, SEQUENCE_OFFSET, nextSequence * 2UL);
        }

        /// <summary>
        /// 复制当前帧缓冲区。
        /// </summary>
        /// <returns>独立的帧字节副本。</returns>
        public byte[] ToArray()
        {
            var copy = new byte[mBuffer.Length];
            Buffer.BlockCopy(mBuffer, 0, copy, 0, mBuffer.Length);
            return copy;
        }

        /// <summary>
        /// 创建一个模拟写入中的帧副本，用于验证 sequence 半读保护。
        /// </summary>
        /// <param name="finalRawSequence">原始最终序号。</param>
        /// <returns>写入中状态的帧字节副本。</returns>
        public byte[] CreateWriteInProgressCopy(out ulong finalRawSequence)
        {
            var copy = ToArray();
            finalRawSequence = ReadUInt64(copy, SEQUENCE_OFFSET);
            if (finalRawSequence != 0UL && (finalRawSequence & 1UL) == 0UL)
                WriteUInt64(copy, SEQUENCE_OFFSET, finalRawSequence - 1UL);

            return copy;
        }

        /// <summary>
        /// 直接写入帧缓冲区的原始序号。
        /// </summary>
        /// <param name="buffer">目标帧缓冲区。</param>
        /// <param name="rawSequence">要写入的原始序号。</param>
        public static void WriteRawSequence(byte[] buffer, ulong rawSequence)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < SEQUENCE_OFFSET + sizeof(ulong))
                throw new ArgumentException("Buffer is too small to contain a telemetry sequence.", nameof(buffer));

            WriteUInt64(buffer, SEQUENCE_OFFSET, rawSequence);
        }

        /// <summary>
        /// 尝试从当前帧读取最新已提交样本。
        /// </summary>
        /// <param name="sample">读取成功时返回最新样本。</param>
        /// <returns>读取成功返回 true；帧不完整、版本不匹配或标识符非法时返回 false。</returns>
        public bool TryReadLatest(out SharedMemoryTelemetrySample sample)
        {
            return TryReadLatest(mBuffer, out sample);
        }

        /// <summary>
        /// 尝试从帧缓冲区读取最新已提交样本。
        /// </summary>
        /// <param name="buffer">帧缓冲区。</param>
        /// <param name="sample">读取成功时返回最新样本。</param>
        /// <returns>读取成功返回 true；帧不完整、版本不匹配或标识符非法时返回 false。</returns>
        public static bool TryReadLatest(byte[] buffer, out SharedMemoryTelemetrySample sample)
        {
            sample = default;
            if (buffer == null || buffer.Length < HEADER_SIZE)
                return false;

            var sequenceBefore = ReadUInt64(buffer, SEQUENCE_OFFSET);
            if (sequenceBefore == 0UL || (sequenceBefore & 1UL) == 1UL)
                return false;

            if (ReadInt32(buffer, MAGIC_OFFSET) != MAGIC ||
                ReadInt32(buffer, VERSION_OFFSET) != FORMAT_VERSION ||
                ReadInt32(buffer, HEADER_SIZE_OFFSET) != HEADER_SIZE)
                return false;

            var payloadCapacity = ReadInt32(buffer, PAYLOAD_CAPACITY_OFFSET);
            var payloadLength = ReadInt32(buffer, PAYLOAD_LENGTH_OFFSET);
            var engineIdLength = ReadInt32(buffer, ENGINE_ID_LENGTH_OFFSET);
            var kitLength = ReadInt32(buffer, KIT_LENGTH_OFFSET);
            var nameLength = ReadInt32(buffer, NAME_LENGTH_OFFSET);
            if (payloadCapacity < 0 ||
                payloadLength < 0 ||
                payloadLength > payloadCapacity ||
                HEADER_SIZE + payloadCapacity > buffer.Length ||
                !IsValidIdentifierLength(engineIdLength) ||
                !IsValidIdentifierLength(kitLength) ||
                !IsValidIdentifierLength(nameLength))
                return false;

            var engineId = Encoding.UTF8.GetString(buffer, ENGINE_ID_OFFSET, engineIdLength);
            var kit = Encoding.UTF8.GetString(buffer, KIT_OFFSET, kitLength);
            var name = Encoding.UTF8.GetString(buffer, NAME_OFFSET, nameLength);
            if (!CommandBridgeProtocol.IsSafeIdentifier(engineId) ||
                !CommandBridgeProtocol.IsSafeIdentifier(kit) ||
                !CommandBridgeProtocol.IsSafeIdentifier(name))
                return false;

            var payloadJson = Encoding.UTF8.GetString(buffer, HEADER_SIZE, payloadLength);
            var timestampUnixMs = ReadInt64(buffer, TIMESTAMP_UNIX_MS_OFFSET);
            var sequenceAfter = ReadUInt64(buffer, SEQUENCE_OFFSET);
            if (sequenceBefore != sequenceAfter || (sequenceAfter & 1UL) == 1UL)
                return false;

            sample = new SharedMemoryTelemetrySample(
                sequenceAfter / 2UL,
                DateTimeOffset.FromUnixTimeMilliseconds(timestampUnixMs).UtcDateTime,
                engineId,
                kit,
                name,
                payloadJson);
            return true;
        }

        private static bool IsValidIdentifierLength(int length)
        {
            return length > 0 && length <= MAX_IDENTIFIER_BYTES;
        }

        private string ReadIdentifier(int offset, int length)
        {
            if (!IsValidIdentifierLength(length))
                return string.Empty;

            return Encoding.UTF8.GetString(mBuffer, offset, length);
        }

        private static byte[] EncodeIdentifier(string value, string name)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > MAX_IDENTIFIER_BYTES)
                throw new ArgumentException("Telemetry identifier is too long: " + value, name);

            return bytes;
        }

        private static void EnsureIdentifier(string value, string name)
        {
            if (!CommandBridgeProtocol.IsSafeIdentifier(value))
                throw new ArgumentException("Invalid telemetry identifier: " + value, name);
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return buffer[offset] |
                   buffer[offset + 1] << 8 |
                   buffer[offset + 2] << 16 |
                   buffer[offset + 3] << 24;
        }

        private static long ReadInt64(byte[] buffer, int offset)
        {
            var lo = (uint)ReadInt32(buffer, offset);
            var hi = (uint)ReadInt32(buffer, offset + 4);
            return (long)(lo | (ulong)hi << 32);
        }

        private static ulong ReadUInt64(byte[] buffer, int offset)
        {
            var lo = (uint)ReadInt32(buffer, offset);
            var hi = (uint)ReadInt32(buffer, offset + 4);
            return lo | (ulong)hi << 32;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt64(byte[] buffer, int offset, long value)
        {
            WriteUInt64(buffer, offset, (ulong)value);
        }

        private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
        }
    }
}
