using YokiFrame.Tooling.Application.Models.Capabilities;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 封装能力目录、总体状态和严格模式失败时需要保留的证据路径。
/// </summary>
public sealed class CapabilityCatalogResult
{
    /// <summary>创建能力目录结果。</summary>
    public CapabilityCatalogResult(
        string state,
        CapabilityCatalog catalog,
        IReadOnlyList<string> evidencePaths)
    {
        State = state;
        Catalog = catalog;
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>获取 Ready、Partial、Drifted 或 Blocked 状态。</summary>
    public string State { get; }

    /// <summary>获取结构化能力目录。</summary>
    public CapabilityCatalog Catalog { get; }

    /// <summary>获取所有非空且去重的证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>获取目录是否没有缺失、过期或漂移问题。</summary>
    public bool IsReady => string.Equals(State, "Ready", StringComparison.Ordinal);
}
