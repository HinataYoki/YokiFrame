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

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 保存一次 Dashboard 刷新的 Kit 投影结果。
/// 
/// 该目录把页面 read model 与 Dashboard 稳定 envelope 分开，新增页面只需扩展此目录，
/// 不再继续向 WorkbenchDashboardState 的长构造器追加位置参数。
/// </summary>
public sealed class WorkbenchDashboardProjectionCatalog
{
    /// <summary>
    /// 创建一组页面投影结果。
    /// </summary>
    /// <param name="fsmKitState">FsmKit 投影。</param>
    /// <param name="architectureState">Architecture 投影。</param>
    /// <param name="eventKitState">EventKit 投影。</param>
    /// <param name="logKitState">LogKit 投影。</param>
    /// <param name="poolKitState">PoolKit 投影。</param>
    /// <param name="resKitState">ResKit 投影。</param>
    /// <param name="actionKitState">ActionKit 投影。</param>
    /// <param name="audioKitState">AudioKit 投影。</param>
    /// <param name="spatialKitState">SpatialKit 投影。</param>
    /// <param name="uiKitState">UIKit 投影。</param>
    /// <param name="saveKitState">SaveKit 投影。</param>
    public WorkbenchDashboardProjectionCatalog(
        WorkbenchFsmKitState? fsmKitState,
        WorkbenchArchitectureState? architectureState,
        WorkbenchEventKitState? eventKitState,
        WorkbenchLogKitState? logKitState,
        WorkbenchPoolKitState? poolKitState,
        WorkbenchResKitState? resKitState,
        WorkbenchActionKitState? actionKitState,
        WorkbenchAudioKitState? audioKitState,
        WorkbenchSpatialKitState? spatialKitState,
        WorkbenchUIKitState? uiKitState,
        WorkbenchSaveKitState? saveKitState)
    {
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
    }

    /// <summary>获取 FsmKit 强类型投影。</summary>
    public WorkbenchFsmKitState? FsmKitState { get; }

    /// <summary>获取 Architecture 强类型投影。</summary>
    public WorkbenchArchitectureState? ArchitectureState { get; }

    /// <summary>获取 EventKit 强类型投影。</summary>
    public WorkbenchEventKitState? EventKitState { get; }

    /// <summary>获取 LogKit 强类型投影。</summary>
    public WorkbenchLogKitState? LogKitState { get; }

    /// <summary>获取 PoolKit 强类型投影。</summary>
    public WorkbenchPoolKitState? PoolKitState { get; }

    /// <summary>获取 ResKit 强类型投影。</summary>
    public WorkbenchResKitState? ResKitState { get; }

    /// <summary>获取 ActionKit 强类型投影。</summary>
    public WorkbenchActionKitState? ActionKitState { get; }

    /// <summary>获取 AudioKit 强类型投影。</summary>
    public WorkbenchAudioKitState? AudioKitState { get; }

    /// <summary>获取 SpatialKit 强类型投影。</summary>
    public WorkbenchSpatialKitState? SpatialKitState { get; }

    /// <summary>获取 UIKit 强类型投影。</summary>
    public WorkbenchUIKitState? UIKitState { get; }

    /// <summary>获取 SaveKit 强类型投影。</summary>
    public WorkbenchSaveKitState? SaveKitState { get; }
}
