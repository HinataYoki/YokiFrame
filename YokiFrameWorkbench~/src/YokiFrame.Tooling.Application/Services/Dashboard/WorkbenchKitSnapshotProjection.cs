using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 从本轮 snapshot 列表抽取单个 Kit 的公共字段，供各 Kit 强类型投影复用。
/// 只消除 DataSource 构造样板，不改变 stale / 身份 / payload 语义。
/// </summary>
internal static class WorkbenchKitSnapshotProjection
{
    /// <summary>
    /// 尝试定位指定 Kit 的 snapshot 项。
    /// </summary>
    /// <param name="snapshots">本轮已读取的 Kit 状态集合。</param>
    /// <param name="kit">目标 Kit 名称。</param>
    /// <param name="snapshot">找到的 snapshot；未找到时为 null。</param>
    /// <returns>存在对应 Kit 项时返回 true。</returns>
    internal static bool TryGetSnapshot(
        IReadOnlyList<WorkbenchSnapshotState> snapshots,
        string kit,
        out WorkbenchSnapshotState? snapshot)
    {
        snapshot = snapshots.FirstOrDefault(
            item => string.Equals(item.Kit, kit, StringComparison.Ordinal));
        return snapshot != null;
    }

    /// <summary>
    /// 合并 snapshot 的 stale 与错误信息，优先保留显式 stale 原因。
    /// </summary>
    /// <param name="snapshot">当前 Kit snapshot。</param>
    /// <returns>可供 DataSource 使用的 stale 文本；正常时可能为空。</returns>
    internal static string ResolveStaleReason(WorkbenchSnapshotState snapshot)
    {
        return string.IsNullOrWhiteSpace(snapshot.StaleReason)
            ? snapshot.ErrorMessage ?? string.Empty
            : snapshot.StaleReason;
    }

    /// <summary>
    /// 仅在 snapshot 可读时采用 bridge 宿主身份，避免用过期 bridge 身份污染失败项。
    /// </summary>
    /// <param name="snapshot">当前 Kit snapshot。</param>
    /// <param name="bridgeHealth">本轮 bridge 健康信息。</param>
    /// <returns>sessionId、generation、mode 三元组。</returns>
    internal static (string SessionId, long Generation, string Mode) ResolveHostIdentity(
        WorkbenchSnapshotState snapshot,
        WorkbenchBridgeHealth bridgeHealth)
    {
        if (!snapshot.Exists)
        {
            return (string.Empty, 0L, string.Empty);
        }

        return (bridgeHealth.SessionId, bridgeHealth.Generation, bridgeHealth.Mode);
    }

    /// <summary>
    /// 解析 snapshot 更新时间；缺失时回落 MinValue，与历史 DataSource 构造保持一致。
    /// </summary>
    /// <param name="snapshot">当前 Kit snapshot。</param>
    /// <returns>UTC 更新时间。</returns>
    internal static DateTimeOffset ResolveUpdatedAtUtc(WorkbenchSnapshotState snapshot)
    {
        return snapshot.UpdatedAtUtc ?? DateTimeOffset.MinValue;
    }
}
