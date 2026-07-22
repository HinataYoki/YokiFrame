using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.LocalizationKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 LocalizationKit 文本预览、搜索和缺失诊断页面状态。</summary>
public sealed partial class LocalizationKitPageViewModel : ViewModelBase
{
    private const int PAGE_ENTRY_LIMIT = 1000;
    private readonly LocalizationKitApplicationService mService;
    private string mProjectRoot;
    private string mSourcePath = "Assets/Settings/YokiFrame/localization.json";
    private string mSearchText = string.Empty;
    private bool mMissingOnly;
    private string mStatusText = "等待刷新";
    private string mProviderText = "Json";
    private string mSelectedLanguage = "全部";
    private bool mHasAttemptedAutomaticLoad;
    private bool mIsRefreshing;
    private bool mIsCreatingTemplate;
    private int mLoadVersion;
    private LocalizationCatalog? mCatalog;
    private LocalizationEntryRecord? mSelectedEntry;
    private IReadOnlyList<LocalizationLanguageRecord> mCatalogLanguages = Array.Empty<LocalizationLanguageRecord>();

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
                InvalidateCatalog("源文件已变更，点击刷新");
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
    public ObservableCollection<string> LanguageOptions { get; } = new() { "全部" };

    /// <summary>当前选中的语言。</summary>
    public string SelectedLanguage
    {
        get => mSelectedLanguage;
        set
        {
            if (SetProperty(ref mSelectedLanguage, value ?? "全部"))
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
        ? "未选择条目"
        : SelectedEntry.HasMissing
            ? "缺失 " + string.Join("、", SelectedEntry.MissingLanguages)
            : "语言配置完整";

    /// <summary>当前是否存在选中条目。</summary>
    public bool HasSelection => SelectedEntry is not null;

    /// <summary>当前是否没有选中条目。</summary>
    public bool HasNoSelection => !HasSelection;

    /// <summary>当前目录是否读取失败，空状态需要显示诊断提示。</summary>
    public bool HasLoadError => StatusText.StartsWith("失败:", StringComparison.Ordinal);

    /// <summary>当前是否存在搜索、语言或缺失筛选条件。</summary>
    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(SearchText)
        || MissingOnly
        || !string.Equals(SelectedLanguage, "全部", StringComparison.Ordinal);

    /// <summary>条目列表空状态标题。</summary>
    public string EmptyTitleText => HasLoadError ? "无法读取本地化目录" : "没有匹配条目";

    /// <summary>条目列表空状态说明。</summary>
    public string EmptyHintText => HasLoadError
        ? "检查 Luban schema 或 standalone JSON 后点击刷新"
        : "调整搜索、语言或缺失筛选";

    /// <summary>当前筛选结果是否为空。</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>当前语言数量。</summary>
    public int LanguageCount { get; private set; }

    /// <summary>目录条目数量。</summary>
    public int EntryCount { get; private set; }

    /// <summary>缺失条目数量。</summary>
    public int MissingEntryCount { get; private set; }

    /// <summary>页面顶部统计文本。</summary>
    public string SummaryText => "语言 " + LanguageCount + " · 条目 " + EntryCount + " · 缺失 " + MissingEntryCount;

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
        InvalidateCatalog("项目已切换，等待刷新");
    }

    /// <summary>在页面首次激活时按需读取目录；同一项目失败后保持诊断状态，等待用户显式刷新。</summary>
    /// <returns>首次加载完成任务。</returns>
    public Task EnsureLoadedAsync()
    {
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

        StatusText = "正在加载本地化目录";
        try
        {
            LocalizationOperationResult result = await Task.Run(() => mService.LoadPreferredAsync(projectRoot, sourcePath, lubanWorkDir));
            if (loadVersion != mLoadVersion)
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
            if (loadVersion == mLoadVersion)
            {
                mIsRefreshing = false;
            }
        }
    }

    /// <summary>一次性清除筛选条件后仅重投影内存目录，避免每个字段分别触发筛选。</summary>
    private void ClearFilters()
    {
        bool changed = !string.IsNullOrWhiteSpace(mSearchText)
            || mMissingOnly
            || !string.Equals(mSelectedLanguage, "全部", StringComparison.Ordinal);
        if (!changed)
        {
            return;
        }

        mSearchText = string.Empty;
        mMissingOnly = false;
        mSelectedLanguage = "全部";
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
        ClearCatalogProjection();
        ProviderText = string.IsNullOrWhiteSpace(result.Provider) ? "未知" : result.Provider;
        SetStatistics(0, 0, 0);
        StatusText = "失败: " + string.Join("; ", result.Diagnostics);
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
        StatusText = "已加载 " + Entries.Count + " / " + EntryCount + " 条";
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
        string selectedLanguage = mSelectedLanguage;
        string[] nextOptions = new[] { "全部" }.Concat(languages.Select(static language => language.Id)).ToArray();
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
        return string.Equals(SelectedLanguage, "全部", StringComparison.OrdinalIgnoreCase)
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
                hasText ? value! : "未配置",
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
        if (LanguageOptions.Count != 1 || LanguageOptions[0] != "全部")
        {
            LanguageOptions.Clear();
            LanguageOptions.Add("全部");
        }

        if (!string.Equals(mSelectedLanguage, "全部", StringComparison.Ordinal))
        {
            mSelectedLanguage = "全部";
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
        StatusText = statusText;
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
}
