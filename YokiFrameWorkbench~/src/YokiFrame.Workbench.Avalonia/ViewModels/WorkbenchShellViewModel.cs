using System.Windows.Input;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Tooling.Application.Models.SpatialKit;
using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Tooling.Application.Services.SaveKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Tooling.Application.Services.UIKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 承载 Workbench Shell 的可绑定状态和首批页面投影。
/// </summary>
public sealed partial class WorkbenchShellViewModel : ViewModelBase
{
    private const int MAX_COMMAND_RESULT_LOG_LENGTH = 180;
    private readonly Action mRefreshRequested;
    private readonly Action<string> mEngineChanged;
    private readonly Func<string, string, Task> mCommandRequested;
    private readonly Func<Uri, Task>? mOpenUriAsync;
    private Action<Task>? mTrackTask;
    private IReadOnlyList<string> mEngineIds = Array.Empty<string>();
    private WorkbenchDashboardState? mDashboardState;
    private string mCommandTraceText = string.Empty;
    private string mHeaderText = "Workbench loading";
    private string mSelectedEngineId = string.Empty;
    private string mStatusText = "waiting for dashboard";
    private bool mIsUpdatingEngines;


    /// <summary>
    /// 通过页头唯一入口同时刷新 dashboard 与当前项目的 Skill 安装状态。
    /// </summary>
    private void RefreshWorkbench()
    {
        mRefreshRequested();
        RefreshSkillStatusForCurrentProject();
    }

    /// <summary>
    /// 获取 FsmKit 专用只读页面状态。
    /// </summary>
    public FsmKitPageViewModel FsmKitPage { get; }

    /// <summary>
    /// 获取 LogKit 运行配置专用页面状态。
    /// </summary>
    public LogKitPageViewModel LogKitPage { get; }

    /// <summary>
    /// 获取 PoolKit 对象池诊断专用页面状态。
    /// </summary>
    public PoolKitPageViewModel PoolKitPage { get; }

    /// <summary>
    /// 获取 ResKit 资源诊断专用页面状态。
    /// </summary>
    public ResKitPageViewModel ResKitPage { get; }

    /// <summary>
    /// 获取 ActionKit 活动动作树诊断专用页面状态。
    /// </summary>
    public ActionKitPageViewModel ActionKitPage { get; }

    /// <summary>获取 AudioKit Bus、播放进度与历史观察页面状态。</summary>
    public AudioKitPageViewModel AudioKitPage { get; }

    /// <summary>获取 SpatialKit 索引实例和密度诊断页面状态。</summary>
    public SpatialKitPageViewModel SpatialKitPage { get; }

    /// <summary>获取 Unity UIKit Runtime 面板和栈诊断页面状态。</summary>
    public UIKitPageViewModel UIKitPage { get; }

    /// <summary>获取 TableKit Luban 验证与生成页面状态。</summary>
    public TableKitPageViewModel TableKitPage { get; }

    /// <summary>获取 LocalizationKit 本地化搜索与缺失诊断页面状态。</summary>
    public LocalizationKitPageViewModel LocalizationKitPage { get; }

    /// <summary>获取 SaveKit 存档配置与文件浏览页面状态。</summary>
    public SaveKitPageViewModel SaveKitPage { get; }

    /// <summary>
    /// 获取 EventKit Runtime 事件专用页面状态。
    /// </summary>
    public EventKitPageViewModel EventKitPage { get; }

    /// <summary>
    /// 获取包内离线文档专用页面状态。
    /// </summary>
    public DocumentationPageViewModel DocumentationPage { get; }

    /// <summary>
    /// 获取 Workbench Runtime 后台新版检测与显式重新编译状态。
    /// </summary>
    public WorkbenchRuntimeUpdateViewModel RuntimeUpdate { get; }

    /// <summary>
    /// 绑定窗口会话的后台任务登记器，使页面异步加载随窗口关闭取消并等待。
    /// </summary>
    /// <param name="trackTask">窗口会话提供的任务登记回调。</param>
    public void SetTaskTracker(Action<Task> trackTask)
    {
        ArgumentNullException.ThrowIfNull(trackTask);
        mTrackTask = trackTask;
    }

    /// <summary>
    /// 将页面异步任务纳入窗口会话；headless 场景未绑定窗口时保留独立运行行为。
    /// </summary>
    /// <param name="task">页面初始化任务。</param>
    internal void TrackPageTask(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (mTrackTask != null)
        {
            mTrackTask(task);
        }
    }

    /// <summary>
    /// 获取顶部 engine selector 使用的 engine id 列表。
    /// </summary>
    public IReadOnlyList<string> EngineIds
    {
        get => mEngineIds;
        private set => SetProperty(ref mEngineIds, value);
    }

    /// <summary>
    /// 获取或设置当前选中的 engine；用户切换时会触发 dashboard 刷新。
    /// </summary>
    public string SelectedEngineId
    {
        get => mSelectedEngineId;
        set => SetSelectedEngine(value);
    }

    /// <summary>
    /// 获取窗口顶部状态文本。
    /// </summary>
    public string HeaderText
    {
        get => mHeaderText;
        private set => SetProperty(ref mHeaderText, value);
    }

    /// <summary>
    /// 获取窗口底部 dashboard 摘要。
    /// </summary>
    public string StatusText
    {
        get => mStatusText;
        private set => SetProperty(ref mStatusText, value);
    }

    /// <summary>
    /// 获取命令发送和响应摘要。
    /// </summary>
    public string CommandTraceText
    {
        get => mCommandTraceText;
        private set => SetProperty(ref mCommandTraceText, value);
    }

    /// <summary>
    /// 获取刷新 dashboard 的命令。
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// 获取清空运行日志的命令。
    /// </summary>
    public ICommand ClearLogCommand { get; }

    /// <summary>
    /// 获取发送当前下拉所选 Kit/action 的命令。
    /// </summary>
    public ICommand SendSelectedCommand { get; }

    /// <summary>
    /// 获取发送 ping 的命令。
    /// </summary>
    public ICommand PingCommand { get; }

    /// <summary>
    /// 获取发送 bridge status 的命令。
    /// </summary>
    public ICommand BridgeStatusCommand { get; }

    /// <summary>
    /// 获取刷新 Runtime 命令目录的命令。
    /// </summary>
    public ICommand RefreshCommandCatalogCommand { get; }

    /// <summary>
    /// 使用新的 dashboard 状态更新当前页面投影。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    public void UpdateDashboard(WorkbenchDashboardState state)
    {
        mDashboardState = state;
        HeaderText = CreateHeaderText(state);
        StatusText = CreateStatusText(state);
        UpdateEngineSelector(state);
        RefreshCommandCatalogForSelectedEngine(state);
        RefreshWorkbenchLayout();
        UpdateSkillProjectRoot(state.ProjectRoot);
        UpdateCurrentPage();
    }

    /// <summary>
    /// 显示命令发送中的临时状态。
    /// </summary>
    /// <param name="action">命令 action。</param>
    public void ShowCommandInFlight(string action)
    {
        ShowCommandInFlight("System", action);
    }

    /// <summary>
    /// 显示命令发送中的临时状态。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">命令 action。</param>
    public void ShowCommandInFlight(string kit, string action)
    {
        CommandTraceText = "command " + kit + "/" + action + " pending";
        AddLogLine("命令发送 -> " + kit + "/" + action, WorkbenchLogLineKind.Outbound);
    }

    /// <summary>
    /// 显示命令响应摘要。
    /// </summary>
    /// <param name="state">命令响应状态。</param>
    public void ShowCommandResult(WorkbenchCommandState state)
    {
        CommandTraceText = state.Outcome == CommandOutcomeState.Unknown
            ? state.Kit + "/" + state.Action + " -> Unknown " + state.ErrorMessage
            : state.Ok
            ? state.Kit + "/" + state.Action + " -> " + state.Status + " " + state.ResultJson
            : state.Kit + "/" + state.Action + " -> " + state.ErrorMessage;
        if (state.Ok && state.Kit == "System" && state.Action == "list_commands")
        {
            UpdateCommandCatalogJson(state.ResultJson);
        }

        if (state.Ok)
        {
            AddLogLine("命令接收 <- " + state.Kit + "/" + state.Action + " [" + state.Status + "] " + CreateCommandResultPreview(state.ResultJson), WorkbenchLogLineKind.Inbound);
            return;
        }

        AddLogLine(
            state.Outcome == CommandOutcomeState.Unknown
                ? "命令结果不确定 <- " + state.Kit + "/" + state.Action + " " + state.ErrorMessage
                : "命令失败 <- " + state.Kit + "/" + state.Action + " " + state.ErrorMessage,
            WorkbenchLogLineKind.Error);
    }

    /// <summary>
    /// 显示非命令类临时错误。
    /// </summary>
    /// <param name="message">错误说明。</param>
    public void ShowTransientError(string message)
    {
        CommandTraceText = "error -> " + message;
        AddLogLine("错误: " + message, WorkbenchLogLineKind.Error);
    }

}
