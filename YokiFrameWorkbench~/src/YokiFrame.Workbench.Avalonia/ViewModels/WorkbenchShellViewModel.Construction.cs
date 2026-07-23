using System.Windows.Input;
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
        : this(
            refreshRequested,
            engineChanged,
            (_, action) => commandRequested(action),
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            string.Empty)
    {
    }

    /// <summary>
    /// 创建 Workbench Shell ViewModel。
    /// </summary>
    /// <param name="refreshRequested">刷新 dashboard 的回调。</param>
    /// <param name="engineChanged">切换 engine 后通知窗口刷新数据的回调。</param>
    /// <param name="commandRequested">发送 Kit/action 命令的回调。</param>
    public WorkbenchShellViewModel(Action refreshRequested, Action<string> engineChanged, Func<string, string, Task> commandRequested)
        : this(
            refreshRequested,
            engineChanged,
            commandRequested,
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            string.Empty)
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
        YokiFramePackageMetadata packageMetadata,
        Func<Uri, Task> openUriAsync)
        : this(
            refreshRequested,
            engineChanged,
            commandRequested,
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            string.Empty,
            packageMetadata,
            openUriAsync)
    {
    }

    /// <summary>
    /// 创建带 FsmKit 详情查询和离线文档服务的完整 Workbench Shell。
    /// </summary>
    /// <param name="refreshRequested">刷新 dashboard 的回调。</param>
    /// <param name="engineChanged">切换 engine 后通知窗口刷新数据的回调。</param>
    /// <param name="commandRequested">发送 Kit/action 命令的回调。</param>
    /// <param name="fsmDetailsQuery">按 instanceId 查询 FsmKit 详情的 Application 用例。</param>
    /// <param name="sourcePackageRoot">启动入口解析出的真实 YokiFrame 包根。</param>
    /// <param name="projectRoot">当前宿主项目根目录。</param>
    /// <param name="documentationService">包内离线文档 Application 服务。</param>
    /// <param name="copyTextAsync">平台剪贴板写入回调。</param>
    /// <param name="documentationInitializationError">文档服务创建失败时的可显示原因。</param>
    /// <param name="packageMetadata">Application 解析完成的 package.json 元数据。</param>
    /// <param name="openUriAsync">平台外部链接打开回调。</param>
    /// <param name="eventKitCodeScanAsync">EventKit 静态代码扫描用例。</param>
    /// <param name="openEventKitCodeLocationAsync">打开 EventKit 源码位置的宿主回调。</param>
    /// <param name="loadLogKitProjectSettings">读取当前项目 LogKit 设置与并发指纹的用例。</param>
    /// <param name="saveLogKitSettingsAsync">保存项目设置并尝试应用到当前 Runtime 的用例。</param>
    /// <param name="clearLogKitHistoryAsync">清空 Runtime 内存历史的用例。</param>
    /// <param name="readLogKitFileAsync">按用户选择读取日志文件尾部的用例。</param>
    /// <param name="setPoolKitTrackingAsync">更新 PoolKit 跟踪选项并回读状态的用例。</param>
    /// <param name="checkPoolKitLeaksAsync">触发 PoolKit 泄漏检查并回读状态的用例。</param>
    /// <param name="clearPoolKitHistoryAsync">清空 PoolKit 事件历史并回读状态的用例。</param>
    /// <param name="openPoolKitCodeLocationAsync">打开 PoolKit 借出对象源码位置的宿主回调。</param>
    /// <param name="getResKitResourceDetailAsync">按需读取 ResKit 资源 lease 来源的用例。</param>
    /// <param name="setResKitTrackingAsync">切换 ResKit 加载位置跟踪并回读状态的用例。</param>
    /// <param name="clearResKitHistoryAsync">清空 ResKit 卸载历史并回读状态的用例。</param>
    /// <param name="openResKitCodeLocationAsync">打开 ResKit lease 来源的宿主回调。</param>
    /// <param name="setActionKitStackTraceAsync">切换 ActionKit 堆栈捕获并回读状态的用例。</param>
    /// <param name="clearActionKitStackTraceAsync">清空 ActionKit 活动堆栈并回读状态的用例。</param>
    /// <param name="scanAudioIndexAsync">扫描 AudioKit 稳定索引的用例。</param>
    /// <param name="generateAudioIndexAsync">生成 AudioKit 稳定索引的用例。</param>
    /// <param name="loadAudioIndexSettings">读取项目独立 AudioKit 索引配置的用例。</param>
    /// <param name="saveAudioIndexSettingsAsync">保存项目独立 AudioKit 索引配置的用例。</param>
    /// <param name="uikitEditorActionAsync">执行 Unity UIKit Editor Tools 强类型操作的用例。</param>
    internal WorkbenchShellViewModel(
        Action refreshRequested,
        Action<string> engineChanged,
        Func<string, string, Task> commandRequested,
        Func<string, CancellationToken, Task<WorkbenchFsmKitState>>? fsmDetailsQuery,
        string sourcePackageRoot,
        string projectRoot,
        OfflineDocumentationService? documentationService,
        Func<string, Task>? copyTextAsync,
        string documentationInitializationError,
        YokiFramePackageMetadata? packageMetadata = null,
        Func<Uri, Task>? openUriAsync = null,
        Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? eventKitCodeScanAsync = null,
        Func<WorkbenchEventKitCodeLocation, Task>? openEventKitCodeLocationAsync = null,
        Func<string, WorkbenchLogKitProjectSettings>? loadLogKitProjectSettings = null,
        Func<string, WorkbenchLogKitSettings, string, CancellationToken, Task<WorkbenchLogKitSettingsSaveResult>>? saveLogKitSettingsAsync = null,
        Func<string, CancellationToken, Task<WorkbenchLogKitState>>? clearLogKitHistoryAsync = null,
        Func<string, string, CancellationToken, Task<WorkbenchLogKitFilePreview>>? readLogKitFileAsync = null,
        Func<string, bool, bool, bool, CancellationToken, Task<WorkbenchPoolKitState>>? setPoolKitTrackingAsync = null,
        Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? checkPoolKitLeaksAsync = null,
        Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? clearPoolKitHistoryAsync = null,
        Func<string, int, Task>? openPoolKitCodeLocationAsync = null,
        Func<string, string, string, CancellationToken, Task<WorkbenchResKitResourceDetail>>? getResKitResourceDetailAsync = null,
        Func<string, bool, CancellationToken, Task<WorkbenchResKitState>>? setResKitTrackingAsync = null,
        Func<string, CancellationToken, Task<WorkbenchResKitState>>? clearResKitHistoryAsync = null,
        Func<WorkbenchResKitLoadSource, Task>? openResKitCodeLocationAsync = null,
        Func<string, bool, CancellationToken, Task<WorkbenchActionKitState>>? setActionKitStackTraceAsync = null,
        Func<string, CancellationToken, Task<WorkbenchActionKitState>>? clearActionKitStackTraceAsync = null,
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? scanAudioIndexAsync = null,
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? generateAudioIndexAsync = null,
        Func<string, AudioIndexSettings>? loadAudioIndexSettings = null,
        Func<string, AudioIndexSettings, CancellationToken, Task>? saveAudioIndexSettingsAsync = null,
        SaveKitWorkbenchSettingsService? saveKitSettingsService = null,
        IInstallerFolderPicker? saveKitFolderPicker = null,
        Func<string, Task>? saveKitOpenDirectoryAsync = null,
        TableKitApplicationService? tableKitApplicationService = null,
        Func<WorkbenchUIKitEditorAction, WorkbenchUIKitPanelGenerationRequest?, CancellationToken, Task<WorkbenchUIKitEditorResult>>? uikitEditorActionAsync = null,
        UIKitEditorSettingsService? uikitEditorSettingsService = null)
        : this(
            refreshRequested, engineChanged, commandRequested, fsmDetailsQuery, sourcePackageRoot, projectRoot,
            documentationService, copyTextAsync, documentationInitializationError, packageMetadata, openUriAsync,
            eventKitCodeScanAsync, openEventKitCodeLocationAsync, loadLogKitProjectSettings, saveLogKitSettingsAsync,
            clearLogKitHistoryAsync, readLogKitFileAsync, setPoolKitTrackingAsync, checkPoolKitLeaksAsync,
            clearPoolKitHistoryAsync, openPoolKitCodeLocationAsync, getResKitResourceDetailAsync, setResKitTrackingAsync, clearResKitHistoryAsync,
            openResKitCodeLocationAsync, setActionKitStackTraceAsync, clearActionKitStackTraceAsync, scanAudioIndexAsync,
            generateAudioIndexAsync, loadAudioIndexSettings, saveAudioIndexSettingsAsync, saveKitSettingsService,
            saveKitFolderPicker, saveKitOpenDirectoryAsync, tableKitApplicationService, false,
            uikitEditorActionAsync, uikitEditorSettingsService)
    {
    }

    /// <summary>创建带 SaveKit Application 服务和目录选择器的完整 Shell。</summary>
    private WorkbenchShellViewModel(
        Action refreshRequested, Action<string> engineChanged, Func<string, string, Task> commandRequested,
        Func<string, CancellationToken, Task<WorkbenchFsmKitState>>? fsmDetailsQuery, string sourcePackageRoot,
        string projectRoot, OfflineDocumentationService? documentationService, Func<string, Task>? copyTextAsync,
        string documentationInitializationError, YokiFramePackageMetadata? packageMetadata, Func<Uri, Task>? openUriAsync,
        Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? eventKitCodeScanAsync,
        Func<WorkbenchEventKitCodeLocation, Task>? openEventKitCodeLocationAsync,
        Func<string, WorkbenchLogKitProjectSettings>? loadLogKitProjectSettings,
        Func<string, WorkbenchLogKitSettings, string, CancellationToken, Task<WorkbenchLogKitSettingsSaveResult>>? saveLogKitSettingsAsync,
        Func<string, CancellationToken, Task<WorkbenchLogKitState>>? clearLogKitHistoryAsync,
        Func<string, string, CancellationToken, Task<WorkbenchLogKitFilePreview>>? readLogKitFileAsync,
        Func<string, bool, bool, bool, CancellationToken, Task<WorkbenchPoolKitState>>? setPoolKitTrackingAsync,
        Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? checkPoolKitLeaksAsync,
        Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? clearPoolKitHistoryAsync,
        Func<string, int, Task>? openPoolKitCodeLocationAsync,
        Func<string, string, string, CancellationToken, Task<WorkbenchResKitResourceDetail>>? getResKitResourceDetailAsync,
        Func<string, bool, CancellationToken, Task<WorkbenchResKitState>>? setResKitTrackingAsync,
        Func<string, CancellationToken, Task<WorkbenchResKitState>>? clearResKitHistoryAsync,
        Func<WorkbenchResKitLoadSource, Task>? openResKitCodeLocationAsync,
        Func<string, bool, CancellationToken, Task<WorkbenchActionKitState>>? setActionKitStackTraceAsync,
        Func<string, CancellationToken, Task<WorkbenchActionKitState>>? clearActionKitStackTraceAsync,
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? scanAudioIndexAsync,
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? generateAudioIndexAsync,
        Func<string, AudioIndexSettings>? loadAudioIndexSettings,
        Func<string, AudioIndexSettings, CancellationToken, Task>? saveAudioIndexSettingsAsync,
        SaveKitWorkbenchSettingsService? saveKitSettingsService,
        IInstallerFolderPicker? saveKitFolderPicker,
        Func<string, Task>? saveKitOpenDirectoryAsync,
        TableKitApplicationService? tableKitApplicationService,
        bool _saveKitConstructorMarker = false,
        Func<WorkbenchUIKitEditorAction, WorkbenchUIKitPanelGenerationRequest?, CancellationToken, Task<WorkbenchUIKitEditorResult>>? uikitEditorActionAsync = null,
        UIKitEditorSettingsService? uikitEditorSettingsService = null)
    {
        mRefreshRequested = refreshRequested;
        mEngineChanged = engineChanged;
        mCommandRequested = commandRequested;
        mOpenUriAsync = openUriAsync;
        ApplyPackageMetadata(packageMetadata);
        RefreshCommand = new RelayCommand(RefreshWorkbench);
        ClearLogCommand = new RelayCommand(ClearLogLines);
        SendSelectedCommand = new RelayCommand(() => _ = mCommandRequested(CommandGroup, CommandAction));
        PingCommand = new RelayCommand(() => _ = mCommandRequested("System", "ping"));
        BridgeStatusCommand = new RelayCommand(() => _ = mCommandRequested("System", "bridge_status"));
        RefreshCommandCatalogCommand = new RelayCommand(() => _ = mCommandRequested("System", "list_commands"));
        OpenRepositoryCommand = new AsyncRelayCommand(OpenRepositoryAsync, CanOpenRepository);
        EventKitPage = new EventKitPageViewModel(
            eventKitCodeScanAsync,
            openEventKitCodeLocationAsync);
        FsmKitPage = new FsmKitPageViewModel(fsmDetailsQuery);
        LogKitPage = new LogKitPageViewModel(
            loadLogKitProjectSettings,
            saveLogKitSettingsAsync,
            clearLogKitHistoryAsync,
            readLogKitFileAsync);
        PoolKitPage = new PoolKitPageViewModel(
            setPoolKitTrackingAsync,
            checkPoolKitLeaksAsync,
            clearPoolKitHistoryAsync,
            openPoolKitCodeLocationAsync);
        ResKitPage = new ResKitPageViewModel(
            getResKitResourceDetailAsync,
            setResKitTrackingAsync,
            clearResKitHistoryAsync,
            openResKitCodeLocationAsync);
        ActionKitPage = new ActionKitPageViewModel(
            setActionKitStackTraceAsync,
            clearActionKitStackTraceAsync);
        AudioKitPage = new AudioKitPageViewModel(
            scanAudioIndexAsync, generateAudioIndexAsync,
            loadAudioIndexSettings, saveAudioIndexSettingsAsync);
        SpatialKitPage = new SpatialKitPageViewModel();
        UIKitPage = new UIKitPageViewModel(
            copyTextAsync,
            uikitEditorActionAsync,
            uikitEditorSettingsService);
        TableKitPage = new TableKitPageViewModel(
            projectRoot,
            tableKitApplicationService ?? new TableKitApplicationService(),
            copyTextAsync,
            saveKitFolderPicker);
        LocalizationKitPage = new LocalizationKitPageViewModel(
            projectRoot,
            new YokiFrame.Tooling.Application.Services.LocalizationKit.LocalizationKitApplicationService(),
            saveKitFolderPicker,
            saveKitOpenDirectoryAsync);
        SaveKitPage = new SaveKitPageViewModel(saveKitSettingsService, saveKitFolderPicker, saveKitOpenDirectoryAsync);
        DocumentationPage = new DocumentationPageViewModel(
            sourcePackageRoot,
            documentationService,
            copyTextAsync,
            documentationInitializationError);
        RuntimeUpdate = new WorkbenchRuntimeUpdateViewModel(sourcePackageRoot, projectRoot);
        InitializeWorkbenchLayout();
        InitializeSkillInstaller();
        UpdateCurrentPage();
    }
}
