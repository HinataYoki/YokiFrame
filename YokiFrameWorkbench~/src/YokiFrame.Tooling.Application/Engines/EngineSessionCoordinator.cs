using YokiFrame.Client;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Engines;

/// <summary>
/// 一次性读取 registry 与 heartbeat，并生成可供 Dashboard、Doctor 和命令用例复用的会话快照。
/// </summary>
public sealed class EngineSessionCoordinator
{
    private readonly IEngineStateReader mStateReader;
    private readonly EngineSelectionService mSelectionService;

    /// <summary>
    /// 使用统一 Client 创建引擎会话协调器。
    /// </summary>
    /// <param name="client">用于读取 registry 与 heartbeat 的 Client。</param>
    public EngineSessionCoordinator(IEngineStateReader stateReader)
    {
        mStateReader = stateReader;
        mSelectionService = new EngineSelectionService(stateReader);
    }

    /// <summary>
    /// 读取并校验一次引擎会话；单个坏文件只产生局部诊断，不阻断其它条目。
    /// </summary>
    /// <param name="requestedEngineId">调用方显式选择的 engine；为空时自动发现。</param>
    /// <param name="nowUtc">本轮 stale 判定时间。</param>
    /// <returns>包含候选、heartbeat、身份和局部诊断的会话快照。</returns>
    public EngineSessionSnapshot Read(string? requestedEngineId, DateTimeOffset nowUtc)
    {
        List<EngineSessionDiagnostic> diagnostics = new();
        IReadOnlyList<EngineRegistryEntry> entries = ReadEngineEntries(diagnostics);
        Dictionary<string, HeartbeatInfo?> heartbeats = ReadHeartbeats(
            entries,
            requestedEngineId,
            diagnostics);
        var selection = mSelectionService.Select(requestedEngineId, entries, nowUtc, heartbeats);
        diagnostics.AddRange(selection.Diagnostics);
        return new EngineSessionSnapshot(nowUtc, entries, selection, heartbeats, diagnostics);
    }

    /// <summary>
    /// 读取 registry，并在部分文件损坏时保留有效条目。
    /// </summary>
    /// <param name="diagnostics">诊断收集器。</param>
    /// <returns>成功解析的 registry 条目。</returns>
    private IReadOnlyList<EngineRegistryEntry> ReadEngineEntries(List<EngineSessionDiagnostic> diagnostics)
    {
        try
        {
            return mStateReader.ReadEngineEntries();
        }
        catch (EngineRegistryReadException exception)
        {
            diagnostics.Add(new EngineSessionDiagnostic(
                "EngineRegistryPartialRead",
                exception.Message,
                null,
                exception.InvalidPaths));
            return exception.ValidEntries;
        }
        catch (Exception exception)
        {
            diagnostics.Add(new EngineSessionDiagnostic(
                "EngineRegistryReadFailed",
                exception.Message,
                null,
                new[] { mStateReader.Paths.EnginesRoot }));
            return Array.Empty<EngineRegistryEntry>();
        }
    }

    /// <summary>
    /// 按有效 registry 条目读取 heartbeat；异常只影响对应 engine。
    /// </summary>
    /// <param name="entries">成功解析的 registry 条目。</param>
    /// <param name="diagnostics">诊断收集器。</param>
    /// <returns>按 engine 标识保存的 heartbeat 快照。</returns>
    private Dictionary<string, HeartbeatInfo?> ReadHeartbeats(
        IReadOnlyList<EngineRegistryEntry> entries,
        string? requestedEngineId,
        List<EngineSessionDiagnostic> diagnostics)
    {
        Dictionary<string, HeartbeatInfo?> heartbeats = new(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(requestedEngineId)
                && !string.Equals(entry.EngineId, requestedEngineId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.EngineId) || heartbeats.ContainsKey(entry.EngineId))
            {
                continue;
            }

            try
            {
                var heartbeat = mStateReader.ReadHeartbeat(entry.EngineId);
                heartbeats.Add(entry.EngineId, heartbeat);
                if (heartbeat == null)
                {
                    diagnostics.Add(new EngineSessionDiagnostic(
                        "HeartbeatMissing",
                        "Engine heartbeat was not found.",
                        entry.EngineId,
                        new[] { mStateReader.Paths.GetHeartbeatPath(entry.EngineId) }));
                }
            }
            catch (Exception exception)
            {
                heartbeats[entry.EngineId] = null;
                diagnostics.Add(new EngineSessionDiagnostic(
                    "HeartbeatReadFailed",
                    exception.Message,
                    entry.EngineId,
                    new[] { mStateReader.Paths.GetHeartbeatPath(entry.EngineId) }));
            }
        }

        return heartbeats;
    }
}
