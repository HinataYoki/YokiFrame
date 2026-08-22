using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>创建 Workbench Shell 并装配各 Kit 页面 ViewModel。</summary>
public sealed partial class WorkbenchShellViewModel
{
    /// <summary>
    /// 创建 Workbench Shell ViewModel。
    /// </summary>
    /// <param name="refreshRequested">刷新 dashboard 的回调。</param>
    /// <param name="engineChanged">切换 engine 后通知窗口刷新数据的回调。</param>
    /// <param name="commandRequested">发送 System 命令的回调。</param>
    public WorkbenchShellViewModel(Action refreshRequested, Action<string> engineChanged, Func<string, Task> commandRequested)
        : this(new WorkbenchShellCallbacks(
                refreshRequested,
                engineChanged,
                (_, action) => commandRequested(action)))
    {
    }

    /// <summary>
    /// 创建 Workbench Shell ViewModel。
    /// </summary>
    /// <param name="refreshRequested">刷新 dashboard 的回调。</param>
    /// <param name="engineChanged">切换 engine 后通知窗口刷新数据的回调。</param>
    /// <param name="commandRequested">发送 Kit/action 命令的回调。</param>
    public WorkbenchShellViewModel(Action refreshRequested, Action<string> engineChanged, Func<string, string, Task> commandRequested)
        : this(new WorkbenchShellCallbacks(refreshRequested, engineChanged, commandRequested))
    {
    }

    /// <summary>
    /// 创建带真实包元数据和外部链接边界的 Workbench Shell。
    /// </summary>
    /// <param name="refreshRequested">刷新 dashboard 的回调。</param>
    /// <param name="engineChanged">切换 engine 后通知窗口刷新数据的回调。</param>
    /// <param name="commandRequested">发送 Kit/action 命令的回调。</param>
    /// <param name="packageMetadata">Application 解析完成的 package.json 元数据。</param>
    /// <param name="openUriAsync">通过当前平台默认浏览器打开 HTTPS 地址的回调。</param>
    public WorkbenchShellViewModel(
        Action refreshRequested,
        Action<string> engineChanged,
        Func<string, string, Task> commandRequested,
        YokiFrame.Tooling.Application.Packages.YokiFramePackageMetadata packageMetadata,
        Func<Uri, Task> openUriAsync)
        : this(
            new WorkbenchShellCallbacks(refreshRequested, engineChanged, commandRequested, openUriAsync),
            new WorkbenchShellMeta(string.Empty, string.Empty, packageMetadata))
    {
    }

    /// <summary>
    /// 分组依赖的唯一真实构造器：公开重载只负责把平铺参数组装成分组记录，
    /// 各 Kit 页面依赖按 Kit 边界成组传递，新增页面依赖不再拉长参数表。
    /// </summary>
    /// <param name="callbacks">Shell 级回调：刷新、engine 切换、命令发送、外链与剪贴板。</param>
    /// <param name="meta">Shell 元数据：真实包根、项目根、包元数据与离线文档服务。</param>
    /// <param name="fsmEventDependencies">FsmKit 详情查询与 EventKit 扫描/源码定位依赖。</param>
    /// <param name="logKitDependencies">LogKit 设置读写与日志尾读依赖。</param>
    /// <param name="poolKitDependencies">PoolKit 跟踪/泄漏/历史与源码定位依赖。</param>
    /// <param name="resKitDependencies">ResKit 详情/跟踪/历史与 lease 定位依赖。</param>
    /// <param name="actionKitDependencies">ActionKit 堆栈捕获切换依赖。</param>
    /// <param name="audioKitDependencies">AudioKit 索引扫描/生成与配置读写依赖。</param>
    /// <param name="toolPageDependencies">SaveKit / TableKit / UIKit 工具页服务与选择器依赖。</param>
    internal WorkbenchShellViewModel(
        WorkbenchShellCallbacks callbacks,
        WorkbenchShellMeta? meta = null,
        WorkbenchFsmEventDependencies? fsmEventDependencies = null,
        WorkbenchLogKitDependencies? logKitDependencies = null,
        WorkbenchPoolKitDependencies? poolKitDependencies = null,
        WorkbenchResKitDependencies? resKitDependencies = null,
        WorkbenchActionKitDependencies? actionKitDependencies = null,
        WorkbenchAudioKitDependencies? audioKitDependencies = null,
        WorkbenchToolPageDependencies? toolPageDependencies = null)
    {
        mRefreshRequested = callbacks.RefreshRequested;
        mEngineChanged = callbacks.EngineChanged;
        mCommandRequested = callbacks.CommandRequested;
        mOpenUriAsync = callbacks.OpenUriAsync;
        ApplyPackageMetadata(meta?.PackageMetadata);
        var projectRoot = meta?.ProjectRoot ?? string.Empty;
        var sourcePackageRoot = meta?.SourcePackageRoot ?? string.Empty;
        RefreshCommand = new RelayCommand(RefreshWorkbench);
        ClearLogCommand = new RelayCommand(ClearLogLines);
        SendSelectedCommand = new RelayCommand(() => _ = mCommandRequested(CommandGroup, CommandAction));
        PingCommand = new RelayCommand(() => _ = mCommandRequested("System", "ping"));
        BridgeStatusCommand = new RelayCommand(() => _ = mCommandRequested("System", "bridge_status"));
        RefreshCommandCatalogCommand = new RelayCommand(() => _ = mCommandRequested("System", "list_commands"));
        OpenRepositoryCommand = new AsyncRelayCommand(OpenRepositoryAsync, CanOpenRepository);
        EventKitPage = new EventKitPageViewModel(
            fsmEventDependencies?.EventKitCodeScanAsync,
            fsmEventDependencies?.OpenEventKitCodeLocationAsync);
        FsmKitPage = new FsmKitPageViewModel(fsmEventDependencies?.FsmDetailsQuery);
        LogKitPage = new LogKitPageViewModel(
            logKitDependencies?.LoadProjectSettings,
            logKitDependencies?.SaveSettingsAsync,
            logKitDependencies?.ClearHistoryAsync,
            logKitDependencies?.ReadFileAsync);
        PoolKitPage = new PoolKitPageViewModel(
            poolKitDependencies?.SetTrackingAsync,
            poolKitDependencies?.CheckLeaksAsync,
            poolKitDependencies?.ClearHistoryAsync,
            poolKitDependencies?.OpenCodeLocationAsync);
        ResKitPage = new ResKitPageViewModel(
            resKitDependencies?.GetResourceDetailAsync,
            resKitDependencies?.SetTrackingAsync,
            resKitDependencies?.ClearHistoryAsync,
            resKitDependencies?.OpenCodeLocationAsync);
        ActionKitPage = new ActionKitPageViewModel(
            actionKitDependencies?.SetStackTraceAsync,
            actionKitDependencies?.ClearStackTraceAsync);
        AudioKitPage = new AudioKitPageViewModel(
            audioKitDependencies?.ScanIndexAsync,
            audioKitDependencies?.GenerateIndexAsync,
            audioKitDependencies?.LoadSettings,
            audioKitDependencies?.SaveSettingsAsync);
        SpatialKitPage = new SpatialKitPageViewModel();
        UIKitPage = new UIKitPageViewModel(
            callbacks.CopyTextAsync,
            toolPageDependencies?.UIKitEditorActionAsync,
            toolPageDependencies?.UIKitEditorSettingsService);
        TableKitPage = new TableKitPageViewModel(
            projectRoot,
            toolPageDependencies?.TableKitApplicationService ?? new TableKitApplicationService(),
            callbacks.CopyTextAsync,
            toolPageDependencies?.SaveKitFolderPicker,
            toolPageDependencies?.TableKitLubanFilePicker);
        LocalizationKitPage = new LocalizationKitPageViewModel(
            projectRoot,
            new YokiFrame.Tooling.Application.Services.LocalizationKit.LocalizationKitApplicationService(),
            toolPageDependencies?.SaveKitFolderPicker,
            toolPageDependencies?.SaveKitOpenDirectoryAsync);
        SaveKitPage = new SaveKitPageViewModel(
            toolPageDependencies?.SaveKitSettingsService,
            toolPageDependencies?.SaveKitFolderPicker,
            toolPageDependencies?.SaveKitOpenDirectoryAsync);
        DocumentationPage = new DocumentationPageViewModel(
            sourcePackageRoot,
            meta?.DocumentationService,
            callbacks.CopyTextAsync,
            meta?.DocumentationInitializationError ?? string.Empty);
        RuntimeUpdate = new WorkbenchRuntimeUpdateViewModel(sourcePackageRoot, projectRoot);
        InitializeWorkbenchLayout();
        InitializeSkillInstaller();
        UpdateCurrentPage();
    }
}
