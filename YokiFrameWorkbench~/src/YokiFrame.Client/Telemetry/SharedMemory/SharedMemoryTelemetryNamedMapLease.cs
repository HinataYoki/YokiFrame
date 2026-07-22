using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Telemetry.SharedMemory;

/// <summary>
/// 持有单个 segment、engine 身份与 generation 对应的只读映射，避免高频轮询重复打开 OS 句柄。
/// </summary>
internal sealed class SharedMemoryTelemetryNamedMapLease : IDisposable
{
    private readonly string mSegmentName;
    private readonly long? mExpectedGeneration;
    private readonly ulong mExpectedEngineIdHash;
    private MemoryMappedFile? mMemoryMap;
    private MemoryMappedViewAccessor? mAccessor;
    private byte[]? mFirstHeaderBytes;
    private byte[]? mSecondHeaderBytes;
    private bool mDisposed;

    /// <summary>
    /// 创建绑定固定 segment 与宿主身份的轻量 lease；实际 OS 句柄延迟到首次读取时打开。
    /// </summary>
    /// <param name="segmentName">已包含项目、engine、Kit 与名称作用域的 segment 名称。</param>
    /// <param name="expectedGeneration">当前 registry/heartbeat 确认的 generation。</param>
    /// <param name="expectedEngineIdHash">当前 engineId 的稳定哈希。</param>
    public SharedMemoryTelemetryNamedMapLease(
        string segmentName,
        long? expectedGeneration,
        ulong expectedEngineIdHash)
    {
        mSegmentName = segmentName;
        mExpectedGeneration = expectedGeneration;
        mExpectedEngineIdHash = expectedEngineIdHash;
    }

    /// <summary>获取本 lease 成功打开 map/accessor 的次数，供性能回归验证同代复用。</summary>
    public int OpenCount { get; private set; }

    /// <summary>获取当前 lease 是否持有可复用 accessor。</summary>
    public bool IsOpen => mAccessor != null;

    /// <summary>
    /// 读取当前完整帧；身份或稳定读取失败会立即释放句柄，下一次调用再尝试重连。
    /// </summary>
    /// <param name="maxPayloadBytes">允许读取的 payload 最大字节数。</param>
    /// <returns>帧读取结果。</returns>
    public SharedMemoryTelemetryFrameReadResult Read(int maxPayloadBytes)
    {
        return ReadCore(maxPayloadBytes, null)!;
    }

    /// <summary>
    /// 只读取晚于游标的帧；稳定未变化时保留句柄并在 payload 复制前返回空。
    /// </summary>
    /// <param name="maxPayloadBytes">允许读取的 payload 最大字节数。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号。</param>
    /// <returns>新帧或读取失败结果；未变化时返回空。</returns>
    public SharedMemoryTelemetryFrameReadResult? ReadIfChanged(int maxPayloadBytes, long afterSequence)
    {
        return ReadCore(maxPayloadBytes, afterSequence);
    }

    /// <summary>
    /// 打开或复用 accessor 完成一次读取，并把 IO、权限和对象失效统一转换为可回落状态。
    /// </summary>
    /// <param name="maxPayloadBytes">允许读取的 payload 最大字节数。</param>
    /// <param name="afterSequence">可选的增量帧序号。</param>
    /// <returns>读取结果；稳定未变化时返回空。</returns>
    private SharedMemoryTelemetryFrameReadResult? ReadCore(int maxPayloadBytes, long? afterSequence)
    {
        ThrowIfDisposed();
        if (!OperatingSystem.IsWindows())
        {
            return CreateUnavailable("Named memory map telemetry is only enabled on Windows in this slice.");
        }

        try
        {
            EnsureOpen();
            var result = SharedMemoryTelemetryNamedMapReader.Read(
                mAccessor!, mExpectedGeneration, mExpectedEngineIdHash, maxPayloadBytes,
                afterSequence, mFirstHeaderBytes!, mSecondHeaderBytes!);
            if (result != null && !CanKeepOpen(result.Status))
            {
                ReleaseMap();
            }

            return result;
        }
        catch (FileNotFoundException)
        {
            return ReleaseAndCreateUnavailable("Telemetry segment was not found.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return ReleaseAndCreateUnavailable("Telemetry segment could not be opened for read: " + exception.Message);
        }
        catch (IOException exception)
        {
            return ReleaseAndCreateUnavailable("Telemetry segment read failed: " + exception.Message);
        }
        catch (ObjectDisposedException exception)
        {
            return ReleaseAndCreateUnavailable("Telemetry segment reader was invalidated: " + exception.Message);
        }
    }

    /// <summary>
    /// 延迟打开 map/accessor，并为整个 lease 生命周期租用两块固定 header 缓冲区。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private void EnsureOpen()
    {
        if (mAccessor != null)
        {
            return;
        }

        var memoryMap = MemoryMappedFile.OpenExisting(mSegmentName, MemoryMappedFileRights.Read);
        try
        {
            mAccessor = memoryMap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            mMemoryMap = memoryMap;
            RentHeaderBuffers();
            OpenCount++;
        }
        catch
        {
            mAccessor?.Dispose();
            mAccessor = null;
            memoryMap.Dispose();
            throw;
        }
    }

    /// <summary>只在首次成功打开时租用 header 缓冲区，后续同代轮询不再触碰 ArrayPool。</summary>
    private void RentHeaderBuffers()
    {
        var headerSize = SharedMemoryTelemetryFrameHeader.HEADER_SIZE;
        mFirstHeaderBytes ??= ArrayPool<byte>.Shared.Rent(headerSize);
        mSecondHeaderBytes ??= ArrayPool<byte>.Shared.Rent(headerSize);
    }

    /// <summary>判断短暂写入状态是否仍允许安全保留当前映射。</summary>
    /// <param name="status">本次帧读取状态。</param>
    /// <returns>已接受或可短重试状态返回 true。</returns>
    private static bool CanKeepOpen(SharedMemoryTelemetryFrameStatus status)
    {
        return status is SharedMemoryTelemetryFrameStatus.Accepted
            or SharedMemoryTelemetryFrameStatus.Writing
            or SharedMemoryTelemetryFrameStatus.HalfWrite;
    }

    /// <summary>释放失效映射并创建包含 segment 证据的不可用结果。</summary>
    /// <param name="message">当前失败原因。</param>
    /// <returns>可供上层回落 snapshot 的结果。</returns>
    private SharedMemoryTelemetryFrameReadResult ReleaseAndCreateUnavailable(string message)
    {
        ReleaseMap();
        return CreateUnavailable(message);
    }

    /// <summary>创建包含当前 segment 名称的不可用读取结果。</summary>
    /// <param name="message">当前失败原因。</param>
    /// <returns>不可用读取结果。</returns>
    private SharedMemoryTelemetryFrameReadResult CreateUnavailable(string message)
    {
        return new SharedMemoryTelemetryFrameReadResult(
            SharedMemoryTelemetryFrameStatus.Unavailable,
            null,
            string.Empty,
            message + " Segment: " + mSegmentName);
    }

    /// <summary>按 accessor、map 顺序释放 OS 资源；保留小型 header 缓冲区供同代重连复用。</summary>
    private void ReleaseMap()
    {
        try
        {
            mAccessor?.Dispose();
        }
        finally
        {
            mAccessor = null;
            mMemoryMap?.Dispose();
            mMemoryMap = null;
        }
    }

    /// <summary>拒绝 lease 释放后的再次读取，避免把生命周期错误伪装成 segment 不可用。</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(mDisposed, this);
    }

    /// <summary>释放映射并归还 header 缓冲区；重复调用保持幂等。</summary>
    public void Dispose()
    {
        if (mDisposed)
        {
            return;
        }

        mDisposed = true;
        ReleaseMap();
        ReturnHeaderBuffer(ref mFirstHeaderBytes);
        ReturnHeaderBuffer(ref mSecondHeaderBytes);
    }

    /// <summary>归还单个可选 ArrayPool 缓冲区并清空字段，防止重复归还。</summary>
    /// <param name="buffer">待归还的字段引用。</param>
    private static void ReturnHeaderBuffer(ref byte[]? buffer)
    {
        var rented = buffer;
        buffer = null;
        if (rented != null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
