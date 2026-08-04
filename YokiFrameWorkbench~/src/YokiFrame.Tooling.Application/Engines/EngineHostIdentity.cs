using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Engines;

/// <summary>
/// 统一比较 engine registry 与 heartbeat 公开的宿主身份，防止文件切换窗口把旧状态当作当前实例。
/// </summary>
internal static class EngineHostIdentity
{
    /// <summary>
    /// 判断两个来源同时提供有效 session 或 generation 时是否属于不同宿主实例；缺失字段保持向后兼容，不单独构成不一致。
    /// </summary>
    /// <param name="registry">本轮读取到的 engine registry。</param>
    /// <param name="heartbeat">同一 engine 的 heartbeat。</param>
    /// <returns>任一有效身份字段不一致时返回 true。</returns>
    public static bool HasMismatch(EngineRegistryEntry registry, HeartbeatInfo heartbeat)
    {
        var sessionMismatch = !string.IsNullOrWhiteSpace(registry.SessionId)
            && !string.IsNullOrWhiteSpace(heartbeat.SessionId)
            && !string.Equals(registry.SessionId, heartbeat.SessionId, StringComparison.Ordinal);
        var generationMismatch = registry.Generation != 0L
            && heartbeat.Generation != 0L
            && registry.Generation != heartbeat.Generation;
        return sessionMismatch || generationMismatch;
    }

    /// <summary>
    /// 从 registry 与 heartbeat 组合可用于命令和 telemetry 门禁的宿主身份。
    /// </summary>
    /// <param name="registry">本轮读取到的 registry。</param>
    /// <param name="heartbeat">同一 engine 的 heartbeat。</param>
    /// <param name="identity">成功组合出的宿主身份。</param>
    /// <returns>字段一致且身份完整时返回 true。</returns>
    public static bool TryCreate(
        EngineRegistryEntry registry,
        HeartbeatInfo heartbeat,
        out HostIdentity identity)
    {
        identity = null!;
        if (HasMismatch(registry, heartbeat))
        {
            return false;
        }

        var sessionId = string.IsNullOrWhiteSpace(heartbeat.SessionId)
            ? registry.SessionId
            : heartbeat.SessionId;
        var generation = heartbeat.Generation != 0L
            ? heartbeat.Generation
            : registry.Generation;
        var mode = string.IsNullOrWhiteSpace(heartbeat.Mode)
            ? registry.Mode
            : heartbeat.Mode;
        var candidate = new HostIdentity(registry.EngineId, sessionId, generation, mode);
        if (!candidate.IsValid)
        {
            return false;
        }

        identity = candidate;
        return true;
    }
}
