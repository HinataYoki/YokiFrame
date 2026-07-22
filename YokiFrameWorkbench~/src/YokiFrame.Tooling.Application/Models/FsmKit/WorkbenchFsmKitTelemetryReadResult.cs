using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>表示一次 FsmKit 高频 Telemetry 读取的明确结果类别。</summary>
public enum WorkbenchFsmKitTelemetryReadStatus
{
    /// <summary>读到并解析出可提交的新详情帧。</summary>
    Accepted,

    /// <summary>Shared Memory header 与调用方游标相同，无需读取 payload。</summary>
    Unchanged,

    /// <summary>writer 正在提交帧，调用方应保留原游标并在下一次高频周期重试。</summary>
    Retryable,

    /// <summary>目标命名段尚不存在或当前平台不可读取。</summary>
    Unavailable,

    /// <summary>帧已写入但协议、JSON 或实例身份不满足当前读取要求。</summary>
    Rejected
}

/// <summary>描述一次 FsmKit Telemetry 轮询结果、可选状态及已检查的 Shared Memory 游标。</summary>
public sealed class WorkbenchFsmKitTelemetryReadResult
{
    private const int MAX_DIAGNOSTIC_LENGTH = 512;
    private static readonly WorkbenchFsmKitTelemetryReadResult sUnchanged = new(
        WorkbenchFsmKitTelemetryReadStatus.Unchanged,
        null,
        long.MinValue,
        long.MinValue,
        false,
        string.Empty);

    /// <summary>创建内部受控的 Telemetry 读取结果。</summary>
    private WorkbenchFsmKitTelemetryReadResult(
        WorkbenchFsmKitTelemetryReadStatus status,
        WorkbenchFsmKitState? state,
        long sequence,
        long writtenAtUtcTicks,
        bool hasCursor,
        string diagnostic)
    {
        Status = status;
        State = state;
        Sequence = sequence;
        WrittenAtUtcTicks = writtenAtUtcTicks;
        HasCursor = hasCursor;
        Diagnostic = NormalizeDiagnostic(diagnostic);
    }

    /// <summary>获取本次轮询的稳定结果类别。</summary>
    public WorkbenchFsmKitTelemetryReadStatus Status { get; }

    /// <summary>获取已接受的强类型状态；其它结果为空。</summary>
    public WorkbenchFsmKitState? State { get; }

    /// <summary>获取本次已检查帧的 sequence；没有 header 时为最小值。</summary>
    public long Sequence { get; }

    /// <summary>获取本次已检查帧的写入时间；没有 header 时为最小值。</summary>
    public long WrittenAtUtcTicks { get; }

    /// <summary>获取结果是否携带可推进负向游标的稳定 header。</summary>
    public bool HasCursor { get; }

    /// <summary>获取 unavailable 或 rejected 的有界诊断文本。</summary>
    public string Diagnostic { get; }

    /// <summary>创建已经通过协议、身份和 parser 校验的新帧结果。</summary>
    internal static WorkbenchFsmKitTelemetryReadResult Accepted(
        WorkbenchFsmKitState state,
        SharedMemoryTelemetryFrameHeader header)
    {
        return new WorkbenchFsmKitTelemetryReadResult(
            WorkbenchFsmKitTelemetryReadStatus.Accepted,
            state,
            header.Sequence,
            header.WrittenAtUtcTicks,
            true,
            string.Empty);
    }

    /// <summary>创建 header 未变化的空闲轮询结果。</summary>
    internal static WorkbenchFsmKitTelemetryReadResult Unchanged()
    {
        return sUnchanged;
    }

    /// <summary>创建不推进游标的瞬态写入结果，下一次 100ms 周期可以继续读取同一帧。</summary>
    /// <param name="diagnostic">Writing 或 HalfWrite 诊断。</param>
    internal static WorkbenchFsmKitTelemetryReadResult Retryable(string diagnostic)
    {
        return new WorkbenchFsmKitTelemetryReadResult(
            WorkbenchFsmKitTelemetryReadStatus.Retryable,
            null,
            long.MinValue,
            long.MinValue,
            false,
            diagnostic);
    }

    /// <summary>创建没有可读命名段的降频结果。</summary>
    /// <param name="diagnostic">Client 返回的有界失败说明。</param>
    internal static WorkbenchFsmKitTelemetryReadResult Unavailable(string diagnostic)
    {
        return new WorkbenchFsmKitTelemetryReadResult(
            WorkbenchFsmKitTelemetryReadStatus.Unavailable,
            null,
            long.MinValue,
            long.MinValue,
            false,
            diagnostic);
    }

    /// <summary>创建携带可信 header 的拒绝结果，使调用方不重复解析同一坏 payload。</summary>
    /// <param name="header">底层协议与宿主身份已经完整校验的帧 header。</param>
    /// <param name="diagnostic">parser 或实例身份拒绝原因。</param>
    internal static WorkbenchFsmKitTelemetryReadResult RejectedWithTrustedCursor(
        SharedMemoryTelemetryFrameHeader header,
        string diagnostic)
    {
        return new WorkbenchFsmKitTelemetryReadResult(
            WorkbenchFsmKitTelemetryReadStatus.Rejected,
            null,
            header.Sequence,
            header.WrittenAtUtcTicks,
            true,
            diagnostic);
    }

    /// <summary>创建不携带游标的协议拒绝结果，避免损坏或错误宿主 header 污染 sequence 上界。</summary>
    /// <param name="diagnostic">底层协议拒绝原因。</param>
    internal static WorkbenchFsmKitTelemetryReadResult Rejected(string diagnostic)
    {
        return new WorkbenchFsmKitTelemetryReadResult(
            WorkbenchFsmKitTelemetryReadStatus.Rejected,
            null,
            long.MinValue,
            long.MinValue,
            false,
            diagnostic);
    }

    /// <summary>限制诊断长度，避免损坏 payload 的异常文本进入长期 UI 轮询状态。</summary>
    /// <param name="diagnostic">原始诊断文本。</param>
    /// <returns>空值归一化且不超过固定上限的文本。</returns>
    private static string NormalizeDiagnostic(string diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic) || diagnostic.Length <= MAX_DIAGNOSTIC_LENGTH)
        {
            return diagnostic ?? string.Empty;
        }

        return diagnostic[..MAX_DIAGNOSTIC_LENGTH];
    }
}
