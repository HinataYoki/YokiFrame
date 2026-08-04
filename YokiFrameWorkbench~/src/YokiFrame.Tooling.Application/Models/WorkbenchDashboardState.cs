using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models.Architecture;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Tooling.Application.Models.SaveKit;
using YokiFrame.Tooling.Application.Models.SpatialKit;
using YokiFrame.Tooling.Application.Models.UIKit;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench 首屏所需的 FileBridge 聚合状态。
/// </summary>
public sealed class WorkbenchDashboardState
{
    /// <summary>
    /// 创建 Workbench dashboard 状态。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="generatedAtUtc">状态生成时间。</param>
    /// <param name="engines">engine registry 列表。</param>
    /// <param name="selectedEngineId">当前选中的 engine。</param>
    /// <param name="bridgeStatus">FileBridge 状态。</param>
    /// <param name="bridgeHealth">FileBridge 连接健康信息。</param>
    /// <param name="doctorReport">doctor 只读诊断报告；状态读取失败时为 null。</param>
    /// <param name="snapshots">首批页面 snapshot 状态。</param>
    /// <param name="harnessSummary">harness 摘要。</param>
    /// <param name="errorMessages">读取过程中的非终止错误。</param>
    /// <param name="fsmKitState">FsmKit 强类型状态；旧调用方可省略。</param>
    /// <param name="architectureState">Architecture 强类型状态；旧调用方可省略。</param>
    /// <param name="eventKitState">EventKit 强类型状态；尚无数据时为空。</param>
    /// <param name="logKitState">LogKit 强类型状态；尚无数据时为空。</param>
    /// <param name="poolKitState">PoolKit 强类型状态；尚无数据时为空。</param>
    /// <param name="resKitState">ResKit 强类型状态；尚无数据时为空。</param>
    /// <param name="actionKitState">ActionKit 强类型状态；尚无数据时为空。</param>
    /// <param name="audioKitState">AudioKit 强类型状态；尚无数据时为空。</param>
    /// <param name="spatialKitState">SpatialKit 强类型状态；尚无数据时为空。</param>
    /// <param name="uiKitState">UIKit 强类型状态；Unity Runtime 尚未发布时为空。</param>
    /// <param name="saveKitState">SaveKit 强类型状态；Runtime 尚未发布时为空。</param>
    public WorkbenchDashboardState(
        string projectRoot,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<EngineRegistryEntry> engines,
        string selectedEngineId,
        FileBridgeStatus? bridgeStatus,
        WorkbenchBridgeHealth bridgeHealth,
        WorkbenchDoctorReport? doctorReport,
        IReadOnlyList<WorkbenchSnapshotState> snapshots,
        string harnessSummary,
        IReadOnlyList<string> errorMessages,
        WorkbenchFsmKitState? fsmKitState = null,
        WorkbenchArchitectureState? architectureState = null,
        WorkbenchEventKitState? eventKitState = null,
        WorkbenchLogKitState? logKitState = null,
        WorkbenchPoolKitState? poolKitState = null,
        WorkbenchResKitState? resKitState = null,
        WorkbenchActionKitState? actionKitState = null,
        WorkbenchAudioKitState? audioKitState = null,
        WorkbenchSpatialKitState? spatialKitState = null,
        WorkbenchUIKitState? uiKitState = null,
        WorkbenchSaveKitState? saveKitState = null,
        EngineSessionSnapshot? engineSession = null)
        : this(
            projectRoot,
            generatedAtUtc,
            engines,
            EngineSelectionResult.CreateSelected(selectedEngineId),
            bridgeStatus,
            bridgeHealth,
            doctorReport,
            snapshots,
            harnessSummary,
            errorMessages,
            fsmKitState,
            architectureState,
            eventKitState,
            logKitState,
            poolKitState,
            resKitState,
            actionKitState,
            audioKitState,
            spatialKitState,
            uiKitState,
            saveKitState,
            engineSession)
    {
    }

    /// <summary>
    /// 使用稳定 Dashboard envelope 和集中投影目录创建状态。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="generatedAtUtc">状态生成时间。</param>
    /// <param name="engines">engine registry 列表。</param>
    /// <param name="engineSelection">当前 engine 选择结果。</param>
    /// <param name="bridgeStatus">FileBridge 状态。</param>
    /// <param name="bridgeHealth">FileBridge 连接健康信息。</param>
    /// <param name="doctorReport">doctor 只读诊断报告。</param>
    /// <param name="snapshots">首批页面 snapshot 状态。</param>
    /// <param name="harnessSummary">harness 摘要。</param>
    /// <param name="errorMessages">读取过程中的非终止错误。</param>
    /// <param name="projections">本轮页面投影目录。</param>
    /// <param name="engineSession">registry/heartbeat 统一会话快照。</param>
    public WorkbenchDashboardState(
        string projectRoot,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<EngineRegistryEntry> engines,
        EngineSelectionResult engineSelection,
        FileBridgeStatus? bridgeStatus,
        WorkbenchBridgeHealth bridgeHealth,
        WorkbenchDoctorReport? doctorReport,
        IReadOnlyList<WorkbenchSnapshotState> snapshots,
        string harnessSummary,
        IReadOnlyList<string> errorMessages,
        WorkbenchDashboardProjectionCatalog projections,
        EngineSessionSnapshot? engineSession = null)
    {
        ArgumentNullException.ThrowIfNull(projections);
        ProjectRoot = projectRoot;
        GeneratedAtUtc = generatedAtUtc;
        Engines = engines;
        EngineSelection = engineSelection;
        BridgeStatus = bridgeStatus;
        BridgeHealth = bridgeHealth;
        DoctorReport = doctorReport;
        Snapshots = snapshots;
        HarnessSummary = harnessSummary;
        ErrorMessages = errorMessages;
        KitProjections = projections;
        FsmKitState = projections.FsmKitState;
        ArchitectureState = projections.ArchitectureState;
        EventKitState = projections.EventKitState;
        LogKitState = projections.LogKitState;
        PoolKitState = projections.PoolKitState;
        ResKitState = projections.ResKitState;
        ActionKitState = projections.ActionKitState;
        AudioKitState = projections.AudioKitState;
        SpatialKitState = projections.SpatialKitState;
        UIKitState = projections.UIKitState;
        SaveKitState = projections.SaveKitState;
        EngineSession = engineSession;
    }

    /// <summary>
    /// 使用可恢复 engine 选择结果创建 Workbench dashboard 状态。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="generatedAtUtc">状态生成时间。</param>
    /// <param name="engines">engine registry 列表。</param>
    /// <param name="engineSelection">当前 engine 选择结果。</param>
    /// <param name="bridgeStatus">FileBridge 状态。</param>
    /// <param name="bridgeHealth">FileBridge 连接健康信息。</param>
    /// <param name="doctorReport">doctor 只读诊断报告；状态读取失败时为 null。</param>
    /// <param name="snapshots">首批页面 snapshot 状态。</param>
    /// <param name="harnessSummary">harness 摘要。</param>
    /// <param name="errorMessages">读取过程中的非终止错误。</param>
    /// <param name="fsmKitState">FsmKit 强类型状态；旧调用方可省略。</param>
    /// <param name="architectureState">Architecture 强类型状态；旧调用方可省略。</param>
    /// <param name="eventKitState">EventKit 强类型状态；尚无数据时为空。</param>
    /// <param name="logKitState">LogKit 强类型状态；尚无数据时为空。</param>
    /// <param name="poolKitState">PoolKit 强类型状态；尚无数据时为空。</param>
    /// <param name="resKitState">ResKit 强类型状态；尚无数据时为空。</param>
    /// <param name="actionKitState">ActionKit 强类型状态；尚无数据时为空。</param>
    /// <param name="audioKitState">AudioKit 强类型状态；尚无数据时为空。</param>
    /// <param name="spatialKitState">SpatialKit 强类型状态；尚无数据时为空。</param>
    /// <param name="uiKitState">UIKit 强类型状态；Unity Runtime 尚未发布时为空。</param>
    /// <param name="saveKitState">SaveKit 强类型状态；Runtime 尚未发布时为空。</param>
    public WorkbenchDashboardState(
        string projectRoot,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<EngineRegistryEntry> engines,
        EngineSelectionResult engineSelection,
        FileBridgeStatus? bridgeStatus,
        WorkbenchBridgeHealth bridgeHealth,
        WorkbenchDoctorReport? doctorReport,
        IReadOnlyList<WorkbenchSnapshotState> snapshots,
        string harnessSummary,
        IReadOnlyList<string> errorMessages,
        WorkbenchFsmKitState? fsmKitState = null,
        WorkbenchArchitectureState? architectureState = null,
        WorkbenchEventKitState? eventKitState = null,
        WorkbenchLogKitState? logKitState = null,
        WorkbenchPoolKitState? poolKitState = null,
        WorkbenchResKitState? resKitState = null,
        WorkbenchActionKitState? actionKitState = null,
        WorkbenchAudioKitState? audioKitState = null,
        WorkbenchSpatialKitState? spatialKitState = null,
        WorkbenchUIKitState? uiKitState = null,
        WorkbenchSaveKitState? saveKitState = null,
        EngineSessionSnapshot? engineSession = null)
    {
        ProjectRoot = projectRoot;
        GeneratedAtUtc = generatedAtUtc;
        Engines = engines;
        EngineSelection = engineSelection;
        BridgeStatus = bridgeStatus;
        BridgeHealth = bridgeHealth;
        DoctorReport = doctorReport;
        Snapshots = snapshots;
        HarnessSummary = harnessSummary;
        ErrorMessages = errorMessages;
        FsmKitState = fsmKitState;
        ArchitectureState = architectureState;
        EventKitState = eventKitState;
        LogKitState = logKitState;
        PoolKitState = poolKitState;
        ResKitState = resKitState;
        ActionKitState = actionKitState;
        AudioKitState = audioKitState;
        SpatialKitState = spatialKitState;
        UIKitState = uiKitState;
        SaveKitState = saveKitState;
        KitProjections = new WorkbenchDashboardProjectionCatalog(
            fsmKitState,
            architectureState,
            eventKitState,
            logKitState,
            poolKitState,
            resKitState,
            actionKitState,
            audioKitState,
            spatialKitState,
            uiKitState,
            saveKitState);
        EngineSession = engineSession;
    }

    /// <summary>
    /// 获取项目根目录。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取状态生成时间。
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>
    /// 获取已发现的 engine。
    /// </summary>
    public IReadOnlyList<EngineRegistryEntry> Engines { get; }

    /// <summary>
    /// 获取当前 engine 选择结果，供交互式调用方显示候选和恢复建议。
    /// </summary>
    public EngineSelectionResult EngineSelection { get; }

    /// <summary>
    /// 获取当前选中的 engine 标识。
    /// </summary>
    public string SelectedEngineId => EngineSelection.SelectedEngineId;

    /// <summary>
    /// 获取当前可用于命令和 telemetry 门禁的完整宿主身份。
    /// 非 Online 或 registry/heartbeat 尚未收敛时返回 null。
    /// </summary>
    public HostIdentity? CurrentHostIdentity
    {
        get
        {
            if (EngineSession != null)
            {
                return EngineSession.CurrentHostIdentity;
            }

            if (BridgeHealth.State != WorkbenchBridgeConnectionState.Online
                || string.IsNullOrWhiteSpace(SelectedEngineId)
                || string.IsNullOrWhiteSpace(BridgeHealth.SessionId)
                || BridgeHealth.Generation <= 0L)
            {
                return null;
            }

            return new HostIdentity(
                SelectedEngineId,
                BridgeHealth.SessionId,
                BridgeHealth.Generation,
                BridgeHealth.Mode);
        }
    }

    /// <summary>
    /// 获取本轮 registry/heartbeat 统一发现快照；旧手工构造状态时可以为空。
    /// </summary>
    public EngineSessionSnapshot? EngineSession { get; }

    /// <summary>
    /// 获取本轮页面投影目录；旧属性仍保留用于兼容已有 ViewModel 和测试。
    /// </summary>
    public WorkbenchDashboardProjectionCatalog KitProjections { get; }

    /// <summary>
    /// 获取 FileBridge 队列和 heartbeat 状态。
    /// </summary>
    public FileBridgeStatus? BridgeStatus { get; }

    /// <summary>
    /// 获取 FileBridge 连接健康信息。
    /// </summary>
    public WorkbenchBridgeHealth BridgeHealth { get; }

    /// <summary>
    /// 获取 doctor 只读诊断报告；状态读取失败时为 null。
    /// </summary>
    public WorkbenchDoctorReport? DoctorReport { get; }

    /// <summary>
    /// 获取首批页面 snapshot 状态。
    /// </summary>
    public IReadOnlyList<WorkbenchSnapshotState> Snapshots { get; }

    /// <summary>
    /// 获取 harness capability 摘要。
    /// </summary>
    public string HarnessSummary { get; }

    /// <summary>
    /// 获取读取过程中的非终止错误。
    /// </summary>
    public IReadOnlyList<string> ErrorMessages { get; }

    /// <summary>
    /// 获取 FsmKit 强类型状态；旧调用方手工创建 dashboard 时可以为空。
    /// </summary>
    public WorkbenchFsmKitState? FsmKitState { get; }

    /// <summary>
    /// 获取 Architecture 强类型状态；旧调用方手工创建 dashboard 时可以为空。
    /// </summary>
    public WorkbenchArchitectureState? ArchitectureState { get; }

    /// <summary>
    /// 获取 EventKit 强类型状态；Runtime 尚未发布时为空。
    /// </summary>
    public WorkbenchEventKitState? EventKitState { get; }

    /// <summary>
    /// 获取 LogKit 强类型状态；Runtime 尚未发布时为空。
    /// </summary>
    public WorkbenchLogKitState? LogKitState { get; }

    /// <summary>
    /// 获取 PoolKit 强类型状态；Runtime 尚未发布时为空。
    /// </summary>
    public WorkbenchPoolKitState? PoolKitState { get; }

    /// <summary>
    /// 获取 ResKit 资源诊断强类型状态；Runtime 尚未发布时为空。
    /// </summary>
    public WorkbenchResKitState? ResKitState { get; }

    /// <summary>
    /// 获取 ActionKit 强类型状态；Runtime 尚未加载 Provider 时为空。
    /// </summary>
    public WorkbenchActionKitState? ActionKitState { get; }

    /// <summary>获取 AudioKit 强类型状态；Runtime 尚未加载 Provider 时为空。</summary>
    public WorkbenchAudioKitState? AudioKitState { get; }

    /// <summary>获取 SpatialKit 强类型状态；Runtime 尚未加载 Provider 时为空。</summary>
    public WorkbenchSpatialKitState? SpatialKitState { get; }

    /// <summary>获取 UIKit 强类型只读状态；Unity Runtime 尚未发布时为空。</summary>
    public WorkbenchUIKitState? UIKitState { get; }

    /// <summary>获取 SaveKit 强类型只读状态；Runtime 尚未发布时为空。</summary>
    public WorkbenchSaveKitState? SaveKitState { get; }
}
