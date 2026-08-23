using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.LocalizationKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 LocalizationKit 文本预览、搜索和缺失诊断页面状态。</summary>
public sealed partial class LocalizationKitPageViewModel : ViewModelBase
{
    private const int PAGE_ENTRY_LIMIT = 1000;
    /// <summary>语言筛选“全部”的不变哨兵值；展示文本由资源投影。</summary>
    internal const string LANGUAGE_ALL = "all";
    private readonly LocalizationKitApplicationService mService;
    private string mProjectRoot;
    private string mSourcePath = "Assets/Settings/YokiFrame/localization.json";
    private string mSearchText = string.Empty;
    private bool mMissingOnly;
    /// <summary>当前状态是否为“等待刷新”初始占位；仅占位随语言切换重投影。</summary>
    private bool mIsWaitingRefreshStatus = true;
    private string mStatusText = GetString(WaitingRefreshKey, "等待刷新");
    private string mProviderText = "Json";
    private string mSelectedLanguage = GetString(LanguageAllKey, "全部");
    private bool mHasAttemptedAutomaticLoad;
    private bool mIsRefreshing;
    private bool mIsCreatingTemplate;
    private int mLoadVersion;
    private int mDisposed;
    private LocalizationCatalog? mCatalog;
    private LocalizationEntryRecord? mSelectedEntry;
    private IReadOnlyList<LocalizationLanguageRecord> mCatalogLanguages = Array.Empty<LocalizationLanguageRecord>();
    /// <summary>当前目录是否处于读取失败状态；空状态据此切换诊断文案。</summary>
    private bool mHasLoadError;

    /// <summary>创建默认 LocalizationKit 页面。</summary>
    public LocalizationKitPageViewModel() : this(Directory.GetCurrentDirectory(), new LocalizationKitApplicationService(), null, null) { }

    /// <summary>创建绑定指定项目根的 LocalizationKit 页面。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="service">用于读取和筛选目录的 Application 服务。</param>
    /// <param name="folderPicker">可选的宿主目录选择器。</param>
    /// <param name="openDirectoryAsync">可选的宿主目录打开回调。</param>
    public LocalizationKitPageViewModel(
        string projectRoot,
        LocalizationKitApplicationService service,
        IInstallerFolderPicker? folderPicker = null,
        Func<string, Task>? openDirectoryAsync = null)
    {
        mProjectRoot = NormalizeProjectRoot(projectRoot);
        mService = service ?? throw new ArgumentNullException(nameof(service));
        mSettingsService = new LocalizationKitSettingsService();
        mFolderPicker = folderPicker;
        mOpenDirectoryAsync = openDirectoryAsync;
        LoadLubanWorkspaceSettings();
        // 订阅全局语言切换；对应解除订阅在 Dispose，由 WorkbenchWindow 关闭流程统一调用。
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateTemplateCommand = new AsyncRelayCommand(CreateLubanTemplateAsync);
        BrowseLubanWorkDirCommand = new AsyncRelayCommand(BrowseLubanWorkDirAsync);
        OpenExcelDirectoryCommand = new AsyncRelayCommand(OpenExcelDirectoryAsync);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    /// <summary>当前源文件路径；变更后等待用户或页面激活显式刷新。</summary>
    public string SourcePath
    {
        get => mSourcePath;
        set
        {
            if (SetProperty(ref mSourcePath, value ?? string.Empty))
            {
                InvalidateCatalog(GetString(SourceChangedKey, "源文件已变更，点击刷新"));
            }
        }
    }

    /// <summary>关键字筛选。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasActiveFilters));
                ApplyFilters();
            }
        }
    }

    /// <summary>是否只显示缺失条目。</summary>
    public bool MissingOnly
    {
        get => mMissingOnly;
        set
        {
            if (SetProperty(ref mMissingOnly, value))
            {
                OnPropertyChanged(nameof(HasActiveFilters));
                ApplyFilters();
            }
        }
    }

    /// <summary>当前 Provider 文本。</summary>
    public string ProviderText { get => mProviderText; private set => SetProperty(ref mProviderText, value); }

    /// <summary>当前页面状态。</summary>
    public string StatusText
    {
        get => mStatusText;
        private set
        {
            if (SetProperty(ref mStatusText, value))
            {
                OnPropertyChanged(nameof(HasLoadError));
                OnPropertyChanged(nameof(EmptyTitleText));
                OnPropertyChanged(nameof(EmptyHintText));
            }
        }
    }

    /// <summary>语言筛选选项。</summary>
    public ObservableCollection<string> LanguageOptions { get; } = new() { GetString(LanguageAllKey, "全部") };

    /// <summary>当前选中的语言。</summary>
    public string SelectedLanguage
    {
        get => mSelectedLanguage;
        set
        {
            if (SetProperty(ref mSelectedLanguage, NormalizeLanguageFilter(value)))
            {
                OnPropertyChanged(nameof(HasActiveFilters));
                ApplyFilters();
            }
        }
    }

    /// <summary>可见本地化条目。</summary>
    public ObservableCollection<LocalizationEntryRecord> Entries { get; } = new();

    /// <summary>当前选中条目。</summary>
    public LocalizationEntryRecord? SelectedEntry
    {
        get => mSelectedEntry;
        set
        {
            if (SetProperty(ref mSelectedEntry, value))
            {
                RebuildSelectedValueRows();
                OnPropertyChanged(nameof(SelectedEntryMissingText));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasNoSelection));
            }
        }
    }

    /// <summary>选中条目的语言对照行。</summary>
    public ObservableCollection<LocalizationPreviewValueViewModel> SelectedValueRows { get; } = new();

    /// <summary>当前目录按语言统计的覆盖情况。</summary>
    public ObservableCollection<LocalizationLanguageCoverageViewModel> LanguageCoverage { get; } = new();

    /// <summary>选中条目的缺失语言摘要。</summary>
    public string SelectedEntryMissingText => SelectedEntry is null
        ? GetString(NoEntrySelectedKey, "未选择条目")
        : SelectedEntry.HasMissing
            ? string.Format(GetString(MissingTemplateKey, "缺失 {0}"), string.Join("、", SelectedEntry.MissingLanguages))
            : GetString(AllLanguagesCompleteKey, "语言配置完整");

    /// <summary>当前是否存在选中条目。</summary>
    public bool HasSelection => SelectedEntry is not null;

    /// <summary>当前是否没有选中条目。</summary>
    public bool HasNoSelection => !HasSelection;

    /// <summary>当前目录是否读取失败，空状态需要显示诊断提示。</summary>
    public bool HasLoadError => mHasLoadError;

    /// <summary>当前是否存在搜索、语言或缺失筛选条件。</summary>
    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(SearchText)
        || MissingOnly
        || NormalizeLanguageFilter(SelectedLanguage) != LANGUAGE_ALL;

    /// <summary>条目列表空状态标题。</summary>
    public string EmptyTitleText => HasLoadError
        ? GetString(LoadErrorTitleKey, "无法读取本地化目录")
        : GetString(EmptyTitleKey, "没有匹配条目");

    /// <summary>条目列表空状态说明。</summary>
    public string EmptyHintText => HasLoadError
        ? GetString(LoadErrorHintKey, "检查 Luban schema 或 standalone JSON 后点击刷新")
        : GetString(EmptyHintKey, "调整搜索、语言或缺失筛选");

    /// <summary>当前筛选结果是否为空。</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>当前语言数量。</summary>
    public int LanguageCount { get; private set; }

    /// <summary>目录条目数量。</summary>
    public int EntryCount { get; private set; }

    /// <summary>缺失条目数量。</summary>
    public int MissingEntryCount { get; private set; }

    /// <summary>页面顶部统计文本。</summary>
    public string SummaryText => string.Format(
        GetString(SummaryTemplateKey, "语言 {0} · 条目 {1} · 缺失 {2}"),
        LanguageCount, EntryCount, MissingEntryCount);

    /// <summary>刷新目录命令。</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>创建 Luban XML schema 与 Excel 作者模板命令。</summary>
    public AsyncRelayCommand CreateTemplateCommand { get; }

    /// <summary>清除搜索、语言和缺失筛选命令。</summary>
    public RelayCommand ClearFiltersCommand { get; }

    /// <summary>更新页面使用的项目根；根目录变化时丢弃旧项目的内存目录。</summary>
    public void SetProjectRoot(string projectRoot)
    {
        string normalizedRoot = NormalizeProjectRoot(projectRoot);
        if (string.Equals(mProjectRoot, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mProjectRoot = normalizedRoot;
        mHasAttemptedAutomaticLoad = false;
        LoadLubanWorkspaceSettings();
        InvalidateCatalog(GetString(ProjectSwitchedKey, "项目已切换，等待刷新"));
    }

    /// <summary>在页面首次激活时按需读取目录；同一项目失败后保持诊断状态，等待用户显式刷新。</summary>
    /// <returns>首次加载完成任务。</returns>
    public Task EnsureLoadedAsync()
    {
        if (Volatile.Read(ref mDisposed) != 0)
        {
            return Task.CompletedTask;
        }

        if (mCatalog is not null || mHasAttemptedAutomaticLoad)
        {
            return Task.CompletedTask;
        }

        mHasAttemptedAutomaticLoad = true;
        return RefreshAsync();
    }

    /// <summary>异步加载已注册的 Luban schema 或 JSON standalone 目录，并忽略迟到结果。</summary>
    /// <returns>刷新完成任务。</returns>
    public async Task RefreshAsync()
    {
        if (Volatile.Read(ref mDisposed) != 0)
        {
            return;
        }

        mHasAttemptedAutomaticLoad = true;
        if (mIsRefreshing)
        {
            return;
        }

        mIsRefreshing = true;
        int loadVersion = ++mLoadVersion;
        string projectRoot = mProjectRoot;
        string sourcePath = SourcePath;
        string lubanWorkDir = LubanWorkDir;
        if (!TryPersistLubanWorkspaceSettings())
        {
            mIsRefreshing = false;
            return;
        }

        SetStatus(GetString(LoadingKey, "正在加载本地化目录"));
        try
        {
            LocalizationOperationResult result = await Task.Run(() => mService.LoadPreferredAsync(projectRoot, sourcePath, lubanWorkDir));
            if (Volatile.Read(ref mDisposed) != 0 || loadVersion != mLoadVersion)
            {
                return;
            }

            if (!result.Succeeded || result.Catalog is null)
            {
                ApplyLoadFailure(result);
                return;
            }

            ApplyCatalog(result.Catalog);
        }
        finally
        {
            if (Volatile.Read(ref mDisposed) == 0 && loadVersion == mLoadVersion)
            {
                mIsRefreshing = false;
            }
        }
    }

    /// <summary>
    /// 使关闭窗口前已经启动的本地化目录读取结果失效，阻止后台任务继续修改页面状态。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref mDisposed, 1) != 0)
        {
            return;
        }

        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        Interlocked.Increment(ref mLoadVersion);
        mIsRefreshing = false;
    }

    /// <summary>按当前语言重新投影占位、筛选选项和派生文案；目录数据不变。</summary>
    private void OnCultureChanged()
    {
        // 语言下拉的“全部”展示值随语言重建，选中哨兵保持不变。
        RebuildLanguageOptionsWithAllLabel();
        if (mIsWaitingRefreshStatus)
        {
            StatusText = GetString(WaitingRefreshKey, "等待刷新");
        }

        OnPropertyChanged(nameof(SelectedEntryMissingText));
        OnPropertyChanged(nameof(EmptyTitleText));
        OnPropertyChanged(nameof(EmptyHintText));
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>把语言下拉第一项替换为当前语言的“全部”展示文本。</summary>
    private void RebuildLanguageOptionsWithAllLabel()
    {
        string allLabel = GetString(LanguageAllKey, "全部");
        if (LanguageOptions.Count > 0 && !string.Equals(LanguageOptions[0], allLabel, StringComparison.Ordinal))
        {
            LanguageOptions[0] = allLabel;
        }
    }

    /// <summary>一次性清除筛选条件后仅重投影内存目录，避免每个字段分别触发筛选。</summary>
    private void ClearFilters()
    {
        bool changed = !string.IsNullOrWhiteSpace(mSearchText)
            || mMissingOnly
            || NormalizeLanguageFilter(mSelectedLanguage) != LANGUAGE_ALL;
        if (!changed)
        {
            return;
        }

        mSearchText = string.Empty;
        mMissingOnly = false;
        mSelectedLanguage = GetString(LanguageAllKey, "全部");
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(MissingOnly));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(HasActiveFilters));
        ApplyFilters();
    }

    /// <summary>保存刚加载的目录、语言选项、覆盖率和统计，再投影当前筛选结果。</summary>
    private void ApplyCatalog(LocalizationCatalog catalog)
    {
        mCatalog = catalog;
        mCatalogLanguages = catalog.Languages;
        ProviderText = catalog.Provider + " · " + Path.GetFileName(catalog.SourcePath);
        UpdateLanguageOptions(catalog.Languages);
        RebuildLanguageCoverage(catalog);
        SetStatistics(catalog.Languages.Count, catalog.Entries.Count, catalog.MissingEntryCount);
        ApplyFilters();
    }

    /// <summary>把加载失败状态投影到页面，并清除已失效的旧项目数据。</summary>
    private void ApplyLoadFailure(LocalizationOperationResult result)
    {
        mCatalog = null;
        mHasLoadError = true;
        ClearCatalogProjection();
        ProviderText = string.IsNullOrWhiteSpace(result.Provider) ? GetString(UnknownProviderKey, "未知") : result.Provider;
        SetStatistics(0, 0, 0);
        SetStatus(string.Format(GetString(LoadFailedStatusTemplateKey, "失败: {0}"), string.Join("; ", result.Diagnostics)));
    }

    /// <summary>基于缓存目录应用搜索、缺失和语言筛选，不执行文件 IO。</summary>
    private void ApplyFilters()
    {
        if (mCatalog is null)
        {
            return;
        }

        int? selectedId = SelectedEntry?.Id;
        IReadOnlyList<LocalizationEntryRecord> filteredEntries = mService.Filter(
            mCatalog,
            SearchText,
            MissingOnly,
            PAGE_ENTRY_LIMIT);
        Entries.Clear();
        foreach (LocalizationEntryRecord entry in filteredEntries)
        {
            if (MatchesLanguage(entry))
            {
                Entries.Add(entry);
            }
        }

        SelectedEntry = selectedId.HasValue
            ? Entries.FirstOrDefault(entry => entry.Id == selectedId.Value) ?? Entries.FirstOrDefault()
            : Entries.FirstOrDefault();
        mHasLoadError = false;
        SetStatus(string.Format(
            GetString(LoadedEntriesTemplateKey, "已加载 {0} / {1} 条"), Entries.Count, EntryCount));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>重建目录级语言覆盖统计，并与缺失判定复用同一值存在规则。</summary>
    private void RebuildLanguageCoverage(LocalizationCatalog catalog)
    {
        LanguageCoverage.Clear();
        foreach (LocalizationLanguageRecord language in catalog.Languages)
        {
            int presentCount = 0;
            foreach (LocalizationEntryRecord entry in catalog.Entries)
            {
                if (entry.HasValueFor(language.Id))
                {
                    presentCount++;
                }
            }

            LanguageCoverage.Add(new LocalizationLanguageCoverageViewModel(
                language.Id,
                presentCount,
                catalog.Entries.Count - presentCount));
        }
    }

    /// <summary>同步语言选项并恢复仍然有效的筛选值。</summary>
    private void UpdateLanguageOptions(IReadOnlyList<LocalizationLanguageRecord> languages)
    {
        // 语言筛选使用展示值存储；“全部”哨兵在 Normalize 中与各语言标签互转。
        if (NormalizeLanguageFilter(mSelectedLanguage) == LANGUAGE_ALL)
        {
            mSelectedLanguage = GetString(LanguageAllKey, "全部");
        }

        string selectedLanguage = mSelectedLanguage;
        string[] nextOptions = new[] { GetString(LanguageAllKey, "全部") }.Concat(languages.Select(static language => language.Id)).ToArray();
        if (!LanguageOptions.SequenceEqual(nextOptions))
        {
            LanguageOptions.Clear();
            foreach (string option in nextOptions)
            {
                LanguageOptions.Add(option);
            }
        }

        string normalizedLanguage = nextOptions.Contains(selectedLanguage, StringComparer.Ordinal) ? selectedLanguage : "全部";
        if (!string.Equals(mSelectedLanguage, normalizedLanguage, StringComparison.Ordinal))
        {
            mSelectedLanguage = normalizedLanguage;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(HasActiveFilters));
        }
    }

    /// <summary>按当前语言筛选存在可显示文本的条目。</summary>
    private bool MatchesLanguage(LocalizationEntryRecord entry)
    {
        return NormalizeLanguageFilter(SelectedLanguage) == LANGUAGE_ALL
            || entry.HasValueFor(SelectedLanguage);
    }

    /// <summary>按目录语言顺序重建当前条目的对照预览。</summary>
    private void RebuildSelectedValueRows()
    {
        SelectedValueRows.Clear();
        if (SelectedEntry is null)
        {
            return;
        }

        foreach (LocalizationLanguageRecord language in mCatalogLanguages)
        {
            bool hasText = SelectedEntry.Values.TryGetValue(language.Id, out string? value)
                && !string.IsNullOrWhiteSpace(value);
            string pluralValue = string.Empty;
            if (SelectedEntry.PluralValues.TryGetValue(language.Id, out IReadOnlyDictionary<string, string>? plural))
            {
                pluralValue = string.Join(" · ", plural.Select(pair => pair.Key + " = " + pair.Value));
            }

            SelectedValueRows.Add(new LocalizationPreviewValueViewModel(
                language.Id,
                hasText ? value! : GetString(NotConfiguredKey, "未配置"),
                pluralValue,
                !SelectedEntry.HasValueFor(language.Id)));
        }
    }

    /// <summary>清理旧目录投影，使不同项目的条目和覆盖率不会交叉显示。</summary>
    private void ClearCatalogProjection()
    {
        mCatalogLanguages = Array.Empty<LocalizationLanguageRecord>();
        Entries.Clear();
        LanguageCoverage.Clear();
        SelectedEntry = null;
        string allLabel = GetString(LanguageAllKey, "全部");
        if (LanguageOptions.Count != 1 || LanguageOptions[0] != allLabel)
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(allLabel);
        }

        if (NormalizeLanguageFilter(mSelectedLanguage) != LANGUAGE_ALL)
        {
            mSelectedLanguage = allLabel;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>使缓存失效并提升加载版本，防止旧文件读取结果覆盖新项目状态。</summary>
    private void InvalidateCatalog(string statusText)
    {
        mCatalog = null;
        mIsRefreshing = false;
        mLoadVersion++;
        ClearCatalogProjection();
        ProviderText = "Json";
        SetStatistics(0, 0, 0);
        SetStatus(statusText);
    }

    /// <summary>更新顶部统计字段并通知依赖的摘要绑定。</summary>
    private void SetStatistics(int languageCount, int entryCount, int missingEntryCount)
    {
        LanguageCount = languageCount;
        EntryCount = entryCount;
        MissingEntryCount = missingEntryCount;
        OnPropertyChanged(nameof(LanguageCount));
        OnPropertyChanged(nameof(EntryCount));
        OnPropertyChanged(nameof(MissingEntryCount));
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>规范化可用项目根；空值回落当前进程目录。</summary>
    private static string NormalizeProjectRoot(string projectRoot)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot);
    }

    /// <summary>把界面传入的语言筛选值归一化；“全部”/"All"统一映射到不变哨兵，其余原样保留。</summary>
    /// <param name="value">界面传入的语言展示值。</param>
    /// <returns>归一化后的内部筛选值。</returns>
    private static string NormalizeLanguageFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LANGUAGE_ALL;
        }

        return value.Trim() switch
        {
            LANGUAGE_ALL => LANGUAGE_ALL,
            "全部" => LANGUAGE_ALL,
            "All" => LANGUAGE_ALL,
            _ => value.Trim()
        };
    }

    /// <summary>写入状态文本并维护“等待刷新”占位标记。</summary>
    /// <param name="text">新的状态文本。</param>
    private void SetStatus(string text)
    {
        mIsWaitingRefreshStatus = false;
        StatusText = text;
    }

    /// <summary>等待首次刷新占位资源 key。</summary>
    private const string WaitingRefreshKey = "String.LocalizationKit.WaitingRefresh";

    /// <summary>语言筛选“全部”展示文本资源 key。</summary>
    private const string LanguageAllKey = "String.LocalizationKit.LanguageAll";

    /// <summary>未选择条目占位资源 key。</summary>
    private const string NoEntrySelectedKey = "String.LocalizationKit.NoEntrySelected";

    /// <summary>缺失摘要模板资源 key。</summary>
    private const string MissingTemplateKey = "String.LocalizationKit.MissingTemplate";

    /// <summary>语言配置完整提示资源 key。</summary>
    private const string AllLanguagesCompleteKey = "String.LocalizationKit.AllLanguagesComplete";

    /// <summary>目录读取失败标题资源 key。</summary>
    private const string LoadErrorTitleKey = "String.LocalizationKit.LoadErrorTitle";

    /// <summary>目录读取失败说明资源 key。</summary>
    private const string LoadErrorHintKey = "String.LocalizationKit.LoadErrorHint";

    /// <summary>无匹配条目标题资源 key。</summary>
    private const string EmptyTitleKey = "String.LocalizationKit.EmptyTitle";

    /// <summary>无匹配条目说明资源 key。</summary>
    private const string EmptyHintKey = "String.LocalizationKit.EmptyHint";

    /// <summary>顶部统计模板资源 key。</summary>
    private const string SummaryTemplateKey = "String.LocalizationKit.SummaryTemplate";

    /// <summary>源文件变更提示资源 key。</summary>
    private const string SourceChangedKey = "String.LocalizationKit.SourceChanged";

    /// <summary>项目切换提示资源 key。</summary>
    private const string ProjectSwitchedKey = "String.LocalizationKit.ProjectSwitched";

    /// <summary>正在加载提示资源 key。</summary>
    private const string LoadingKey = "String.LocalizationKit.Loading";

    /// <summary>加载完成模板资源 key。</summary>
    private const string LoadedEntriesTemplateKey = "String.LocalizationKit.LoadedEntriesTemplate";

    /// <summary>加载失败状态模板资源 key。</summary>
    private const string LoadFailedStatusTemplateKey = "String.LocalizationKit.LoadFailedStatusTemplate";

    /// <summary>未知 Provider 占位资源 key。</summary>
    private const string UnknownProviderKey = "String.LocalizationKit.UnknownProvider";

    /// <summary>未配置占位资源 key。</summary>
    private const string NotConfiguredKey = "String.LocalizationKit.NotConfigured";

    /// <summary>从当前语言资源读取 LocalizationKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }
}
