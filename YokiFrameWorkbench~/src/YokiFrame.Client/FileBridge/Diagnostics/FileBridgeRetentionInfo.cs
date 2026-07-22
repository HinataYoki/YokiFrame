using System.Text.Json.Nodes;

namespace YokiFrame.Client.FileBridge.Diagnostics;

/// <summary>
/// 描述 FileBridge 证据目录的保留策略。
/// </summary>
public sealed class FileBridgeRetentionInfo
{
    /// <summary>
    /// 创建证据保留策略描述。
    /// </summary>
    /// <param name="archive">archive 目录保留策略。</param>
    /// <param name="deadletter">deadletter 目录保留策略。</param>
    /// <param name="results">results 目录保留策略。</param>
    /// <param name="cleanup">清理触发策略。</param>
    public FileBridgeRetentionInfo(string archive, string deadletter, string results, string cleanup)
    {
        Archive = archive;
        Deadletter = deadletter;
        Results = results;
        Cleanup = cleanup;
    }

    /// <summary>
    /// 获取 archive 目录保留策略。
    /// </summary>
    public string Archive { get; }

    /// <summary>
    /// 获取 deadletter 目录保留策略。
    /// </summary>
    public string Deadletter { get; }

    /// <summary>
    /// 获取 results 目录保留策略。
    /// </summary>
    public string Results { get; }

    /// <summary>
    /// 获取清理触发策略。
    /// </summary>
    public string Cleanup { get; }

    /// <summary>
    /// 创建当前 FileBridge 默认的手动保留策略。
    /// </summary>
    /// <returns>手动保留策略。</returns>
    public static FileBridgeRetentionInfo CreateManual()
    {
        return new FileBridgeRetentionInfo("manual", "manual", "manual", "explicit-maintenance");
    }

    /// <summary>
    /// 转换为 CLI compact JSON 输出。
    /// </summary>
    /// <returns>保留策略 JSON。</returns>
    public JsonObject ToJson()
    {
        return new JsonObject
        {
            ["archive"] = Archive,
            ["deadletter"] = Deadletter,
            ["results"] = Results,
            ["cleanup"] = Cleanup
        };
    }
}
