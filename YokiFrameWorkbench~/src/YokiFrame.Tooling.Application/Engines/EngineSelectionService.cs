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
    private readonly IEngineStateReader mStateReader;

    /// <summary>
    /// 获取 engine 在线判定使用的 heartbeat stale 阈值。
    /// </summary>
    public static TimeSpan HeartbeatStaleThreshold { get; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 使用统一 Client 创建 engine 选择服务。
    /// </summary>
    /// <param name="client">用于读取 registry 和 heartbeat 的 Client。</param>
    public EngineSelectionService(IEngineStateReader stateReader)
    {
        mStateReader = stateReader;
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

        try
        {
            var entries = mStateReader.ReadEngineEntries();
            return Select(string.Empty, entries, nowUtc);
        }
        catch (EngineRegistryReadException exception)
        {
            var result = Select(string.Empty, exception.ValidEntries, nowUtc);
            return result.WithAdditionalDiagnostics(new[]
            {
                new EngineSessionDiagnostic(
                    "EngineRegistryPartialRead",
                    exception.Message,
                    null,
                    exception.InvalidPaths)
            });
        }
        catch (Exception exception)
        {
            return Select(string.Empty, Array.Empty<EngineRegistryEntry>(), nowUtc)
                .WithAdditionalDiagnostics(new[]
                {
                    new EngineSessionDiagnostic(
                        "EngineRegistryReadFailed",
                        exception.Message,
                        null,
                        new[] { mStateReader.Paths.EnginesRoot })
                });
        }
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
        return Select(requestedEngineId, entries, nowUtc, null);
    }

    /// <summary>
    /// 使用已读取的 heartbeat 选择目标 engine，避免 Dashboard 在同一轮重复读取文件。
    /// </summary>
    /// <param name="requestedEngineId">调用方显式指定的 engine；为空时自动发现。</param>
    /// <param name="entries">当前 registry 条目。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <param name="heartbeats">按 engine 标识保存的 heartbeat；空值表示缺失。</param>
    /// <returns>engine 选择结果和局部诊断。</returns>
    internal EngineSelectionResult Select(
        string? requestedEngineId,
        IReadOnlyList<EngineRegistryEntry> entries,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, HeartbeatInfo?>? heartbeats)
    {
        if (!string.IsNullOrWhiteSpace(requestedEngineId))
        {
            return EngineSelectionResult.CreateSelected(ResolveExplicitEngineId(requestedEngineId));
        }

        var onlineEngineIds = FindOnlineEngineIds(entries, nowUtc, heartbeats, out var diagnostics);
        if (onlineEngineIds.Count == 1)
        {
            return EngineSelectionResult.CreateSelected(onlineEngineIds[0], onlineEngineIds, diagnostics);
        }

        var status = onlineEngineIds.Count == 0
            ? EngineSelectionStatus.Unavailable
            : EngineSelectionStatus.SelectionRequired;
        return EngineSelectionResult.CreatePending(
            status,
            onlineEngineIds,
            CreateSelectionError(onlineEngineIds),
            diagnostics);
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
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, HeartbeatInfo?>? heartbeats,
        out IReadOnlyList<EngineSessionDiagnostic> diagnostics)
    {
        HashSet<string> visitedEngineIds = new(StringComparer.Ordinal);
        List<string> onlineEngineIds = new();
        List<EngineSessionDiagnostic> localDiagnostics = new();
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.EngineId) || !visitedEngineIds.Add(entry.EngineId))
            {
                continue;
            }

            HeartbeatInfo? heartbeat;
            if (heartbeats != null)
            {
                if (!heartbeats.TryGetValue(entry.EngineId, out heartbeat))
                {
                    localDiagnostics.Add(new EngineSessionDiagnostic(
                        "HeartbeatMissing",
                        "Engine heartbeat was not found.",
                        entry.EngineId,
                        new[] { mStateReader.Paths.GetHeartbeatPath(entry.EngineId) }));
                    continue;
                }

                // Coordinator 已经为 null heartbeat 记录了精确的缺失或解析诊断，避免在选择阶段重复包装。
                if (heartbeat == null)
                {
                    continue;
                }
            }
            else
            {
                try
                {
                    heartbeat = mStateReader.ReadHeartbeat(entry.EngineId);
                }
                catch (Exception exception)
                {
                    localDiagnostics.Add(new EngineSessionDiagnostic(
                        "HeartbeatReadFailed",
                        exception.Message,
                        entry.EngineId,
                        new[] { mStateReader.Paths.GetHeartbeatPath(entry.EngineId) }));
                    continue;
                }
            }

            if (heartbeat == null)
            {
                localDiagnostics.Add(new EngineSessionDiagnostic(
                    "HeartbeatMissing",
                    "Engine heartbeat was not found.",
                    entry.EngineId,
                    new[] { mStateReader.Paths.GetHeartbeatPath(entry.EngineId) }));
                continue;
            }

            if (EngineHostIdentity.HasMismatch(entry, heartbeat))
            {
                localDiagnostics.Add(new EngineSessionDiagnostic(
                    "HostIdentityMismatch",
                    "Engine registry and heartbeat refer to different host identities.",
                    entry.EngineId,
                    new[] { heartbeat.Path, mStateReader.Paths.GetEngineRoot(entry.EngineId) }));
                continue;
            }

            if (heartbeat.IsStale(nowUtc, HeartbeatStaleThreshold))
            {
                localDiagnostics.Add(new EngineSessionDiagnostic(
                    "HeartbeatStale",
                    "Engine heartbeat is stale.",
                    entry.EngineId,
                    new[] { heartbeat.Path }));
                continue;
            }

            onlineEngineIds.Add(entry.EngineId);
        }

        onlineEngineIds.Sort(StringComparer.Ordinal);
        diagnostics = localDiagnostics;
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
                new[] { mStateReader.Paths.EnginesRoot });
        }

        var engineList = string.Join(", ", onlineEngineIds);
        return new YokiFrameError(
            "EngineSelectionRequired",
            "Multiple engines are online: " + engineList + ".",
            "Pass --engine with one of the online engine ids.",
            new[] { mStateReader.Paths.EnginesRoot });
    }
}
