using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Tooling.Application.Models.Architecture;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Tooling.Application.Models.SaveKit;
using YokiFrame.Tooling.Application.Models.SpatialKit;
using YokiFrame.Tooling.Application.Models.UIKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 将本轮 snapshot 列表投影为各 Kit 强类型状态。
/// LoadDashboard 只做编排；命令路径仍留在 WorkbenchDashboardService 各 Kit partial。
/// </summary>
internal static class WorkbenchDashboardKitProjections
{
    /// <summary>投影 Architecture 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchArchitectureState? ProjectArchitecture(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithoutTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "Architecture",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchArchitectureStateParser.Parse(new WorkbenchArchitectureDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 EventKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchEventKitState? ProjectEventKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithoutTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "EventKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchEventKitStateParser.Parse(new WorkbenchEventKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 FsmKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchFsmKitState? ProjectFsmKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "FsmKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchFsmKitStateParser.Parse(new WorkbenchFsmKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 LogKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchLogKitState? ProjectLogKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "LogKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchLogKitStateParser.Parse(new WorkbenchLogKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 PoolKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchPoolKitState? ProjectPoolKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "PoolKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchPoolKitStateParser.Parse(new WorkbenchPoolKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 ResKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchResKitState? ProjectResKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "ResKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchResKitStateParser.Parse(new WorkbenchResKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 ActionKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchActionKitState? ProjectActionKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "ActionKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchActionKitStateParser.Parse(new WorkbenchActionKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 AudioKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchAudioKitState? ProjectAudioKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "AudioKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchAudioKitStateParser.Parse(new WorkbenchAudioKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 SpatialKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchSpatialKitState? ProjectSpatialKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "SpatialKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchSpatialKitStateParser.Parse(new WorkbenchSpatialKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 SaveKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchSaveKitState? ProjectSaveKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "SaveKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchSaveKitStateParser.Parse(new WorkbenchSaveKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>投影 UIKit 周期状态；无对应 snapshot 时返回 null。</summary>
    internal static WorkbenchUIKitState? ProjectUIKit(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots)
    {
        return ProjectWithEmptyTransport(
            engineId,
            bridgeHealth,
            snapshots,
            "UIKit",
            static (engineId, sessionId, generation, mode, updatedAtUtc, source, evidence, stale, payload) =>
                WorkbenchUIKitStateParser.Parse(new WorkbenchUIKitDataSource(
                    engineId,
                    sessionId,
                    generation,
                    mode,
                    updatedAtUtc,
                    source,
                    string.Empty,
                    evidence,
                    stale,
                    payload)));
    }

    /// <summary>
    /// 无 Transport 字段的 DataSource 投影（Architecture / EventKit）。
    /// </summary>
    private static TState? ProjectWithoutTransport<TState>(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots,
        string kit,
        Func<string, string, long, string, DateTimeOffset, string, IReadOnlyList<string>, string, string, TState> parse)
        where TState : class
    {
        if (!WorkbenchKitSnapshotProjection.TryGetSnapshot(snapshots, kit, out WorkbenchSnapshotState? snapshot)
            || snapshot == null)
        {
            return null;
        }

        (string sessionId, long generation, string mode) = WorkbenchKitSnapshotProjection.ResolveHostIdentity(
            snapshot,
            bridgeHealth);
        return parse(
            engineId,
            sessionId,
            generation,
            mode,
            WorkbenchKitSnapshotProjection.ResolveUpdatedAtUtc(snapshot),
            snapshot.Source,
            snapshot.EvidencePaths,
            WorkbenchKitSnapshotProjection.ResolveStaleReason(snapshot),
            snapshot.RawPayloadJson);
    }

    /// <summary>
    /// 周期读取使用空 Transport 的 DataSource 投影（命令路径另填实际传输）。
    /// </summary>
    private static TState? ProjectWithEmptyTransport<TState>(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        IReadOnlyList<WorkbenchSnapshotState> snapshots,
        string kit,
        Func<string, string, long, string, DateTimeOffset, string, IReadOnlyList<string>, string, string, TState> parse)
        where TState : class
    {
        return ProjectWithoutTransport(engineId, bridgeHealth, snapshots, kit, parse);
    }
}
