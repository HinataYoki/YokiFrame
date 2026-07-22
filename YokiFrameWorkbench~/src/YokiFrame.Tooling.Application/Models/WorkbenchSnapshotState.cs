namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench 中单个 Kit snapshot 的读取状态。
/// </summary>
public sealed class WorkbenchSnapshotState
{
    /// <summary>
    /// 创建 snapshot 状态。
    /// </summary>
    /// <param name="kit">Kit 名称。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <param name="path">snapshot 文件路径。</param>
    /// <param name="source">当前数据来源，例如 telemetry 或 snapshot。</param>
    /// <param name="exists">文件是否存在并成功读取。</param>
    /// <param name="payloadPreview">payload 或 JSON 摘要。</param>
    /// <param name="errorMessage">读取失败说明。</param>
    /// <param name="rawPayloadJson">未经裁剪的业务 payload。</param>
    /// <param name="updatedAtUtc">源数据更新时间；不可用时为空。</param>
    /// <param name="staleReason">数据回落或陈旧原因。</param>
    /// <param name="evidencePaths">本次读取尝试涉及的全部证据标识。</param>
    public WorkbenchSnapshotState(
        string kit,
        string name,
        string path,
        string source,
        bool exists,
        string payloadPreview,
        string errorMessage,
        string rawPayloadJson = "",
        DateTimeOffset? updatedAtUtc = null,
        string staleReason = "",
        IReadOnlyList<string>? evidencePaths = null)
    {
        Kit = kit;
        Name = name;
        Path = path;
        Source = source;
        Exists = exists;
        PayloadPreview = payloadPreview;
        ErrorMessage = errorMessage;
        RawPayloadJson = rawPayloadJson;
        UpdatedAtUtc = updatedAtUtc;
        StaleReason = staleReason;
        EvidencePaths = evidencePaths?.Where(static evidence => !string.IsNullOrWhiteSpace(evidence)).ToArray()
            ?? (string.IsNullOrWhiteSpace(path) ? Array.Empty<string>() : new[] { path });
    }

    /// <summary>
    /// 获取 Kit 名称。
    /// </summary>
    public string Kit { get; }

    /// <summary>
    /// 获取 snapshot 名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取 snapshot 文件路径。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 获取当前数据来源，例如 telemetry 或 snapshot。
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// 获取是否成功读取。
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// 获取 payload 或 JSON 摘要。
    /// </summary>
    public string PayloadPreview { get; }

    /// <summary>
    /// 获取读取失败说明。
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// 获取未经裁剪的业务 payload，供 Application 内部构建强类型 Kit 状态。
    /// </summary>
    public string RawPayloadJson { get; }

    /// <summary>
    /// 获取 snapshot 或 telemetry 的源更新时间；不可用时为空。
    /// </summary>
    public DateTimeOffset? UpdatedAtUtc { get; }

    /// <summary>
    /// 获取数据回落或陈旧原因；正常读取时为空。
    /// </summary>
    public string StaleReason { get; }

    /// <summary>
    /// 获取本次读取尝试涉及的 telemetry segment、snapshot 或其它证据标识。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
