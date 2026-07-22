using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Tooling.Application.Models.EventKit;

/// <summary>表示一次 EventKit 高频 Telemetry 读取的明确结果类别。</summary>
public enum WorkbenchEventKitTelemetryReadStatus
{
    /// <summary>读到并解析出可提交的新帧。</summary>
    Accepted,
    /// <summary>Shared Memory header 与调用方游标相同。</summary>
    Unchanged,
    /// <summary>writer 正在提交帧，应在下一高频周期重试。</summary>
    Retryable,
    /// <summary>目标段尚不存在或当前平台不可读取。</summary>
    Unavailable,
    /// <summary>帧协议、JSON 或宿主身份不满足要求。</summary>
    Rejected
}

/// <summary>描述一次 EventKit Telemetry 轮询结果和已检查帧游标。</summary>
public sealed class WorkbenchEventKitTelemetryReadResult
{
    private const int MAX_DIAGNOSTIC_LENGTH = 512;
    private static readonly WorkbenchEventKitTelemetryReadResult sUnchanged = new(
        WorkbenchEventKitTelemetryReadStatus.Unchanged,
        null,
        long.MinValue,
        false,
        string.Empty);

    /// <summary>创建内部受控的 EventKit Telemetry 结果。</summary>
    private WorkbenchEventKitTelemetryReadResult(
        WorkbenchEventKitTelemetryReadStatus status,
        WorkbenchEventKitState? state,
        long sequence,
        bool hasCursor,
        string diagnostic)
    {
        Status = status;
        State = state;
        Sequence = sequence;
        HasCursor = hasCursor;
        Diagnostic = NormalizeDiagnostic(diagnostic);
    }

    /// <summary>获取本次轮询的结果类别。</summary>
    public WorkbenchEventKitTelemetryReadStatus Status { get; }
    /// <summary>获取已接受的新状态。</summary>
    public WorkbenchEventKitState? State { get; }
    /// <summary>获取可信帧 sequence。</summary>
    public long Sequence { get; }
    /// <summary>获取结果是否携带可推进的可信游标。</summary>
    public bool HasCursor { get; }
    /// <summary>获取有界诊断文本。</summary>
    public string Diagnostic { get; }

    /// <summary>创建完整校验通过的新帧结果。</summary>
    internal static WorkbenchEventKitTelemetryReadResult Accepted(
        WorkbenchEventKitState state,
        SharedMemoryTelemetryFrameHeader header)
    {
        return new WorkbenchEventKitTelemetryReadResult(
            WorkbenchEventKitTelemetryReadStatus.Accepted,
            state,
            header.Sequence,
            true,
            string.Empty);
    }

    /// <summary>创建 header 未变化的空闲结果。</summary>
    internal static WorkbenchEventKitTelemetryReadResult Unchanged()
    {
        return sUnchanged;
    }

    /// <summary>创建不推进游标的瞬态写入结果。</summary>
    internal static WorkbenchEventKitTelemetryReadResult Retryable(string diagnostic)
    {
        return CreateWithoutCursor(WorkbenchEventKitTelemetryReadStatus.Retryable, diagnostic);
    }

    /// <summary>创建目标段不可读取的降频结果。</summary>
    internal static WorkbenchEventKitTelemetryReadResult Unavailable(string diagnostic)
    {
        return CreateWithoutCursor(WorkbenchEventKitTelemetryReadStatus.Unavailable, diagnostic);
    }

    /// <summary>创建带可信 header 的 parser 拒绝结果。</summary>
    internal static WorkbenchEventKitTelemetryReadResult RejectedWithTrustedCursor(
        SharedMemoryTelemetryFrameHeader header,
        string diagnostic)
    {
        return new WorkbenchEventKitTelemetryReadResult(
            WorkbenchEventKitTelemetryReadStatus.Rejected,
            null,
            header.Sequence,
            true,
            diagnostic);
    }

    /// <summary>创建没有可信游标的协议拒绝结果。</summary>
    internal static WorkbenchEventKitTelemetryReadResult Rejected(string diagnostic)
    {
        return CreateWithoutCursor(WorkbenchEventKitTelemetryReadStatus.Rejected, diagnostic);
    }

    /// <summary>创建不携带状态与游标的轮询结果。</summary>
    private static WorkbenchEventKitTelemetryReadResult CreateWithoutCursor(
        WorkbenchEventKitTelemetryReadStatus status,
        string diagnostic)
    {
        return new WorkbenchEventKitTelemetryReadResult(
            status,
            null,
            long.MinValue,
            false,
            diagnostic);
    }

    /// <summary>限制异常文本长度，避免长期轮询状态被损坏 payload 撑大。</summary>
    private static string NormalizeDiagnostic(string diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic) || diagnostic.Length <= MAX_DIAGNOSTIC_LENGTH)
        {
            return diagnostic ?? string.Empty;
        }

        return diagnostic[..MAX_DIAGNOSTIC_LENGTH];
    }
}
