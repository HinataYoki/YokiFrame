using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>表示一次 LogKit 高频 telemetry 读取的结果类别。</summary>
public enum WorkbenchLogKitTelemetryReadStatus
{
    /// <summary>读到并解析出可提交的新帧。</summary>
    Accepted,
    /// <summary>Shared Memory header 与调用方游标相同。</summary>
    Unchanged,
    /// <summary>writer 正在提交帧，应在下一周期重试。</summary>
    Retryable,
    /// <summary>目标段不存在或当前平台不可读取。</summary>
    Unavailable,
    /// <summary>帧协议、JSON 或宿主身份不满足要求。</summary>
    Rejected
}

/// <summary>描述一次 LogKit telemetry 轮询结果和可信游标。</summary>
public sealed class WorkbenchLogKitTelemetryReadResult
{
    private const int MAX_DIAGNOSTIC_LENGTH = 512;
    private static readonly WorkbenchLogKitTelemetryReadResult sUnchanged = new(
        WorkbenchLogKitTelemetryReadStatus.Unchanged,
        null,
        long.MinValue,
        false,
        string.Empty);

    /// <summary>创建内部受控结果。</summary>
    private WorkbenchLogKitTelemetryReadResult(
        WorkbenchLogKitTelemetryReadStatus status,
        WorkbenchLogKitState? state,
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

    /// <summary>获取本次轮询类别。</summary>
    public WorkbenchLogKitTelemetryReadStatus Status { get; }
    /// <summary>获取已接受的新状态。</summary>
    public WorkbenchLogKitState? State { get; }
    /// <summary>获取可信帧 sequence。</summary>
    public long Sequence { get; }
    /// <summary>获取是否携带可推进游标。</summary>
    public bool HasCursor { get; }
    /// <summary>获取有界诊断文本。</summary>
    public string Diagnostic { get; }

    /// <summary>创建完整校验通过的新帧结果。</summary>
    internal static WorkbenchLogKitTelemetryReadResult Accepted(
        WorkbenchLogKitState state,
        SharedMemoryTelemetryFrameHeader header)
    {
        return new WorkbenchLogKitTelemetryReadResult(
            WorkbenchLogKitTelemetryReadStatus.Accepted,
            state,
            header.Sequence,
            true,
            string.Empty);
    }

    /// <summary>创建 header 未变化的结果。</summary>
    internal static WorkbenchLogKitTelemetryReadResult Unchanged()
    {
        return sUnchanged;
    }

    /// <summary>创建不推进游标的瞬态结果。</summary>
    internal static WorkbenchLogKitTelemetryReadResult Retryable(string diagnostic)
    {
        return CreateWithoutCursor(WorkbenchLogKitTelemetryReadStatus.Retryable, diagnostic);
    }

    /// <summary>创建不可读取结果。</summary>
    internal static WorkbenchLogKitTelemetryReadResult Unavailable(string diagnostic)
    {
        return CreateWithoutCursor(WorkbenchLogKitTelemetryReadStatus.Unavailable, diagnostic);
    }

    /// <summary>创建带可信 header 的拒绝结果。</summary>
    internal static WorkbenchLogKitTelemetryReadResult RejectedWithTrustedCursor(
        SharedMemoryTelemetryFrameHeader header,
        string diagnostic)
    {
        return new WorkbenchLogKitTelemetryReadResult(
            WorkbenchLogKitTelemetryReadStatus.Rejected,
            null,
            header.Sequence,
            true,
            diagnostic);
    }

    /// <summary>创建没有可信游标的拒绝结果。</summary>
    internal static WorkbenchLogKitTelemetryReadResult Rejected(string diagnostic)
    {
        return CreateWithoutCursor(WorkbenchLogKitTelemetryReadStatus.Rejected, diagnostic);
    }

    /// <summary>创建不携带状态和游标的结果。</summary>
    private static WorkbenchLogKitTelemetryReadResult CreateWithoutCursor(
        WorkbenchLogKitTelemetryReadStatus status,
        string diagnostic)
    {
        return new WorkbenchLogKitTelemetryReadResult(status, null, long.MinValue, false, diagnostic);
    }

    /// <summary>限制诊断长度，避免损坏 payload 撑大长期状态。</summary>
    private static string NormalizeDiagnostic(string diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic) || diagnostic.Length <= MAX_DIAGNOSTIC_LENGTH)
        {
            return diagnostic ?? string.Empty;
        }

        return diagnostic[..MAX_DIAGNOSTIC_LENGTH];
    }
}
