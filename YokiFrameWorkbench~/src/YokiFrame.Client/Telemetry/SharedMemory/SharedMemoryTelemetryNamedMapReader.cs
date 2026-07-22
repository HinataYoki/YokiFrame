using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using YokiFrame;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Telemetry.SharedMemory;

/// <summary>
/// 从 Windows named memory map 读取 Shared Memory telemetry v1 帧。
/// </summary>
internal static class SharedMemoryTelemetryNamedMapReader
{
    /// <summary>
    /// 从已经绑定当前 segment 与 generation 的 accessor 读取 telemetry 帧。
    /// </summary>
    /// <param name="accessor">由轻量 lease 持有的只读 accessor。</param>
    /// <param name="expectedGeneration">期望 generation；为 null 时不校验。</param>
    /// <param name="expectedEngineIdHash">由请求 engineId 计算的预期宿主哈希。</param>
    /// <param name="maxPayloadBytes">允许读取的 payload 最大字节数。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号；为空时完整读取。</param>
    /// <param name="firstHeaderBytes">lease 复用的第一次 header 缓冲区。</param>
    /// <param name="secondHeaderBytes">lease 复用的第二次 header 缓冲区。</param>
    /// <param name="payloadReadAttemptObserver">仅供内部测试观察 payload 读取入口；生产调用保持为空。</param>
    /// <returns>新帧或读取失败结果；稳定未变化帧返回空。</returns>
    public static SharedMemoryTelemetryFrameReadResult? Read(
        MemoryMappedViewAccessor accessor,
        long? expectedGeneration,
        ulong expectedEngineIdHash,
        int maxPayloadBytes,
        long? afterSequence,
        byte[] firstHeaderBytes,
        byte[] secondHeaderBytes,
        Action? payloadReadAttemptObserver = null)
    {
        if (accessor.Capacity < SharedMemoryTelemetryFrameHeader.HEADER_SIZE)
        {
            return new SharedMemoryTelemetryFrameReadResult(
                SharedMemoryTelemetryFrameStatus.BufferTooSmall,
                null,
                string.Empty,
                "Telemetry segment is smaller than header.");
        }

        return ReadFrameSnapshot(
            accessor,
            firstHeaderBytes,
            secondHeaderBytes,
            expectedGeneration,
            expectedEngineIdHash,
            maxPayloadBytes,
            afterSequence,
            payloadReadAttemptObserver);
    }

    /// <summary>使用租用的 header 缓冲区完成未变化短路或一次完整帧校验。</summary>
    /// <param name="accessor">已打开的 shared memory accessor。</param>
    /// <param name="firstHeaderBytes">第一次 header 复用缓冲区。</param>
    /// <param name="secondHeaderBytes">第二次 header 复用缓冲区。</param>
    /// <param name="expectedGeneration">期望 generation。</param>
    /// <param name="expectedEngineIdHash">预期宿主哈希。</param>
    /// <param name="maxPayloadBytes">payload 最大字节数。</param>
    /// <param name="afterSequence">增量读取的 sequence 游标；为空时完整读取。</param>
    /// <param name="payloadReadAttemptObserver">仅供内部测试观察 payload 读取入口。</param>
    /// <returns>读取结果；稳定未变化时返回空。</returns>
    private static SharedMemoryTelemetryFrameReadResult? ReadFrameSnapshot(
        MemoryMappedViewAccessor accessor,
        byte[] firstHeaderBytes,
        byte[] secondHeaderBytes,
        long? expectedGeneration,
        ulong expectedEngineIdHash,
        int maxPayloadBytes,
        long? afterSequence,
        Action? payloadReadAttemptObserver)
    {
        var headerSize = SharedMemoryTelemetryFrameHeader.HEADER_SIZE;
        ReadBytes(accessor, 0, firstHeaderBytes, headerSize);
        if (afterSequence.HasValue && CanSkipPayload(
                accessor, firstHeaderBytes, secondHeaderBytes,
                expectedGeneration, expectedEngineIdHash, maxPayloadBytes, afterSequence.Value))
        {
            return null;
        }

        var firstHeaderSpan = firstHeaderBytes.AsSpan(0, headerSize);
        var canReadPayload = IsReadableCommittedHeader(
            accessor,
            firstHeaderSpan,
            expectedGeneration,
            expectedEngineIdHash,
            maxPayloadBytes);
        var firstHeader = SharedMemoryTelemetryFrameHeader.Read(firstHeaderSpan);
        var payloadBytes = canReadPayload
            ? ReadPayload(accessor, firstHeader, maxPayloadBytes, payloadReadAttemptObserver)
            : Array.Empty<byte>();
        ReadBytes(accessor, 0, secondHeaderBytes, headerSize);
        return SharedMemoryTelemetryFrameReader.ReadFrame(
            firstHeaderBytes.AsSpan(0, headerSize),
            payloadBytes,
            secondHeaderBytes.AsSpan(0, headerSize),
            expectedGeneration,
            maxPayloadBytes,
            expectedEngineIdHash);
    }

    /// <summary>确认帧已被游标消费且两次 header 稳定，满足条件时不复制 payload。</summary>
    /// <param name="accessor">已打开的 shared memory accessor。</param>
    /// <param name="firstHeaderBytes">第一次 header 字节。</param>
    /// <param name="secondHeaderBytes">调用方复用的第二次 header 缓冲区。</param>
    /// <param name="expectedGeneration">期望 generation。</param>
    /// <param name="expectedEngineIdHash">预期宿主哈希。</param>
    /// <param name="maxPayloadBytes">payload 最大字节数。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号。</param>
    /// <returns>可安全跳过 payload 时返回 true。</returns>
    private static bool CanSkipPayload(
        MemoryMappedViewAccessor accessor,
        byte[] firstHeaderBytes,
        byte[] secondHeaderBytes,
        long? expectedGeneration,
        ulong expectedEngineIdHash,
        int maxPayloadBytes,
        long afterSequence)
    {
        var headerBytes = firstHeaderBytes.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE);
        var sequence = BinaryPrimitives.ReadInt64LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET..
            YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET]);
        if (sequence > afterSequence
            || !IsReadableCommittedHeader(
                accessor,
                headerBytes,
                expectedGeneration,
                expectedEngineIdHash,
                maxPayloadBytes))
        {
            return false;
        }

        var headerSize = SharedMemoryTelemetryFrameHeader.HEADER_SIZE;
        ReadBytes(accessor, 0, secondHeaderBytes, headerSize);
        return firstHeaderBytes.AsSpan(0, headerSize)
            .SequenceEqual(secondHeaderBytes.AsSpan(0, headerSize));
    }

    /// <summary>检查 header 是否满足未变化短路所需的完整基础约束。</summary>
    /// <param name="accessor">当前内存段 accessor。</param>
    /// <param name="headerBytes">待检查的固定长度 header 字节。</param>
    /// <param name="expectedGeneration">期望 generation。</param>
    /// <param name="expectedEngineIdHash">预期宿主哈希。</param>
    /// <param name="maxPayloadBytes">payload 最大字节数。</param>
    /// <returns>header 已提交、身份匹配且 payload 范围可读时返回 true。</returns>
    private static bool IsReadableCommittedHeader(
        MemoryMappedViewAccessor accessor,
        ReadOnlySpan<byte> headerBytes,
        long? expectedGeneration,
        ulong expectedEngineIdHash,
        int maxPayloadBytes)
    {
        var payloadOffset = YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET;
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET..
            YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET]);
        return ReadUInt32(headerBytes, YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET)
                == SharedMemoryTelemetryFrameHeader.MAGIC
            && ReadInt32(headerBytes, YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET)
                == SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION
            && ReadInt32(headerBytes, YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET)
                == (int)SharedMemoryTelemetryWriteState.Committed
            && ReadUInt64(headerBytes, YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET)
                == expectedEngineIdHash
            && (!expectedGeneration.HasValue
                || ReadInt64(headerBytes, YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET)
                    == expectedGeneration.Value)
            && payloadLength >= 0
            && payloadLength <= maxPayloadBytes
            && accessor.Capacity >= (long)payloadOffset + payloadLength;
    }

    /// <summary>按固定协议偏移读取 Int32，避免未变化热路径构造 header 对象。</summary>
    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + sizeof(int))]);
    }

    /// <summary>按固定协议偏移读取 UInt32，避免未变化热路径构造 header 对象。</summary>
    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + sizeof(uint))]);
    }

    /// <summary>按固定协议偏移读取 Int64，避免未变化热路径构造 header 对象。</summary>
    private static long ReadInt64(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[offset..(offset + sizeof(long))]);
    }

    /// <summary>按固定协议偏移读取 UInt64，避免未变化热路径构造 header 对象。</summary>
    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..(offset + sizeof(ulong))]);
    }

    /// <summary>
    /// 根据第一次 header 安全复制 payload；长度异常时返回空 payload 交由统一校验处理。
    /// </summary>
    /// <param name="accessor">已打开的 shared memory accessor。</param>
    /// <param name="header">第一次 header 快照。</param>
    /// <param name="maxPayloadBytes">payload 最大字节数。</param>
    /// <param name="payloadReadAttemptObserver">仅供内部测试确认是否进入 payload 读取。</param>
    /// <returns>payload 字节快照。</returns>
    private static byte[] ReadPayload(
        MemoryMappedViewAccessor accessor,
        SharedMemoryTelemetryFrameHeader header,
        int maxPayloadBytes,
        Action? payloadReadAttemptObserver)
    {
        payloadReadAttemptObserver?.Invoke();
        if (header.PayloadLength < 0 || header.PayloadLength > maxPayloadBytes)
        {
            return Array.Empty<byte>();
        }

        var payloadOffset = YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET;
        return accessor.Capacity >= (long)payloadOffset + header.PayloadLength
            ? ReadBytes(accessor, payloadOffset, header.PayloadLength)
            : Array.Empty<byte>();
    }

    /// <summary>
    /// 从 accessor 复制指定范围的字节。
    /// </summary>
    /// <param name="accessor">已打开的 shared memory accessor。</param>
    /// <param name="offset">读取偏移。</param>
    /// <param name="count">读取字节数。</param>
    /// <returns>复制出的字节数组。</returns>
    private static byte[] ReadBytes(MemoryMappedViewAccessor accessor, long offset, int count)
    {
        var buffer = new byte[count];
        accessor.ReadArray(offset, buffer, 0, count);
        return buffer;
    }

    /// <summary>把指定范围读入复用缓冲区，避免空闲高频路径持续创建 header 数组。</summary>
    /// <param name="accessor">已打开的 shared memory accessor。</param>
    /// <param name="offset">读取偏移。</param>
    /// <param name="buffer">容量不小于 count 的复用缓冲区。</param>
    /// <param name="count">需要读取的字节数。</param>
    private static void ReadBytes(
        MemoryMappedViewAccessor accessor,
        long offset,
        byte[] buffer,
        int count)
    {
        accessor.ReadArray(offset, buffer, 0, count);
    }

}
