using System.Buffers.Binary;
using YokiFrame;

namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 表示 Shared Memory telemetry 帧头，字段顺序与架构文档中的 v1 格式一致。
/// </summary>
public sealed class SharedMemoryTelemetryFrameHeader
{
    /// <summary>
    /// Shared Memory telemetry v1 magic，按小端序写入 ASCII `YFTM`。
    /// </summary>
    public const uint MAGIC = YokiFrameSharedMemoryTelemetryContract.MAGIC;

    /// <summary>
    /// 当前 reader 支持的 Shared Memory telemetry 协议版本。
    /// </summary>
    public const int PROTOCOL_VERSION = YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION;

    /// <summary>
    /// v1 header 固定字节数。
    /// </summary>
    public const int HEADER_SIZE = YokiFrameSharedMemoryTelemetryContract.HEADER_SIZE;

    /// <summary>
    /// 创建 Shared Memory telemetry 帧头。
    /// </summary>
    /// <param name="magic">帧 magic。</param>
    /// <param name="protocolVersion">协议版本。</param>
    /// <param name="engineIdHash">engine 标识哈希，用于区分内存段归属。</param>
    /// <param name="generation">engine generation。</param>
    /// <param name="sequence">帧序号。</param>
    /// <param name="writtenAtUtcTicks">写入时间 UTC ticks。</param>
    /// <param name="payloadLength">payload 字节数。</param>
    /// <param name="payloadCrc32">payload CRC32。</param>
    /// <param name="writeState">写入状态。</param>
    public SharedMemoryTelemetryFrameHeader(
        uint magic,
        int protocolVersion,
        ulong engineIdHash,
        long generation,
        long sequence,
        long writtenAtUtcTicks,
        int payloadLength,
        uint payloadCrc32,
        SharedMemoryTelemetryWriteState writeState)
    {
        Magic = magic;
        ProtocolVersion = protocolVersion;
        EngineIdHash = engineIdHash;
        Generation = generation;
        Sequence = sequence;
        WrittenAtUtcTicks = writtenAtUtcTicks;
        PayloadLength = payloadLength;
        PayloadCrc32 = payloadCrc32;
        WriteState = writeState;
    }

    /// <summary>
    /// 获取帧 magic。
    /// </summary>
    public uint Magic { get; }

    /// <summary>
    /// 获取协议版本。
    /// </summary>
    public int ProtocolVersion { get; }

    /// <summary>
    /// 获取 engine 标识哈希。
    /// </summary>
    public ulong EngineIdHash { get; }

    /// <summary>
    /// 获取 engine generation。
    /// </summary>
    public long Generation { get; }

    /// <summary>
    /// 获取帧序号。
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// 获取写入时间 UTC ticks。
    /// </summary>
    public long WrittenAtUtcTicks { get; }

    /// <summary>
    /// 获取 payload 字节数。
    /// </summary>
    public int PayloadLength { get; }

    /// <summary>
    /// 获取 payload CRC32。
    /// </summary>
    public uint PayloadCrc32 { get; }

    /// <summary>
    /// 获取写入状态。
    /// </summary>
    public SharedMemoryTelemetryWriteState WriteState { get; }

    /// <summary>
    /// 从固定长度 header 字节读取帧头；调用前需保证缓冲区长度足够。
    /// </summary>
    /// <param name="headerBytes">header 字节。</param>
    /// <returns>解析后的帧头。</returns>
    public static SharedMemoryTelemetryFrameHeader Read(ReadOnlySpan<byte> headerBytes)
    {
        return new SharedMemoryTelemetryFrameHeader(
            BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET..YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET]),
            BinaryPrimitives.ReadInt32LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET..YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET]),
            BinaryPrimitives.ReadUInt64LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET..YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET]),
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET..YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET]),
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET..YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET]),
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET..YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET]),
            BinaryPrimitives.ReadInt32LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET..YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET]),
            BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET..YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET]),
            (SharedMemoryTelemetryWriteState)BinaryPrimitives.ReadInt32LittleEndian(headerBytes[
                YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET..YokiFrameSharedMemoryTelemetryContract.HEADER_SIZE]));
    }

    /// <summary>
    /// 把当前 header 写入固定长度缓冲区，供测试和后续 writer 复用。
    /// </summary>
    /// <param name="headerBytes">目标 header 缓冲区。</param>
    public void WriteTo(Span<byte> headerBytes)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET..YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET], Magic);
        BinaryPrimitives.WriteInt32LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET..YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET], ProtocolVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET..YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET], EngineIdHash);
        BinaryPrimitives.WriteInt64LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET..YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET], Generation);
        BinaryPrimitives.WriteInt64LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET..YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET], Sequence);
        BinaryPrimitives.WriteInt64LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET..YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET], WrittenAtUtcTicks);
        BinaryPrimitives.WriteInt32LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET..YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET], PayloadLength);
        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET..YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET], PayloadCrc32);
        BinaryPrimitives.WriteInt32LittleEndian(headerBytes[
            YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET..YokiFrameSharedMemoryTelemetryContract.HEADER_SIZE], (int)WriteState);
    }
}
