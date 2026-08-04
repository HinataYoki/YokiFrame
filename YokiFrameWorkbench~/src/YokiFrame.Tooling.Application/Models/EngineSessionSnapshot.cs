using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Engines;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述一次完整的 engine registry/heartbeat 发现结果，作为 Application 层唯一的会话身份输入。
/// </summary>
public sealed class EngineSessionSnapshot
{
    /// <summary>
    /// 创建引擎会话快照。
    /// </summary>
    /// <param name="generatedAtUtc">快照生成时间。</param>
    /// <param name="engines">成功解析的 registry 条目。</param>
    /// <param name="selection">当前请求的 engine 选择结果。</param>
    /// <param name="heartbeats">按 engine 标识保存的 heartbeat；缺失 heartbeat 保存为空值。</param>
    /// <param name="diagnostics">本轮局部读取诊断。</param>
    public EngineSessionSnapshot(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<EngineRegistryEntry> engines,
        EngineSelectionResult selection,
        IReadOnlyDictionary<string, HeartbeatInfo?> heartbeats,
        IReadOnlyList<EngineSessionDiagnostic> diagnostics)
    {
        GeneratedAtUtc = generatedAtUtc;
        Engines = engines.ToArray();
        Selection = selection;
        Heartbeats = new Dictionary<string, HeartbeatInfo?>(heartbeats, StringComparer.Ordinal);
        Diagnostics = diagnostics.ToArray();
    }

    /// <summary>获取快照生成时间。</summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>获取成功解析的 registry 条目。</summary>
    public IReadOnlyList<EngineRegistryEntry> Engines { get; }

    /// <summary>获取 engine 选择结果。</summary>
    public EngineSelectionResult Selection { get; }

    /// <summary>获取本轮读取到的 heartbeat。</summary>
    public IReadOnlyDictionary<string, HeartbeatInfo?> Heartbeats { get; }

    /// <summary>获取本轮局部读取诊断。</summary>
    public IReadOnlyList<EngineSessionDiagnostic> Diagnostics { get; }

    /// <summary>获取当前选中的 registry 条目。</summary>
    public EngineRegistryEntry? SelectedRegistry
    {
        get
        {
            if (!Selection.IsSelected)
            {
                return null;
            }

            return Engines.FirstOrDefault(entry => string.Equals(
                entry.EngineId,
                Selection.SelectedEngineId,
                StringComparison.Ordinal));
        }
    }

    /// <summary>获取当前选中的 heartbeat。</summary>
    public HeartbeatInfo? SelectedHeartbeat
    {
        get
        {
            if (!Selection.IsSelected)
            {
                return null;
            }

            return Heartbeats.TryGetValue(Selection.SelectedEngineId, out var heartbeat)
                ? heartbeat
                : null;
        }
    }

    /// <summary>
    /// 获取 registry 与 heartbeat 都能确认时的宿主身份；身份不完整或不一致时返回 null。
    /// </summary>
    public HostIdentity? CurrentHostIdentity
    {
        get
        {
            var registry = SelectedRegistry;
            var heartbeat = SelectedHeartbeat;
            if (registry == null || heartbeat == null)
            {
                return null;
            }

            return EngineHostIdentity.TryCreate(registry, heartbeat, out var identity)
                ? identity
                : null;
        }
    }
}
