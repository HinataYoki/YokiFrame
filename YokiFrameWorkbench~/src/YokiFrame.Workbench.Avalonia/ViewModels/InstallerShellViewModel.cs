using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 把 Installer Application 会话投影为旧版一致的单页安装工作流。
/// </summary>
public sealed partial class InstallerShellViewModel : ViewModelBase, IDisposable
{
    private const string DEFAULT_GIT_URL = "https://github.com/HinataYoki/YokiFrame.git";

    private readonly InstallerSessionService mSession;
    private readonly InstallerTargetDetectionService mTargetDetection;
    private readonly InstallerInputDetectionService mInputDetection;
    private readonly IInstallerFolderPicker mFolderPicker;
    private readonly IGodotRuntimeBootstrapper mGodotRuntimeBootstrapper;
    private readonly SemaphoreSlim mGodotRuntimeBootstrapGate = new(1, 1);
    private readonly SynchronizationContext? mSynchronizationContext;
    private string mSourcePackageRoot;
    private string mTargetProjectRoot;
    private string mGitUrl = DEFAULT_GIT_URL;
    private string mEngineStatusText = WorkbenchI18nService.Instance.GetString(
        "String.Installer.EngineNotDetected",
        "未检测");
    private string mTargetStatusText = WorkbenchI18nService.Instance.GetString(
        "String.Installer.TargetWaiting",
        "等待选择目录");
    private string mSessionStatusText = WorkbenchI18nService.Instance.GetString(
        "String.Installer.Session.Ready",
        "安装器已就绪");
    private InstallerTargetKind mTargetKind;
    private InstallerInstallMode mInstallMode = InstallerInstallMode.UnityLocal;
    private bool mRepairGodotProjectSettings = true;
    private bool mEnableGodotPlugin = true;
    private bool mConfirmLegacyTakeover;
    private bool mIsTakeoverConfirmationVisible;
    private bool mIsProgressVisible;
    private bool mIsCompletionSummaryVisible;
    private bool mIsOutcomeDetailsVisible;
    private double mProgressValue;
    private string mCompletionSummaryText = string.Empty;
    private string mOutcomeDetailsTitle = string.Empty;
    private string mOutcomeDetailsText = string.Empty;
    private string mPlanActionsText = WorkbenchI18nService.Instance.GetString(
        "String.Installer.Plan.Waiting",
        "等待生成安装计划");
    private string mPlanWarningsText = string.Empty;
    private bool mIsPlanWarningVisible;
    private int mProjectedLogCount;
    private InstallerPlanPreview? mPresentedPlan;
    private InstallerExecutionResult? mPresentedResult;
    private bool mIsGodotRuntimeBootstrapRunning;
    private bool mIsGodotRuntimeBootstrapOpeningInstaller;
    private bool mIsDisposed;

    /// <summary>
    /// 创建 Installer ViewModel 并注入 Application 会话、自动检测和原生目录选择边界。
    /// </summary>
    /// <param name="startupOptions">启动默认路径。</param>
    /// <param name="session">Installer Application 会话。</param>
    /// <param name="targetDetection">目标项目检测服务。</param>
    /// <param name="inputDetection">输入节流服务。</param>
    /// <param name="folderPicker">跨平台原生目录选择器。</param>
    public InstallerShellViewModel(
        ToolStartupOptions startupOptions,
        InstallerSessionService session,
        InstallerTargetDetectionService targetDetection,
        InstallerInputDetectionService inputDetection,
        IInstallerFolderPicker folderPicker)
        : this(
            startupOptions,
            session,
            targetDetection,
            inputDetection,
            folderPicker,
            new GodotRuntimeBootstrapper())
    {
    }

    /// <summary>
    /// 创建可替换 Godot Runtime bootstrap 进程边界的 Installer ViewModel，供测试隔离真实构建进程。
    /// </summary>
    /// <param name="startupOptions">启动默认路径。</param>
    /// <param name="session">Installer Application 会话。</param>
    /// <param name="targetDetection">目标 Unity/Godot 项目检测器。</param>
    /// <param name="inputDetection">输入防抖与 latest-wins 调度器。</param>
    /// <param name="folderPicker">跨平台原生目录选择器。</param>
    /// <param name="godotRuntimeBootstrapper">从源码包构建并重新打开 Installer 的进程边界。</param>
    internal InstallerShellViewModel(
        ToolStartupOptions startupOptions,
        InstallerSessionService session,
        InstallerTargetDetectionService targetDetection,
        InstallerInputDetectionService inputDetection,
        IInstallerFolderPicker folderPicker,
        IGodotRuntimeBootstrapper godotRuntimeBootstrapper)
    {
        ArgumentNullException.ThrowIfNull(startupOptions);
        mSession = session ?? throw new ArgumentNullException(nameof(session));
        mTargetDetection = targetDetection ?? throw new ArgumentNullException(nameof(targetDetection));
        mInputDetection = inputDetection ?? throw new ArgumentNullException(nameof(inputDetection));
        mFolderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        mGodotRuntimeBootstrapper = godotRuntimeBootstrapper ?? throw new ArgumentNullException(nameof(godotRuntimeBootstrapper));
        mSynchronizationContext = SynchronizationContext.Current;
        mSourcePackageRoot = startupOptions.SourcePackageRoot;
        mTargetProjectRoot = startupOptions.TargetProjectRoot;
        PickSourceCommand = new AsyncRelayCommand(PickSourceAsync);
        PickTargetCommand = new AsyncRelayCommand(PickTargetAsync);
        PreviewCommand = new AsyncRelayCommand(PreviewPlanAsync, CanPreviewPlan);
        InstallCommand = new AsyncRelayCommand(InstallAsync, CanInstallPlan);
        RetryCommand = new AsyncRelayCommand(RefreshPlanAsync, CanRetryPlan);
        BootstrapGodotRuntimeCommand = new AsyncRelayCommand(
            BootstrapGodotRuntimeAsync,
            CanBootstrapGodotRuntime);
        ClearLogCommand = new RelayCommand(ClearLog);
        mSession.StateChanged += OnSessionStateChanged;
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        AppendLocalLog(WorkbenchI18nService.Instance.GetString(
            "String.Installer.Log.Ready",
            "安装器已就绪。"));
    }

    /// <summary>
    /// 获取或设置本地 YokiFrame 源包根。
    /// </summary>
    public string SourcePackageRoot
    {
        get => mSourcePackageRoot;
        set
        {
            if (SetProperty(ref mSourcePackageRoot, value ?? string.Empty))
            {
                ScheduleAutomaticRefresh();
            }
        }
    }

    /// <summary>
    /// 获取或设置目标 Unity 或 Godot 项目根。
    /// </summary>
    public string TargetProjectRoot
    {
        get => mTargetProjectRoot;
        set
        {
            if (SetProperty(ref mTargetProjectRoot, value ?? string.Empty))
            {
                mConfirmLegacyTakeover = false;
                OnPropertyChanged(nameof(ConfirmLegacyTakeover));
                ScheduleAutomaticRefresh();
            }
        }
    }

    /// <summary>
    /// 获取或设置 Unity Git package URL。
    /// </summary>
    public string GitUrl
    {
        get => mGitUrl;
        set
        {
            if (SetProperty(ref mGitUrl, value ?? string.Empty))
            {
                ScheduleAutomaticRefresh();
            }
        }
    }

    /// <summary>
    /// 获取或设置是否选择 Unity 本地 embedded package。
    /// </summary>
    public bool IsUnityLocalSelected
    {
        get => mInstallMode == InstallerInstallMode.UnityLocal;
        set
        {
            if (value)
            {
                SetInstallMode(InstallerInstallMode.UnityLocal);
            }
        }
    }

    /// <summary>
    /// 获取或设置是否选择 Unity Git URL package。
    /// </summary>
    public bool IsUnityGitSelected
    {
        get => mInstallMode == InstallerInstallMode.UnityGit;
        set
        {
            if (value)
            {
                SetInstallMode(InstallerInstallMode.UnityGit);
            }
        }
    }

    /// <summary>
    /// 获取或设置是否维护 Godot project.godot 中 YokiFrame owner 项。
    /// </summary>
    public bool RepairGodotProjectSettings
    {
        get => mRepairGodotProjectSettings;
        set
        {
            if (SetProperty(ref mRepairGodotProjectSettings, value))
            {
                ScheduleAutomaticRefresh();
            }
        }
    }

    /// <summary>
    /// 获取或设置是否登记并启用 Godot YokiFrame 插件。
    /// </summary>
    public bool EnableGodotPlugin
    {
        get => mEnableGodotPlugin;
        set
        {
            if (SetProperty(ref mEnableGodotPlugin, value))
            {
                ScheduleAutomaticRefresh();
            }
        }
    }

    /// <summary>
    /// 获取或设置用户是否明确确认接管 unmanaged legacy 包。
    /// </summary>
    public bool ConfirmLegacyTakeover
    {
        get => mConfirmLegacyTakeover;
        set
        {
            if (SetProperty(ref mConfirmLegacyTakeover, value))
            {
                ScheduleAutomaticRefresh();
            }
        }
    }

    /// <summary>
    /// 获取当前源目录输入是否可见；Unity Git 模式隐藏。
    /// </summary>
    public bool IsSourcePathVisible => mInstallMode != InstallerInstallMode.UnityGit;

    /// <summary>
    /// 获取是否已经识别到支持的目标项目。
    /// </summary>
    public bool IsEngineOptionsVisible => mTargetKind != InstallerTargetKind.Unknown;

    /// <summary>
    /// 获取 Unity 本地/Git 模式选项是否可见。
    /// </summary>
    public bool IsUnityOptionsVisible => mTargetKind == InstallerTargetKind.Unity;

    /// <summary>
    /// 获取 Godot 项目选项是否可见。
    /// </summary>
    public bool IsGodotOptionsVisible => mTargetKind == InstallerTargetKind.Godot;

    /// <summary>
    /// 获取 Godot Runtime 缓存失配时是否显示一键构建并重新打开 Installer 的恢复入口。
    /// </summary>
    public bool IsGodotRuntimeBootstrapVisible => CanBootstrapGodotRuntime();

    /// <summary>
    /// 获取 Unity Git URL 输入是否可见。
    /// </summary>
    public bool IsGitUrlVisible => IsUnityOptionsVisible && IsUnityGitSelected;

    /// <summary>
    /// 获取当前平台标签是否可见。
    /// </summary>
    public bool IsCurrentPlatformVisible => IsUnityOptionsVisible;

    /// <summary>
    /// 获取当前工具平台名称。
    /// </summary>
    public string CurrentPlatformText => GetCurrentPlatformText();

    /// <summary>
    /// 获取当前选择的安装方式，用于摘要区与输入区保持同一事实源。
    /// </summary>
    public string SelectedInstallModeText => GetModeText(mInstallMode);

    /// <summary>
    /// 获取当前计划动作的中文多行摘要；尚未生成计划时返回等待提示。
    /// </summary>
    public string PlanActionsText
    {
        get => mPlanActionsText;
        private set => SetProperty(ref mPlanActionsText, value);
    }

    /// <summary>
    /// 获取当前计划中需要用户知晓但不阻止执行的影响说明。
    /// </summary>
    public string PlanWarningsText
    {
        get => mPlanWarningsText;
        private set => SetProperty(ref mPlanWarningsText, value);
    }

    /// <summary>
    /// 获取当前计划是否包含需要在摘要区突出显示的非阻断警告。
    /// </summary>
    public bool IsPlanWarningVisible
    {
        get => mIsPlanWarningVisible;
        private set => SetProperty(ref mIsPlanWarningVisible, value);
    }

    /// <summary>
    /// 获取检测到的目标引擎文本。
    /// </summary>
    public string EngineStatusText
    {
        get => mEngineStatusText;
        private set => SetProperty(ref mEngineStatusText, value);
    }

    /// <summary>
    /// 获取实际安装目标路径。
    /// </summary>
    public string TargetStatusText
    {
        get => mTargetStatusText;
        private set => SetProperty(ref mTargetStatusText, value);
    }

    /// <summary>
    /// 获取当前会话状态的用户可读文本。
    /// </summary>
    public string SessionStatusText
    {
        get => mSessionStatusText;
        private set => SetProperty(ref mSessionStatusText, value);
    }

    /// <summary>
    /// 获取当前进度百分比。
    /// </summary>
    public double ProgressValue
    {
        get => mProgressValue;
        private set => SetProperty(ref mProgressValue, value);
    }

    /// <summary>
    /// 获取进度条是否可见。
    /// </summary>
    public bool IsProgressVisible
    {
        get => mIsProgressVisible;
        private set => SetProperty(ref mIsProgressVisible, value);
    }

    /// <summary>
    /// 获取当前进度条是否表示无法预测耗时的 Runtime 构建，而不是可计数的安装事务阶段。
    /// </summary>
    public bool IsProgressIndeterminate => mIsGodotRuntimeBootstrapRunning;

    /// <summary>
    /// 获取安装成功摘要是否可见。
    /// </summary>
    public bool IsCompletionSummaryVisible
    {
        get => mIsCompletionSummaryVisible;
        private set => SetProperty(ref mIsCompletionSummaryVisible, value);
    }

    /// <summary>
    /// 获取安装成功后的引擎、模式、平台、目标和证据摘要。
    /// </summary>
    public string CompletionSummaryText
    {
        get => mCompletionSummaryText;
        private set => SetProperty(ref mCompletionSummaryText, value);
    }

    /// <summary>
    /// 获取冲突或事务失败详情是否可见。
    /// </summary>
    public bool IsOutcomeDetailsVisible
    {
        get => mIsOutcomeDetailsVisible;
        private set => SetProperty(ref mIsOutcomeDetailsVisible, value);
    }

    /// <summary>
    /// 获取冲突或事务失败详情标题。
    /// </summary>
    public string OutcomeDetailsTitle
    {
        get => mOutcomeDetailsTitle;
        private set => SetProperty(ref mOutcomeDetailsTitle, value);
    }

    /// <summary>
    /// 获取冲突路径、回滚结论和诊断证据组成的详情文本。
    /// </summary>
    public string OutcomeDetailsText
    {
        get => mOutcomeDetailsText;
        private set => SetProperty(ref mOutcomeDetailsText, value);
    }

    /// <summary>
    /// 获取当前是否允许显示重试入口。
    /// </summary>
    public bool CanRetry => CanRetryPlan();

    /// <summary>
    /// 获取是否显示 legacy 接管确认项。
    /// </summary>
    public bool IsTakeoverConfirmationVisible
    {
        get => mIsTakeoverConfirmationVisible;
        private set => SetProperty(ref mIsTakeoverConfirmationVisible, value);
    }

    /// <summary>
    /// 获取当前可见日志集合。
    /// </summary>
    public ObservableCollection<InstallerLogLine> LogEntries { get; } = new();

    /// <summary>
    /// 获取选择源目录命令。
    /// </summary>
    public AsyncRelayCommand PickSourceCommand { get; }

    /// <summary>
    /// 获取选择目标项目命令。
    /// </summary>
    public AsyncRelayCommand PickTargetCommand { get; }

    /// <summary>
    /// 获取生成安装预览命令。
    /// </summary>
    public AsyncRelayCommand PreviewCommand { get; }

    /// <summary>
    /// 获取执行安装命令。
    /// </summary>
    public AsyncRelayCommand InstallCommand { get; }

    /// <summary>
    /// 获取错误或冲突后的重试命令。
    /// </summary>
    public AsyncRelayCommand RetryCommand { get; }

    /// <summary>
    /// 获取从当前源码包构建 Godot Runtime 并打开新 Installer 的恢复命令。
    /// </summary>
    public AsyncRelayCommand BootstrapGodotRuntimeCommand { get; }

    /// <summary>
    /// 获取清空当前日志显示命令。
    /// </summary>
    public RelayCommand ClearLogCommand { get; }

    /// <summary>
    /// 解除会话和语言事件订阅，避免关闭 Installer 后静态服务继续持有页面状态。
    /// </summary>
    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        mSession.StateChanged -= OnSessionStateChanged;
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
    }
}
