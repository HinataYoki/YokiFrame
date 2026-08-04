namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 表示一次可验证的宿主会话身份。
/// </summary>
public sealed class HostIdentity : IEquatable<HostIdentity>
{
    /// <summary>
    /// 创建宿主身份。
    /// </summary>
    /// <param name="engineId">宿主 engine 标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主生命周期代次。</param>
    /// <param name="mode">宿主当前模式。</param>
    public HostIdentity(string engineId, string sessionId, long generation, string mode)
    {
        EngineId = engineId ?? string.Empty;
        SessionId = sessionId ?? string.Empty;
        Generation = generation;
        Mode = mode ?? string.Empty;
    }

    /// <summary>获取宿主 engine 标识。</summary>
    public string EngineId { get; }

    /// <summary>获取宿主会话标识。</summary>
    public string SessionId { get; }

    /// <summary>获取宿主生命周期代次。</summary>
    public long Generation { get; }

    /// <summary>获取宿主当前模式。</summary>
    public string Mode { get; }

    /// <summary>
    /// 判断身份是否具有足够字段可用于状态门禁。
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(EngineId)
        && !string.IsNullOrWhiteSpace(SessionId)
        && Generation > 0L;

    /// <summary>
    /// 比较两个宿主身份是否完全一致。
    /// </summary>
    /// <param name="other">待比较身份。</param>
    /// <returns>四个身份字段都一致时返回 true。</returns>
    public bool Equals(HostIdentity? other)
    {
        return other != null
            && string.Equals(EngineId, other.EngineId, StringComparison.Ordinal)
            && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
            && Generation == other.Generation
            && string.Equals(Mode, other.Mode, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as HostIdentity);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(EngineId, SessionId, Generation, Mode);
    }

    /// <summary>
    /// 比较两个宿主身份。
    /// </summary>
    public static bool operator ==(HostIdentity? left, HostIdentity? right)
    {
        return ReferenceEquals(left, right) || left?.Equals(right) == true;
    }

    /// <summary>
    /// 比较两个宿主身份是否不同。
    /// </summary>
    public static bool operator !=(HostIdentity? left, HostIdentity? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 以便于日志和诊断的稳定形式输出身份。
    /// </summary>
    /// <returns>身份摘要。</returns>
    public override string ToString()
    {
        return EngineId + "/" + SessionId + "/" + Generation + "/" + Mode;
    }
}
