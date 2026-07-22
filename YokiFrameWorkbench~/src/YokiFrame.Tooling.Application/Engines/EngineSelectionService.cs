using YokiFrame.Client;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Tooling.Application.Engines;

/// <summary>
/// 解析工具调用应使用的 engine，避免多宿主环境下静默把命令发给固定引擎。
/// </summary>
public sealed class EngineSelectionService
{
    private readonly IYokiFrameClient mClient;

    /// <summary>
    /// 获取 engine 在线判定使用的 heartbeat stale 阈值。
    /// </summary>
    public static TimeSpan HeartbeatStaleThreshold { get; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 使用统一 Client 创建 engine 选择服务。
    /// </summary>
    /// <param name="client">用于读取 registry 和 heartbeat 的 Client。</param>
    public EngineSelectionService(IYokiFrameClient client)
    {
        mClient = client;
    }

    /// <summary>
    /// 解析目标 engine；显式选择优先，否则只自动选择唯一在线 engine。
    /// </summary>
    /// <param name="requestedEngineId">调用方显式指定的 engine；为空时自动发现。</param>
    /// <param name="nowUtc">当前 UTC 时间，用于稳定判断 heartbeat 是否过期。</param>
    /// <returns>已解析的安全 engine 标识。</returns>
    public string Resolve(string? requestedEngineId, DateTimeOffset nowUtc)
    {
        return ResolveSelectedEngine(Select(requestedEngineId, nowUtc));
    }

    /// <summary>
    /// 选择目标 engine，并在无法自动选择时保留 Workbench 恢复所需的候选和标准错误。
    /// </summary>
    /// <param name="requestedEngineId">调用方显式指定的 engine；为空时自动发现。</param>
    /// <param name="nowUtc">当前 UTC 时间，用于稳定判断 heartbeat 是否过期。</param>
    /// <returns>不会通过预期选择失败抛异常的选择结果。</returns>
    public EngineSelectionResult Select(string? requestedEngineId, DateTimeOffset nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(requestedEngineId))
        {
            return EngineSelectionResult.CreateSelected(ResolveExplicitEngineId(requestedEngineId));
        }

        var entries = mClient.ReadEngineEntries();
        return Select(string.Empty, entries, nowUtc);
    }

    /// <summary>
    /// 使用调用方已读取的 registry 解析目标 engine，避免 Dashboard 重复枚举目录。
    /// </summary>
    /// <param name="requestedEngineId">调用方显式指定的 engine；为空时自动发现。</param>
    /// <param name="entries">当前 registry 条目。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>已解析的安全 engine 标识。</returns>
    internal string Resolve(
        string? requestedEngineId,
        IReadOnlyList<EngineRegistryEntry> entries,
        DateTimeOffset nowUtc)
    {
        return ResolveSelectedEngine(Select(requestedEngineId, entries, nowUtc));
    }

    /// <summary>
    /// 使用调用方已读取的 registry 选择目标 engine，供 Dashboard 保留候选并避免重复枚举。
    /// </summary>
    /// <param name="requestedEngineId">调用方显式指定的 engine；为空时自动发现。</param>
    /// <param name="entries">当前 registry 条目。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>engine 选择结果。</returns>
    internal EngineSelectionResult Select(
        string? requestedEngineId,
        IReadOnlyList<EngineRegistryEntry> entries,
        DateTimeOffset nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(requestedEngineId))
        {
            return EngineSelectionResult.CreateSelected(ResolveExplicitEngineId(requestedEngineId));
        }

        var onlineEngineIds = FindOnlineEngineIds(entries, nowUtc);
        if (onlineEngineIds.Count == 1)
        {
            return EngineSelectionResult.CreateSelected(onlineEngineIds[0], onlineEngineIds);
        }

        var status = onlineEngineIds.Count == 0
            ? EngineSelectionStatus.Unavailable
            : EngineSelectionStatus.SelectionRequired;
        return EngineSelectionResult.CreatePending(status, onlineEngineIds, CreateSelectionError(onlineEngineIds));
    }

    /// <summary>
    /// 校验调用方明确指定的 engine 标识；显式诊断不依赖 registry 是否可读。
    /// </summary>
    /// <param name="requestedEngineId">调用方明确指定的 engine 标识。</param>
    /// <returns>通过安全校验的 engine 标识。</returns>
    private static string ResolveExplicitEngineId(string requestedEngineId)
    {
        return SafeIdValidator.EnsureSafeId(requestedEngineId, nameof(requestedEngineId));
    }

    /// <summary>
    /// 根据 registry 和 heartbeat 找出当前在线 engine，只读取轻量 heartbeat 而不扫描完整协议目录。
    /// </summary>
    /// <param name="entries">当前 registry 条目。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>按 engine 标识排序的在线列表。</returns>
    private IReadOnlyList<string> FindOnlineEngineIds(
        IReadOnlyList<EngineRegistryEntry> entries,
        DateTimeOffset nowUtc)
    {
        HashSet<string> visitedEngineIds = new(StringComparer.Ordinal);
        List<string> onlineEngineIds = new();
        foreach (var entry in entries)
        {
            if (!visitedEngineIds.Add(entry.EngineId))
            {
                continue;
            }

            var heartbeat = mClient.ReadHeartbeat(entry.EngineId);
            if (heartbeat != null && !heartbeat.IsStale(nowUtc, HeartbeatStaleThreshold))
            {
                onlineEngineIds.Add(entry.EngineId);
            }
        }

        onlineEngineIds.Sort(StringComparer.Ordinal);
        return onlineEngineIds;
    }

    /// <summary>
    /// 把可恢复选择结果转换为异常式调用需要的 engine 标识。
    /// </summary>
    /// <param name="result">engine 选择结果。</param>
    /// <returns>已选择的 engine 标识。</returns>
    private static string ResolveSelectedEngine(EngineSelectionResult result)
    {
        if (result.IsSelected)
        {
            return result.SelectedEngineId;
        }

        throw new YokiFrameProtocolException(result.Error!);
    }

    /// <summary>
    /// 创建无法自动选择 engine 时的标准错误，并保留 registry 根目录作为诊断证据。
    /// </summary>
    /// <param name="onlineEngineIds">当前在线 engine 标识。</param>
    /// <returns>标准错误。</returns>
    private YokiFrameError CreateSelectionError(IReadOnlyList<string> onlineEngineIds)
    {
        if (onlineEngineIds.Count == 0)
        {
            return new YokiFrameError(
                "EngineUnavailable",
                "No online engine is available.",
                "Start an engine adapter or pass --engine to inspect a known engine explicitly.",
                new[] { mClient.Paths.EnginesRoot });
        }

        var engineList = string.Join(", ", onlineEngineIds);
        return new YokiFrameError(
            "EngineSelectionRequired",
            "Multiple engines are online: " + engineList + ".",
            "Pass --engine with one of the online engine ids.",
            new[] { mClient.Paths.EnginesRoot });
    }
}
