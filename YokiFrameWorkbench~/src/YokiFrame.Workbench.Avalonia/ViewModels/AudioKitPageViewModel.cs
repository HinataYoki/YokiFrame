using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 AudioKit Bus、活动 voice、历史与稳定索引的只读观察页面。</summary>
public sealed partial class AudioKitPageViewModel : ViewModelBase, IDisposable
{
    private const string BUS_SCOPE_ALL = "all";
    private const string BUS_SCOPE_BUILT_IN = "built-in";
    private const string BUS_SCOPE_REGISTERED = "registered";
    private const string BUS_SCOPE_DYNAMIC = "dynamic";

    private enum IndexStatusKind
    {
        None,
        Scanning,
        Generating,
        Scanned,
        Generated,
        Saved,
        LoadFailed,
        SaveFailed,
        Error
    }

    private enum IndexEmptyKind
    {
        None,
        ScanHint,
        NoEntries,
        ScanFailed
    }

    private readonly Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? mScanIndexAsync;
    private readonly Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? mGenerateIndexAsync;
    private readonly Func<string, AudioIndexSettings>? mLoadIndexSettings;
    private readonly Func<string, AudioIndexSettings, CancellationToken, Task>? mSaveIndexSettingsAsync;
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private AudioBusChannelViewModel? mSelectedBusChannel;
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private long mGeneration;
    private long mVersion;
    private string mStaleReason = string.Empty;
    private bool mPayloadTruncated;
    private string mBusSearchText = string.Empty;
    private bool mShowActiveBusesOnly;
    private string mSelectedBusScope = BUS_SCOPE_ALL;
    private int mBusTotal;
    private int mLoadedBusCount;
    private string mBusCoverageText = WorkbenchI18nService.Instance.GetString(
        "String.AudioKit.Coverage.Total", "{0} 条总线").Replace("{0}", "0", StringComparison.Ordinal);
    private bool mHasBusCoverageWarning;
    private string mProjectRoot = string.Empty;
    private string mScanFolder = "Assets/Art/Audio";
    private string mIndexOutputPath = "Assets/Scripts/Generated/AudioIds.cs";
    private string mIndexManifestPath = "Assets/Settings/YokiFrame/audio-index.json";
    private string mIndexNamespace = "GameAudio";
    private string mIndexClassName = "AudioIds";
    private decimal mIndexStartId = 1001m;
    private string mIndexStatusText = string.Empty;
    private string mIndexEmptyText = WorkbenchI18nService.Instance.GetString(
        "String.AudioKit.Index.Empty", "点击“扫描预览”查看可索引音频");
    private IndexStatusKind mIndexStatusKind;
    private IndexEmptyKind mIndexEmptyKind = IndexEmptyKind.ScanHint;
    private int mIndexStatusCount;
    private string mIndexStatusError = string.Empty;

    /// <summary>创建可独立预览的 AudioKit 观察页面。</summary>
    public AudioKitPageViewModel() : this(null, null, null, null) { }

    /// <summary>创建带稳定索引用例的 AudioKit 观察页面。</summary>
    internal AudioKitPageViewModel(
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? scanIndexAsync,
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? generateIndexAsync,
        Func<string, AudioIndexSettings>? loadIndexSettings = null,
        Func<string, AudioIndexSettings, CancellationToken, Task>? saveIndexSettingsAsync = null)
    {
        mScanIndexAsync = scanIndexAsync;
        mGenerateIndexAsync = generateIndexAsync;
        mLoadIndexSettings = loadIndexSettings;
        mSaveIndexSettingsAsync = saveIndexSettingsAsync;
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        CreateCommands();
    }

    /// <summary>获取按 Master 与逻辑 Bus 分组的只读观察卡片。</summary>
    public ObservableCollection<AudioBusChannelViewModel> BusChannels { get; } = new();
    /// <summary>获取音频索引扫描预览。</summary>
    public ObservableCollection<AudioIndexEntry> IndexEntries { get; } = new();
    /// <summary>获取读取或解析诊断。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }
    /// <summary>获取任一 payload 列表是否裁剪。</summary>
    public bool PayloadTruncated { get => mPayloadTruncated; private set => SetProperty(ref mPayloadTruncated, value); }
    /// <summary>获取或设置 Bus 名称搜索文本。</summary>
    public string BusSearchText
    {
        get => mBusSearchText;
        set
        {
            if (SetProperty(ref mBusSearchText, value ?? string.Empty)) RefreshBusFilter();
        }
    }

    /// <summary>获取或设置是否只显示包含活动 voice 的普通 Bus。</summary>
    public bool ShowActiveBusesOnly
    {
        get => mShowActiveBusesOnly;
        set
        {
            if (SetProperty(ref mShowActiveBusesOnly, value)) RefreshBusFilter();
        }
    }

    /// <summary>获取可用于大量 Bus 分类筛选的本地化范围选项。</summary>
    public IReadOnlyList<string> BusScopeOptions => new[]
    {
        GetString("String.AudioKit.BusScope.All", "全部"),
        GetString("String.AudioKit.BusScope.BuiltIn", "内置"),
        GetString("String.AudioKit.BusScope.Registered", "已注册"),
        GetString("String.AudioKit.BusScope.Dynamic", "动态")
    };
    /// <summary>获取或设置当前 Bus 来源范围。</summary>
    public string SelectedBusScope
    {
        get => GetBusScopeDisplay(mSelectedBusScope);
        set
        {
            string normalized = NormalizeBusScope(value);
            if (SetProperty(ref mSelectedBusScope, normalized)) RefreshBusFilter();
        }
    }

    /// <summary>获取 Runtime 报告的 Bus 总数。</summary>
    public int BusTotal { get => mBusTotal; private set => SetProperty(ref mBusTotal, value); }
    /// <summary>获取当前 payload 实际加载的 Bus 数量。</summary>
    public int LoadedBusCount { get => mLoadedBusCount; private set => SetProperty(ref mLoadedBusCount, value); }
    /// <summary>获取当前 Bus payload 覆盖率文本。</summary>
    public string BusCoverageText { get => mBusCoverageText; private set => SetProperty(ref mBusCoverageText, value); }
    /// <summary>获取 Bus payload 是否未覆盖 Runtime 全量。</summary>
    public bool HasBusCoverageWarning
    {
        get => mHasBusCoverageWarning;
        private set => SetProperty(ref mHasBusCoverageWarning, value);
    }

    /// <summary>获取或设置项目内扫描目录。</summary>
    public string ScanFolder { get => mScanFolder; set => SetProperty(ref mScanFolder, value); }
    /// <summary>获取或设置生成 C# 路径。</summary>
    public string IndexOutputPath { get => mIndexOutputPath; set => SetProperty(ref mIndexOutputPath, value); }
    /// <summary>获取或设置稳定 ID manifest 路径。</summary>
    public string IndexManifestPath { get => mIndexManifestPath; set => SetProperty(ref mIndexManifestPath, value); }
    /// <summary>获取或设置生成命名空间。</summary>
    public string IndexNamespace { get => mIndexNamespace; set => SetProperty(ref mIndexNamespace, value); }
    /// <summary>获取或设置生成常量类名。</summary>
    public string IndexClassName { get => mIndexClassName; set => SetProperty(ref mIndexClassName, value); }
    /// <summary>获取或设置新 manifest 的起始 ID。</summary>
    public decimal IndexStartId { get => mIndexStartId; set => SetProperty(ref mIndexStartId, value); }
    /// <summary>获取索引扫描或生成结果。</summary>
    public string IndexStatusText { get => mIndexStatusText; private set => SetProperty(ref mIndexStatusText, value); }
    /// <summary>获取索引预览为空时的操作或格式说明。</summary>
    public string IndexEmptyText { get => mIndexEmptyText; private set => SetProperty(ref mIndexEmptyText, value); }

    /// <summary>获取或设置当前选中的只读 Bus 卡片。</summary>
    public AudioBusChannelViewModel? SelectedBusChannel
    {
        get => mSelectedBusChannel;
        set => SetProperty(ref mSelectedBusChannel, value);
    }

    /// <summary>获取索引预览是否为空。</summary>
    public bool IsIndexEmpty => IndexEntries.Count == 0;

    /// <summary>更新索引服务使用的当前项目根。</summary>
    public void SetProjectRoot(string projectRoot)
    {
        if (string.Equals(mProjectRoot, projectRoot, StringComparison.OrdinalIgnoreCase)) return;
        mProjectRoot = projectRoot ?? string.Empty;
        IndexEntries.Clear();
        SetIndexStatus(IndexStatusKind.None);
        SetIndexEmpty(IndexEmptyKind.ScanHint);
        LoadIndexSettings();
        OnPropertyChanged(nameof(IsIndexEmpty));
        RaiseIndexCommands();
    }

    /// <summary>应用低频 dashboard 状态并拒绝同宿主旧版本。</summary>
    public void ApplyPeriodicState(WorkbenchAudioKitState? state)
    {
        if (state == null)
        {
            ResetRuntimeState();
            return;
        }

        if (MatchesIdentity(state) && state.Version < mVersion)
        {
            StaleReason = state.StaleReason;
            return;
        }

        if (MatchesIdentity(state) && state.Version == mVersion)
        {
            StaleReason = state.StaleReason;
            return;
        }

        ApplyState(state);
    }

    /// <summary>取消页面仍在执行的索引任务。</summary>
    public void Dispose()
    {
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        mLifetimeCancellation.Cancel();
        mLifetimeCancellation.Dispose();
    }

    /// <summary>应用完整状态并尽量保持选中 Bus。</summary>
    private void ApplyState(WorkbenchAudioKitState state)
    {
        bool sameIdentity = MatchesIdentity(state);
        string selectedBusKey = sameIdentity ? SelectedBusChannel?.Key ?? "bus:Master" : "bus:Master";
        RebuildBusChannels(state, selectedBusKey);
        mEngineId = state.EngineId;
        mSessionId = state.SessionId;
        mGeneration = state.Generation;
        mVersion = state.Version;
        StaleReason = state.StaleReason;
        PayloadTruncated = state.BusesTruncated || state.VoicesTruncated || state.HistoryTruncated;
        UpdateBusCoverage(state);
    }

    /// <summary>判断状态是否来自当前宿主身份。</summary>
    private bool MatchesIdentity(WorkbenchAudioKitState state)
    {
        return string.Equals(mEngineId, state.EngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, state.SessionId, StringComparison.Ordinal)
            && mGeneration == state.Generation;
    }

    /// <summary>清空已离线 Runtime 的观察状态。</summary>
    private void ResetRuntimeState()
    {
        BusChannels.Clear();
        mAllBusChannels.Clear();
        SelectedBusChannel = null;
        mEngineId = string.Empty;
        mSessionId = string.Empty;
        mGeneration = 0L;
        mVersion = 0L;
        StaleReason = string.Empty;
        PayloadTruncated = false;
        BusTotal = 0;
        LoadedBusCount = 0;
        HasBusCoverageWarning = false;
        UpdateBusCoverageText();
    }

    /// <summary>根据 payload 总数与实际列表更新 Bus 覆盖率和截断警告。</summary>
    private void UpdateBusCoverage(WorkbenchAudioKitState state)
    {
        BusTotal = state.BusTotal;
        LoadedBusCount = state.Buses.Count;
        HasBusCoverageWarning = state.BusesTruncated || state.Buses.Count < state.BusTotal;
        UpdateBusCoverageText();
    }

    /// <summary>根据当前语言重新投影 AudioKit 的动态展示文本。</summary>
    private void OnCultureChanged()
    {
        OnPropertyChanged(nameof(BusScopeOptions));
        OnPropertyChanged(nameof(SelectedBusScope));
        UpdateBusCoverageText();
        SetIndexStatus(mIndexStatusKind, mIndexStatusCount, mIndexStatusError);
        SetIndexEmpty(mIndexEmptyKind);
        for (var index = 0; index < mAllBusChannels.Count; index++)
        {
            mAllBusChannels[index].RefreshLocalization();
        }
        OnPropertyChanged(nameof(BusChannels));
    }

    /// <summary>从当前语言资源读取 AudioKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }

    /// <summary>把筛选展示值归一化为不随语言变化的内部范围键。</summary>
    private static string NormalizeBusScope(string? value)
    {
        if (string.Equals(value, BUS_SCOPE_BUILT_IN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "内置", StringComparison.Ordinal)
            || string.Equals(value, "Built-in", StringComparison.OrdinalIgnoreCase)) return BUS_SCOPE_BUILT_IN;
        if (string.Equals(value, BUS_SCOPE_REGISTERED, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "已注册", StringComparison.Ordinal)
            || string.Equals(value, "Registered", StringComparison.OrdinalIgnoreCase)) return BUS_SCOPE_REGISTERED;
        if (string.Equals(value, BUS_SCOPE_DYNAMIC, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "动态", StringComparison.Ordinal)
            || string.Equals(value, "Dynamic", StringComparison.OrdinalIgnoreCase)) return BUS_SCOPE_DYNAMIC;
        return BUS_SCOPE_ALL;
    }

    /// <summary>根据内部范围键返回当前语言的筛选显示值。</summary>
    private static string GetBusScopeDisplay(string scope)
    {
        return scope switch
        {
            BUS_SCOPE_BUILT_IN => GetString("String.AudioKit.BusScope.BuiltIn", "内置"),
            BUS_SCOPE_REGISTERED => GetString("String.AudioKit.BusScope.Registered", "已注册"),
            BUS_SCOPE_DYNAMIC => GetString("String.AudioKit.BusScope.Dynamic", "动态"),
            _ => GetString("String.AudioKit.BusScope.All", "全部")
        };
    }

    /// <summary>根据 Bus 数量和裁剪状态重建覆盖率文本。</summary>
    private void UpdateBusCoverageText()
    {
        string key = HasBusCoverageWarning
            ? "String.AudioKit.Coverage.Partial"
            : "String.AudioKit.Coverage.Total";
        string fallback = HasBusCoverageWarning
            ? "已加载 {0} / 共 {1} 条总线"
            : "{0} 条总线";
        string template = GetString(key, fallback);
        BusCoverageText = HasBusCoverageWarning
            ? string.Format(template, LoadedBusCount, BusTotal)
            : string.Format(template, BusTotal);
    }

    /// <summary>记录并显示索引操作状态，使语言切换能够重放同一状态。</summary>
    private void SetIndexStatus(IndexStatusKind kind, int count = 0, string? error = null)
    {
        mIndexStatusKind = kind;
        mIndexStatusCount = count;
        mIndexStatusError = error ?? string.Empty;
        string text = kind switch
        {
            IndexStatusKind.Scanning => GetString("String.AudioKit.Index.Scanning", "扫描中"),
            IndexStatusKind.Generating => GetString("String.AudioKit.Index.Generating", "生成中"),
            IndexStatusKind.Scanned => string.Format(GetString("String.AudioKit.Index.Scanned", "已扫描 {0} 项"), count),
            IndexStatusKind.Generated => string.Format(GetString("String.AudioKit.Index.Generated", "已生成 {0} 项"), count),
            IndexStatusKind.Saved => GetString("String.AudioKit.Index.Saved", "配置已保存"),
            IndexStatusKind.LoadFailed => string.Format(
                GetString("String.AudioKit.Index.LoadFailed", "配置读取失败，已使用默认值：{0}"), mIndexStatusError),
            IndexStatusKind.SaveFailed => string.Format(
                GetString("String.AudioKit.Index.SaveFailed", "配置保存失败：{0}"), mIndexStatusError),
            IndexStatusKind.Error => mIndexStatusError,
            _ => string.Empty
        };
        IndexStatusText = text;
    }

    /// <summary>记录并显示索引列表空态，使语言切换能够重放同一空态。</summary>
    private void SetIndexEmpty(IndexEmptyKind kind)
    {
        mIndexEmptyKind = kind;
        IndexEmptyText = kind switch
        {
            IndexEmptyKind.None => string.Empty,
            IndexEmptyKind.NoEntries => GetString(
                "String.AudioKit.Index.NoEntries", "未找到 wav、mp3、ogg、aiff、flac 或 m4a"),
            IndexEmptyKind.ScanFailed => GetString(
                "String.AudioKit.Index.ScanFailed", "扫描失败，请检查路径与上方错误"),
            _ => GetString("String.AudioKit.Index.Empty", "点击“扫描预览”查看可索引音频")
        };
    }
}
