using System.Collections.ObjectModel;
using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels.LogKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 把 LogKit 项目设置、运行统计、按需文件尾部和高频内存历史投影到单页工作台。
/// </summary>
public sealed partial class LogKitPageViewModel : ViewModelBase, IDisposable
{
    private const string MEMORY_SOURCE = "memory";
    private const string EDITOR_SOURCE = "editor";
    private const string PLAYER_SOURCE = "player";
    private static readonly IReadOnlyList<string> sSettingsLevelOptions =
        new[] { "Debug", "Info", "Warning", "Error" };
    /// <summary>内存历史等级筛选的“全部”哨兵值；与具体等级值互不冲突，展示文本由资源投影。</summary>
    internal const string HISTORY_LEVEL_ALL = "all";
    private readonly Func<string, WorkbenchLogKitProjectSettings>? mLoadProjectSettings;
    private readonly Func<string, WorkbenchLogKitSettings, string, CancellationToken, Task<WorkbenchLogKitSettingsSaveResult>>? mSaveSettingsAsync;
    private readonly Func<string, CancellationToken, Task<WorkbenchLogKitState>>? mClearHistoryAsync;
    private readonly Func<string, string, CancellationToken, Task<WorkbenchLogKitFilePreview>>? mReadFileAsync;
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private CancellationTokenSource mIdentityCancellation = new();
    private Func<string, Task>? mOpenDirectoryAsync;
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private string mMode = string.Empty;
    private string mSource = GetString(CommonWaitingForKey, "等待数据");
    private string mStaleReason = string.Empty;
    private string mLoggerName = GetString(LoggerNotInstalledKey, "未安装");
    private string mRuntimeMinimumLevel = "--";
    private long mGeneration;
    private long mDiagnosticVersion;
    private long mSettingsVersion;
    private int mHistoryCount;
    private int mDroppedCount;
    private bool mRuntimeEnabled;
    private bool mSupportsSettingsApply;
    private bool mSupportsFilePreview;
    private bool mSupportsFileWriter;
    private bool mSupportsPlayerImGui;
    private bool mSupportsEncryption;
    private bool mIsPageActive;
    private bool mIsDisposed;

    /// <summary>创建不具备 Application 写操作的设计时页面。</summary>
    public LogKitPageViewModel()
        : this(null, null, null, null)
    {
    }

    /// <summary>创建通过强类型 Application 用例读写 LogKit 的真实页面。</summary>
    internal LogKitPageViewModel(
        Func<string, WorkbenchLogKitProjectSettings>? loadProjectSettings,
        Func<string, WorkbenchLogKitSettings, string, CancellationToken, Task<WorkbenchLogKitSettingsSaveResult>>? saveSettingsAsync,
        Func<string, CancellationToken, Task<WorkbenchLogKitState>>? clearHistoryAsync,
        Func<string, string, CancellationToken, Task<WorkbenchLogKitFilePreview>>? readFileAsync)
    {
        mLoadProjectSettings = loadProjectSettings;
        mSaveSettingsAsync = saveSettingsAsync;
        mClearHistoryAsync = clearHistoryAsync;
        mReadFileAsync = readFileAsync;
        // 订阅全局语言切换；对应解除订阅在 Dispose，由 WorkbenchWindow 关闭流程统一调用。
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        SettingsDraft.Changed += OnSettingsDraftChanged;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, CanSaveSettings);
        ResetSettingsCommand = new RelayCommand(ResetSettingsToDefaults);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync, CanClearHistory);
        RefreshFileCommand = new AsyncRelayCommand(RefreshSelectedFileAsync, CanRefreshFile);
        OpenDirectoryCommand = new AsyncRelayCommand(OpenDirectoryAsync, CanOpenDirectory);
        SelectMemorySourceCommand = new RelayCommand(() => SelectedSource = MEMORY_SOURCE);
        SelectEditorSourceCommand = new RelayCommand(() => SelectedSource = EDITOR_SOURCE);
        SelectPlayerSourceCommand = new RelayCommand(() => SelectedSource = PLAYER_SOURCE);
    }

    /// <summary>获取配置草稿。</summary>
    public LogKitSettingsDraft SettingsDraft { get; } = new();
    /// <summary>获取筛选后的虚拟化内存历史。</summary>
    public ObservableCollection<LogKitHistoryRowViewModel> HistoryRows { get; } = new();
    /// <summary>获取设置最低等级选项。</summary>
    public IReadOnlyList<string> SettingsLevelOptions => sSettingsLevelOptions;
    /// <summary>获取内存历史等级筛选选项；“全部”展示文本随语言投影。</summary>
    public IReadOnlyList<string> HistoryLevelOptions =>
        new[] { GetString("String.LogKit.LevelAll", "全部"), "Debug", "Info", "Warning", "Error" };
    /// <summary>获取保存设置命令。</summary>
    public AsyncRelayCommand SaveSettingsCommand { get; }
    /// <summary>获取恢复默认草稿命令。</summary>
    public ICommand ResetSettingsCommand { get; }
    /// <summary>获取清空 Runtime 内存历史命令。</summary>
    public AsyncRelayCommand ClearHistoryCommand { get; }
    /// <summary>获取显式刷新当前文件尾部命令。</summary>
    public AsyncRelayCommand RefreshFileCommand { get; }
    /// <summary>获取打开当前 Runtime 日志目录的命令。</summary>
    public AsyncRelayCommand OpenDirectoryCommand { get; }
    /// <summary>获取切换到内存历史的命令。</summary>
    public ICommand SelectMemorySourceCommand { get; }
    /// <summary>获取切换到 Editor 文件的命令。</summary>
    public ICommand SelectEditorSourceCommand { get; }
    /// <summary>获取切换到 Player 文件的命令。</summary>
    public ICommand SelectPlayerSourceCommand { get; }

    /// <summary>获取目标 engine。</summary>
    public string EngineId { get => mEngineId; private set => SetProperty(ref mEngineId, value); }
    /// <summary>获取宿主 session。</summary>
    public string SessionId { get => mSessionId; private set => SetProperty(ref mSessionId, value); }
    /// <summary>获取宿主 generation。</summary>
    public long Generation { get => mGeneration; private set => SetProperty(ref mGeneration, value); }
    /// <summary>获取 Runtime 日志诊断单调版本。</summary>
    public long DiagnosticVersion { get => mDiagnosticVersion; private set => SetProperty(ref mDiagnosticVersion, value); }
    /// <summary>获取 Runtime 设置单调版本。</summary>
    public long SettingsVersion { get => mSettingsVersion; private set => SetProperty(ref mSettingsVersion, value); }
    /// <summary>获取宿主模式。</summary>
    public string Mode { get => mMode; private set => SetProperty(ref mMode, value); }
    /// <summary>获取当前数据来源。</summary>
    public string Source { get => mSource; private set => SetSource(value); }
    /// <summary>获取当前来源诊断。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }
    /// <summary>获取当前 logger 名称。</summary>
    public string LoggerName { get => mLoggerName; private set => SetProperty(ref mLoggerName, value); }
    /// <summary>获取 Runtime 当前最低等级。</summary>
    public string RuntimeMinimumLevel { get => mRuntimeMinimumLevel; private set => SetProperty(ref mRuntimeMinimumLevel, value); }
    /// <summary>获取 Runtime 内存历史总量。</summary>
    public int HistoryCount { get => mHistoryCount; private set => SetHistoryCount(value); }
    /// <summary>获取 Runtime 丢弃日志数量。</summary>
    public int DroppedCount { get => mDroppedCount; private set => SetProperty(ref mDroppedCount, value); }
    /// <summary>获取 Runtime 是否启用 LogKit。</summary>
    public bool RuntimeEnabled { get => mRuntimeEnabled; private set => SetProperty(ref mRuntimeEnabled, value); }
    /// <summary>获取宿主是否支持即时应用设置。</summary>
    public bool SupportsSettingsApply { get => mSupportsSettingsApply; private set => SetCapability(ref mSupportsSettingsApply, value); }
    /// <summary>获取宿主是否支持文件尾部预览。</summary>
    public bool SupportsFilePreview { get => mSupportsFilePreview; private set => SetCapability(ref mSupportsFilePreview, value); }
    /// <summary>获取宿主是否支持日志文件写入。</summary>
    public bool SupportsFileWriter { get => mSupportsFileWriter; private set => SetProperty(ref mSupportsFileWriter, value); }
    /// <summary>获取宿主是否支持 Player IMGUI。</summary>
    public bool SupportsPlayerImGui { get => mSupportsPlayerImGui; private set => SetProperty(ref mSupportsPlayerImGui, value); }
    /// <summary>获取宿主是否支持日志加密。</summary>
    public bool SupportsEncryption
    {
        get => mSupportsEncryption;
        private set
        {
            if (!SetProperty(ref mSupportsEncryption, value))
            {
                return;
            }

            OnPropertyChanged(nameof(EncryptionStatusText));
            OnPropertyChanged(nameof(EncryptionMethodText));
            OnPropertyChanged(nameof(EncryptionToggleToolTip));
            OnPropertyChanged(nameof(EncryptionToggleValue));
        }
    }
    /// <summary>获取日志加密能力状态，避免把配置请求误认为已实现功能。</summary>
    public string EncryptionStatusText => SupportsEncryption
        ? GetString("String.LogKit.EncryptionSupported", "Runtime 已声明支持")
        : GetString("String.LogKit.EncryptionNotImplemented", "当前未实现");
    /// <summary>获取日志解密能力状态；当前协议未提供解密入口。</summary>
    public string DecryptionStatusText => GetString("String.LogKit.DecryptionUnavailable", "当前不可用");
    /// <summary>获取当前版本实际使用的日志加密方式说明。</summary>
    public string EncryptionMethodText => SupportsEncryption
        ? GetString("String.LogKit.EncryptionMethodCapability", "由 Runtime capability 声明，当前页面未暴露算法")
        : GetString("String.LogKit.EncryptionMethodUndefined", "未定义；当前不会使用固定 Key/IV");
    /// <summary>获取日志加密开关的说明性提示。</summary>
    public string EncryptionToggleToolTip => SupportsEncryption
        ? GetString("String.LogKit.EncryptionTooltipSupported", "Runtime 已声明加密能力，但当前版本未提供解密入口和算法详情。")
        : GetString("String.LogKit.EncryptionTooltipUnsupported", "当前 Runtime 未实现可信日志加密，保存此开关不会产生加密日志。");
    /// <summary>获取或设置界面上的有效加密状态；不支持 capability 时始终显示关闭。</summary>
    public bool EncryptionToggleValue
    {
        get => SupportsEncryption && SettingsDraft.EnableEncryption;
        set
        {
            if (SupportsEncryption)
            {
                SettingsDraft.EnableEncryption = value;
            }
        }
    }
    /// <summary>获取标题栏的数据通道。</summary>
    public string DataChannelText => string.Equals(Source, "telemetry", StringComparison.OrdinalIgnoreCase)
        ? "Shared Memory"
        : (string.Equals(Source, "snapshot", StringComparison.OrdinalIgnoreCase) ? "FileBridge" : Source);
    /// <summary>获取运行状态摘要。</summary>
    public string RuntimeStatusText => RuntimeEnabled
        ? GetString("String.LogKit.RuntimeEnabled", "已启用")
        : GetString("String.LogKit.RuntimeDisabled", "已停用");
    /// <summary>获取内存历史与丢弃计数摘要。</summary>
    public string RuntimeHistoryText => string.Format(
        GetString("String.LogKit.RuntimeHistoryTemplate", "{0} 条 / 丢弃 {1}"), HistoryCount, DroppedCount);

    /// <summary>应用低频 dashboard 状态，并通过稳定集合协调避免整页重建。</summary>
    /// <param name="state">Application 解析后的 LogKit 状态。</param>
    public void ApplyPeriodicState(WorkbenchLogKitState? state)
    {
        if (state == null)
        {
            ResetRuntimeState();
            return;
        }

        if (IsOlderSameHostState(state))
        {
            ReportTelemetryIssue(state.StaleReason);
            return;
        }

        ApplyState(state, true);
    }

    /// <summary>应用 Shared Memory 新帧，并拒绝与当前宿主身份不一致的结果。</summary>
    internal bool TryApplyTelemetryState(WorkbenchLogKitState state)
    {
        if (!CanAcceptTelemetryState(state))
        {
            return false;
        }

        if (IsOlderSameHostState(state))
        {
            return true;
        }

        ApplyState(state, false);
        return true;
    }

    /// <summary>显示高频读取诊断，同时保留最后一帧有效日志。</summary>
    internal void ReportTelemetryIssue(string diagnostic)
    {
        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            StaleReason = diagnostic;
        }
    }

    /// <summary>通知页面是否激活；文件读取只在激活和显式切换时发生。</summary>
    internal void SetPageActive(bool isActive)
    {
        if (mIsDisposed || mIsPageActive == isActive)
        {
            return;
        }

        mIsPageActive = isActive;
        if (!isActive)
        {
            CancelFilePreview();
            return;
        }

        EnsureProjectSettingsLoaded();
        QueueSelectedFilePreview();
    }

    /// <summary>取消页面全部异步操作并解除草稿事件。</summary>
    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        SettingsDraft.Changed -= OnSettingsDraftChanged;
        mLifetimeCancellation.Cancel();
        mIdentityCancellation.Cancel();
        CancelFilePreview();
        mIdentityCancellation.Dispose();
        mLifetimeCancellation.Dispose();
    }

    /// <summary>提交状态元数据、能力、文件和稳定历史。</summary>
    private void ApplyState(WorkbenchLogKitState state, bool loadProjectSettings)
    {
        var identityChanged = !MatchesIdentity(state.EngineId, state.SessionId, state.Generation);
        if (identityChanged)
        {
            ReplaceIdentity(state.EngineId, state.SessionId, state.Generation);
        }

        ApplyRuntimeSummary(state);
        ApplyFileMetadata(state.Files);
        ReconcileHistory(state.History.Entries);
        if (loadProjectSettings && (identityChanged || !mProjectSettingsLoaded))
        {
            EnsureProjectSettingsLoaded();
            QueueSelectedFilePreview();
        }
    }

    /// <summary>更新 Runtime 元数据、统计和能力声明。</summary>
    private void ApplyRuntimeSummary(WorkbenchLogKitState state)
    {
        EngineId = state.EngineId;
        SessionId = state.SessionId;
        Generation = state.Generation;
        DiagnosticVersion = state.DiagnosticVersion;
        SettingsVersion = state.SettingsVersion;
        Mode = state.Mode;
        Source = state.Source;
        StaleReason = state.StaleReason;
        LoggerName = state.Stats.HasLogger ? state.Stats.LoggerName : GetString(LoggerNotInstalledKey, "未安装");
        RuntimeEnabled = state.Stats.Enabled;
        RuntimeMinimumLevel = state.Stats.MinimumLevel;
        HistoryCount = state.History.TotalCount;
        DroppedCount = state.History.DroppedCount;
        SupportsSettingsApply = state.Capabilities.SettingsApply;
        SupportsFilePreview = state.Capabilities.FilePreview;
        SupportsFileWriter = state.Capabilities.FileWriter;
        SupportsPlayerImGui = state.Capabilities.PlayerImGui;
        SupportsEncryption = state.Capabilities.Encryption;
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(RuntimeHistoryText));
    }

    /// <summary>判断 telemetry 帧是否属于当前宿主；首帧允许建立尚为空的身份。</summary>
    private bool CanAcceptTelemetryState(WorkbenchLogKitState state)
    {
        return string.IsNullOrWhiteSpace(SessionId)
            || MatchesIdentity(state.EngineId, state.SessionId, state.Generation);
    }

    /// <summary>判断同宿主周期状态是否落后于页面已经接受的诊断版本。</summary>
    private bool IsOlderSameHostState(WorkbenchLogKitState state)
    {
        return MatchesIdentity(state.EngineId, state.SessionId, state.Generation)
            && state.DiagnosticVersion < DiagnosticVersion;
    }

    /// <summary>比较 engine、session 和 generation 三元身份。</summary>
    private bool MatchesIdentity(string engineId, string sessionId, long generation)
    {
        return string.Equals(EngineId, engineId, StringComparison.Ordinal)
            && string.Equals(SessionId, sessionId, StringComparison.Ordinal)
            && Generation == generation;
    }

    /// <summary>切换宿主代并取消所有仍属于旧身份的异步结果。</summary>
    private void ReplaceIdentity(string engineId, string sessionId, long generation)
    {
        var engineChanged = !string.Equals(EngineId, engineId, StringComparison.Ordinal);
        mIdentityCancellation.Cancel();
        mIdentityCancellation.Dispose();
        mIdentityCancellation = CancellationTokenSource.CreateLinkedTokenSource(mLifetimeCancellation.Token);
        CancelFilePreview();
        if (engineChanged)
        {
            if (string.IsNullOrWhiteSpace(EngineId) && mProjectSettingsLoaded && ProjectCanPersist)
            {
                RebindProjectSettingsEngine(engineId);
            }
            else
            {
                ResetProjectSettingsIdentity();
            }
        }

        EngineId = engineId;
        SessionId = sessionId;
        Generation = generation;
        DiagnosticVersion = 0L;
        SettingsVersion = 0L;
    }

    /// <summary>清空断连后的运行状态，但保留用户正在编辑的项目草稿。</summary>
    private void ResetRuntimeState()
    {
        Source = GetString(CommonWaitingForKey, "等待数据");
        StaleReason = string.Empty;
        LoggerName = GetString(LoggerNotInstalledKey, "未安装");
        RuntimeEnabled = false;
        RuntimeMinimumLevel = "--";
        DiagnosticVersion = 0L;
        SettingsVersion = 0L;
        HistoryCount = 0;
        DroppedCount = 0;
        SupportsSettingsApply = false;
        SupportsFilePreview = false;
        SupportsFileWriter = false;
        SupportsPlayerImGui = false;
        SupportsEncryption = false;
        ApplyFileMetadata(null);
        ReconcileHistory(Array.Empty<WorkbenchLogKitHistoryEntry>());
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(RuntimeHistoryText));
    }

    /// <summary>更新来源并通知派生的数据通道。</summary>
    private void SetSource(string value)
    {
        if (SetProperty(ref mSource, value, nameof(Source)))
        {
            OnPropertyChanged(nameof(DataChannelText));
        }
    }

    /// <summary>更新历史数量并通知派生摘要。</summary>
    private void SetHistoryCount(int value)
    {
        if (SetProperty(ref mHistoryCount, value, nameof(HistoryCount)))
        {
            OnPropertyChanged(nameof(RuntimeHistoryText));
        }
    }

    /// <summary>更新影响命令可用性的能力并刷新命令状态。</summary>
    private void SetCapability(ref bool storage, bool value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (!SetProperty(ref storage, value, propertyName))
        {
            return;
        }

        SaveSettingsCommand.RaiseCanExecuteChanged();
        RefreshFileCommand.RaiseCanExecuteChanged();
    }

    /// <summary>按当前语言重新投影 LogKit 的动态展示文本；payload 来源与 Runtime 数据不变。</summary>
    private void OnCultureChanged()
    {
        // 未连接 Runtime 时，占位来源与 logger 名称使用当前语言的等待/未安装文案。
        if (string.IsNullOrWhiteSpace(mEngineId))
        {
            mSource = GetString(CommonWaitingForKey, "等待数据");
            OnPropertyChanged(nameof(Source));
            OnPropertyChanged(nameof(DataChannelText));
            mLoggerName = GetString(LoggerNotInstalledKey, "未安装");
            OnPropertyChanged(nameof(LoggerName));
        }

        OnPropertyChanged(nameof(EncryptionStatusText));
        OnPropertyChanged(nameof(EncryptionMethodText));
        OnPropertyChanged(nameof(EncryptionToggleToolTip));
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(RuntimeHistoryText));
        OnPropertyChanged(nameof(HistoryLevelOptions));
        OnPropertyChanged(nameof(SaveSettingsButtonText));

        // 等待项目配置的初始状态随语言重投影；其余状态文本是操作结果，保持原样。
        if (mIsWaitingProjectConfigStatus)
        {
            SettingsStatusText = GetString(WaitingProjectConfigKey, "等待项目配置");
        }
    }

    /// <summary>等待 Runtime 首帧数据时使用的通用占位文案资源 key。</summary>
    private const string CommonWaitingForKey = "String.Common.WaitingForData";
    /// <summary>Runtime 未声明 logger 时使用的占位文案资源 key。</summary>
    private const string LoggerNotInstalledKey = "String.LogKit.LoggerNotInstalled";
    /// <summary>项目配置尚未加载时使用的状态文本资源 key。</summary>
    private const string WaitingProjectConfigKey = "String.LogKit.WaitingProjectConfig";
    /// <summary>当前设置状态是否处于“等待项目配置”占位（仅该占位随语言重投影）。</summary>
    private bool mIsWaitingProjectConfigStatus = true;

    /// <summary>从当前语言资源读取 LogKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }
}
