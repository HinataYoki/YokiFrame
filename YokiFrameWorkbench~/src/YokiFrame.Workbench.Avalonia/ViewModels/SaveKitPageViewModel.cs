using System.Collections.ObjectModel;
using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.SaveKit;
using YokiFrame.Tooling.Application.Services.SaveKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 SaveKit 项目配置、目录扫描和文件元信息浏览状态。</summary>
public sealed partial class SaveKitPageViewModel : ViewModelBase, IDisposable
{
    private readonly SaveKitWorkbenchSettingsService? mService;
    private readonly IInstallerFolderPicker? mFolderPicker;
    private readonly Func<string, Task>? mOpenDirectoryAsync;
    private readonly Func<string, CancellationToken, Task<string?>>? mResolveRuntimeRootAsync;
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private IReadOnlyList<WorkbenchSaveKitFile> mFilteredFiles = Array.Empty<WorkbenchSaveKitFile>();
    private WorkbenchSaveKitProjectSettings? mBaseline;
    private string mEngineId = string.Empty;
    private string mEngineLabel = GetString(NotConnectedKey, "未连接");
    private string mStoragePath = string.Empty;
    private string mSelectedStorageRoot = STORAGE_ROOT_RUNTIME;
    private string mStorageSubPath = "YokiFrame/Saves";
    private string mFileExtension = ".yoki";
    private string mResolvedStoragePath = string.Empty;
    private string mConfigPath = string.Empty;
    private string mFingerprint = "missing";
    private string mSearchText = string.Empty;
    private string mFilter = FILTER_ALL;
    private string mStatusText = GetString(WaitingProjectConfigKey, "等待项目配置");
    private string mErrorText = string.Empty;
    private bool mDirectoryExists;
    private bool mIsSupported;
    private bool mIsDirty;
    private bool mIsBusy;
    private bool mIsDisposed;
    private int mSlotCount;
    private int mGlobalCount;
    /// <summary>当前状态文本是否处于“等待项目配置”占位；仅占位随语言切换重投影。</summary>
    private bool mIsWaitingProjectConfigStatus = true;
    private bool mUpdatingStorageDraft;

    /// <summary>创建设计时可用的空 SaveKit 页面。</summary>
    public SaveKitPageViewModel() : this(null, null, null)
    {
    }

    /// <summary>创建绑定 Application 服务和目录选择器的 SaveKit 页面。</summary>
    /// <param name="service">项目设置与文件扫描服务。</param>
    /// <param name="folderPicker">宿主目录选择器。</param>
    /// <param name="openDirectoryAsync">打开目录的宿主回调。</param>
    public SaveKitPageViewModel(
        SaveKitWorkbenchSettingsService? service,
        IInstallerFolderPicker? folderPicker,
        Func<string, Task>? openDirectoryAsync = null,
        Func<string, CancellationToken, Task<string?>>? resolveRuntimeRootAsync = null)
    {
        mService = service;
        mFolderPicker = folderPicker;
        mOpenDirectoryAsync = openDirectoryAsync;
        mResolveRuntimeRootAsync = resolveRuntimeRootAsync;
        // 订阅全局语言切换；对应解除订阅在 Dispose，由 WorkbenchWindow 关闭流程统一调用。
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync, CanBrowseFolder);
        OpenDirectoryCommand = new AsyncRelayCommand(OpenDirectoryAsync, CanOpenDirectory);
        ResetCommand = new RelayCommand(Reset, CanReset);
        SelectAllCommand = new RelayCommand(() => Filter = FILTER_ALL);
        SelectSlotCommand = new RelayCommand(() => Filter = "Slot");
        SelectGlobalCommand = new RelayCommand(() => Filter = "Global");
        RefreshStorageRootOptions();
    }

    /// <summary>存档文件元信息集合。</summary>
    public ObservableCollection<WorkbenchSaveKitFile> Files { get; } = new();

    /// <summary>经过搜索和类型筛选的文件集合；变更筛选时重建一次缓存。</summary>
    public IReadOnlyList<WorkbenchSaveKitFile> FilteredFiles => mFilteredFiles;

    /// <summary>保存项目配置命令。</summary>
    public AsyncRelayCommand SaveCommand { get; } = new(static () => Task.CompletedTask);

    /// <summary>重新读取配置和目录命令。</summary>
    public AsyncRelayCommand RefreshCommand { get; } = new(static () => Task.CompletedTask);

    /// <summary>打开原生目录选择器命令。</summary>
    public AsyncRelayCommand BrowseFolderCommand { get; } = new(static () => Task.CompletedTask);

    /// <summary>打开当前配置存档目录命令。</summary>
    public AsyncRelayCommand OpenDirectoryCommand { get; } = new(static () => Task.CompletedTask);

    /// <summary>恢复当前引擎默认值命令。</summary>
    public ICommand ResetCommand { get; } = new RelayCommand(static () => { });

    /// <summary>显示全部文件命令。</summary>
    public ICommand SelectAllCommand { get; } = new RelayCommand(static () => { });

    /// <summary>只显示 Slot 文件命令。</summary>
    public ICommand SelectSlotCommand { get; } = new RelayCommand(static () => { });

    /// <summary>只显示 Global 文件命令。</summary>
    public ICommand SelectGlobalCommand { get; } = new RelayCommand(static () => { });

    /// <summary>当前 engine 标识。</summary>
    public string EngineId
    {
        get => mEngineId;
        private set => SetProperty(ref mEngineId, value);
    }

    /// <summary>当前引擎显示名称。</summary>
    public string EngineLabel
    {
        get => mEngineLabel;
        private set => SetProperty(ref mEngineLabel, value);
    }

    /// <summary>存档目录草稿。</summary>
    public string StoragePath
    {
        get => mStoragePath;
        set
        {
            value ??= string.Empty;
            if (SetProperty(ref mStoragePath, value))
            {
                ResolvedStoragePath = mService?.ResolveStoragePath(value) ?? string.Empty;
                if (!mUpdatingStorageDraft)
                {
                    ProjectStorageDraft(value);
                }
                MarkDirty();
            }
        }
    }

    /// <summary>可选的存档根目录类型；选项由当前 Unity/Godot engine 决定。</summary>
    public ObservableCollection<string> StorageRootOptions { get; } = new();

    /// <summary>当前存档根目录类型：运行时用户目录、项目目录或自定义目录。</summary>
    public string SelectedStorageRoot
    {
        get => GetStorageRootDisplay(mSelectedStorageRoot);
        set
        {
            string normalized = NormalizeStorageRoot(value);
            if (SetProperty(ref mSelectedStorageRoot, normalized))
            {
                ComposeStorageDraft();
            }
        }
    }

    /// <summary>根目录下的相对存档目录，或自定义根目录选项下的绝对目录。</summary>
    public string StorageSubPath
    {
        get => mStorageSubPath;
        set
        {
            if (SetProperty(ref mStorageSubPath, value ?? string.Empty))
            {
                ComposeStorageDraft();
            }
        }
    }

    /// <summary>存档文件扩展名草稿。</summary>
    public string FileExtension
    {
        get => mFileExtension;
        set
        {
            value ??= string.Empty;
            if (SetProperty(ref mFileExtension, value))
            {
                MarkDirty();
            }
        }
    }

    /// <summary>Workbench 可解析的绝对目录；运行时变量路径为空。</summary>
    public string ResolvedStoragePath
    {
        get => mResolvedStoragePath;
        private set => SetProperty(ref mResolvedStoragePath, value);
    }

    /// <summary>实际配置文件路径。</summary>
    public string ConfigPath
    {
        get => mConfigPath;
        private set => SetProperty(ref mConfigPath, value);
    }

    /// <summary>配置文件并发指纹。</summary>
    public string Fingerprint
    {
        get => mFingerprint;
        private set => SetProperty(ref mFingerprint, value);
    }

    /// <summary>文件列表搜索文本。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value))
            {
                RebuildFilteredFiles();
            }
        }
    }

    /// <summary>文件类型筛选，值为哨兵 "all"、Slot 或 Global。</summary>
    public string Filter
    {
        get => mFilter;
        set
        {
            if (SetProperty(ref mFilter, NormalizeFilter(value)))
            {
                RebuildFilteredFiles();
                OnPropertyChanged(nameof(IsAllFilter));
                OnPropertyChanged(nameof(IsSlotFilter));
                OnPropertyChanged(nameof(IsGlobalFilter));
            }
        }
    }

    /// <summary>是否选中全部文件筛选。</summary>
    public bool IsAllFilter => Filter == FILTER_ALL;

    /// <summary>是否选中 Slot 文件筛选。</summary>
    public bool IsSlotFilter => Filter == "Slot";

    /// <summary>是否选中 Global 文件筛选。</summary>
    public bool IsGlobalFilter => Filter == "Global";

    /// <summary>页面状态提示。</summary>
    public string StatusText
    {
        get => mStatusText;
        private set => SetProperty(ref mStatusText, value);
    }

    /// <summary>错误、冲突或不可用提示。</summary>
    public string ErrorText
    {
        get => mErrorText;
        private set => SetProperty(ref mErrorText, value);
    }

    /// <summary>当前是否存在需要用户处理的错误或冲突。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    /// <summary>目录是否已经存在。</summary>
    public bool DirectoryExists
    {
        get => mDirectoryExists;
        private set => SetProperty(ref mDirectoryExists, value);
    }

    /// <summary>当前 engine 是否支持 SaveKit 配置。</summary>
    public bool IsSupported
    {
        get => mIsSupported;
        private set => SetProperty(ref mIsSupported, value);
    }

    /// <summary>草稿是否偏离磁盘配置。</summary>
    public bool IsDirty
    {
        get => mIsDirty;
        private set
        {
            if (SetProperty(ref mIsDirty, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>是否正在执行 IO。</summary>
    public bool IsBusy
    {
        get => mIsBusy;
        private set
        {
            if (SetProperty(ref mIsBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
                BrowseFolderCommand.RaiseCanExecuteChanged();
                OpenDirectoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Slot 文件数量。</summary>
    public int SlotCount => mSlotCount;

    /// <summary>Global 文件数量。</summary>
    public int GlobalCount => mGlobalCount;

    /// <summary>文件总数。</summary>
    public int FileCount => Files.Count;

    /// <summary>页头显示的 Slot / Global 文件摘要。</summary>
    public string FileSummaryText => SlotCount + " Slot · " + GlobalCount + " Global";

    /// <summary>当前是否没有可显示文件。</summary>
    public bool IsEmpty => FilteredFiles.Count == 0;

    /// <summary>应用读取结果并同步文件集合。</summary>
    private void ApplySettings(WorkbenchSaveKitProjectSettings settings, bool replaceDraft)
    {
        mBaseline = settings;
        EngineLabel = settings.EngineLabel;
        IsSupported = settings.IsSupported;
        OpenDirectoryCommand.RaiseCanExecuteChanged();
        ConfigPath = settings.ConfigPath;
        Fingerprint = settings.Fingerprint;
        ResolvedStoragePath = settings.ResolvedStoragePath;
        DirectoryExists = settings.DirectoryExists;
        if (replaceDraft)
        {
            ApplyStoragePath(settings.StoragePath);
            FileExtension = settings.FileExtension;
        }

        Files.Clear();
        mSlotCount = 0;
        mGlobalCount = 0;
        foreach (var file in settings.Files)
        {
            Files.Add(file);
            if (file.Kind == "Slot")
            {
                mSlotCount++;
            }
            else if (file.Kind == "Global")
            {
                mGlobalCount++;
            }
        }

        RebuildFilteredFiles();
        SetStatus(settings.StatusText);
        OnPropertyChanged(nameof(SlotCount));
        OnPropertyChanged(nameof(GlobalCount));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(FileSummaryText));
        UpdateDirty();
    }

    /// <summary>按当前筛选条件重建文件列表缓存，避免绑定反复分配。</summary>
    private void RebuildFilteredFiles()
    {
        if (Files.Count == 0)
        {
            mFilteredFiles = Array.Empty<WorkbenchSaveKitFile>();
        }
        else
        {
            string filter = Filter;
            string search = SearchText?.Trim() ?? string.Empty;
            List<WorkbenchSaveKitFile> filtered = new(Files.Count);
            foreach (WorkbenchSaveKitFile file in Files)
            {
                if (MatchesFilter(file, filter, search))
                {
                    filtered.Add(file);
                }
            }

            mFilteredFiles = filtered;
        }

        OnPropertyChanged(nameof(FilteredFiles));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>根据基线计算 dirty 状态。</summary>
    private void UpdateDirty()
    {
        IsDirty = mBaseline != null
                  && (!string.Equals(StoragePath, mBaseline.StoragePath, StringComparison.Ordinal)
                      || !string.Equals(NormalizeExtensionForCompare(FileExtension), mBaseline.FileExtension, StringComparison.Ordinal));
    }

    /// <summary>字段修改后更新 dirty 状态。</summary>
    private void MarkDirty()
    {
        UpdateDirty();
        SaveCommand.RaiseCanExecuteChanged();
    }

    /// <summary>判断文件是否匹配已经规范化的搜索文本和类型筛选。</summary>
    private static bool MatchesFilter(WorkbenchSaveKitFile file, string filter, string search)
    {
        if (filter != FILTER_ALL && filter != "全部" && file.Kind != filter)
        {
            return false;
        }

        if (search.Length == 0)
        {
            return true;
        }

        return file.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
               || file.FileName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>规范化扩展名以便 dirty 比较。</summary>
    private static string NormalizeExtensionForCompare(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ".yoki"
            : (value.StartsWith(".", StringComparison.Ordinal) ? value : "." + value);
    }

    /// <summary>把界面传入的筛选值归一化为不随语言变化的哨兵或原始类型。</summary>
    /// <param name="value">界面传入的筛选展示值（“全部”/"All"）或具体类型。</param>
    /// <returns>归一化后的内部筛选值。</returns>
    private static string NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FILTER_ALL;
        }

        return value.Trim() switch
        {
            FILTER_ALL => FILTER_ALL,
            "全部" => FILTER_ALL,
            "All" => FILTER_ALL,
            _ => value.Trim()
        };
    }

    /// <summary>按当前语言重新投影占位文本；payload 数据与用户草稿不变。</summary>
    private void OnCultureChanged()
    {
        if (string.IsNullOrWhiteSpace(EngineId))
        {
            mEngineLabel = GetString(NotConnectedKey, "未连接");
            OnPropertyChanged(nameof(EngineLabel));
        }

        // 仅“等待项目配置”占位随语言重投影；其余状态是操作结果，保持原样。
        if (mIsWaitingProjectConfigStatus)
        {
            StatusText = GetString(WaitingProjectConfigKey, "等待项目配置");
        }

        // Runtime 派生文本由分部实现按缓存状态重投影。
        OnRuntimeCultureChanged();
        RefreshStorageRootOptions();
    }

    /// <summary>写入状态文本并维护“等待项目配置”占位标记。</summary>
    /// <param name="text">新的状态文本。</param>
    /// <param name="isWaitingStatus">是否为等待项目配置的占位状态。</param>
    private void SetStatus(string text, bool isWaitingStatus = false)
    {
        mIsWaitingProjectConfigStatus = isWaitingStatus;
        StatusText = text;
    }

    /// <summary>文件类型筛选的“全部”哨兵值；与 Slot/Global 类型值互不冲突。</summary>
    internal const string FILTER_ALL = "all";

    /// <summary>未连接 Runtime 时引擎标签的占位资源 key。</summary>
    private const string NotConnectedKey = "String.SaveKit.NotConnected";

    /// <summary>项目配置尚未加载时状态文本的资源 key。</summary>
    private const string WaitingProjectConfigKey = "String.SaveKit.WaitingProjectConfig";
    internal const string STORAGE_ROOT_RUNTIME = "runtime";
    internal const string STORAGE_ROOT_PROJECT = "project";
    internal const string STORAGE_ROOT_CUSTOM = "custom";

    /// <summary>按当前 engine 生成跨 Unity/Godot 的根目录候选。</summary>
    private void RefreshStorageRootOptions()
    {
        string runtimeLabel = EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase)
            ? GetString("String.SaveKit.GodotUserData", "Godot 用户目录 (OS.GetUserDataDir)")
            : GetString("String.SaveKit.UnityPersistentData", "Unity 持久化目录 (Application.persistentDataPath)");
        string projectLabel = GetString("String.SaveKit.ProjectDirectory", "项目目录");
        string customLabel = GetString("String.SaveKit.CustomDirectory", "自定义绝对路径");
        StorageRootOptions.Clear();
        StorageRootOptions.Add(runtimeLabel);
        StorageRootOptions.Add(projectLabel);
        StorageRootOptions.Add(customLabel);
        OnPropertyChanged(nameof(StorageRootOptions));
        OnPropertyChanged(nameof(SelectedStorageRoot));
    }

    /// <summary>把已保存的完整路径拆成下拉根类型和用户可编辑部分。</summary>
    private void ProjectStorageDraft(string value)
    {
        string runtimeToken = EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase)
            ? "${userDataDir}"
            : "${persistentDataPath}";
        if (value.StartsWith(runtimeToken, StringComparison.Ordinal))
        {
            mSelectedStorageRoot = STORAGE_ROOT_RUNTIME;
            mStorageSubPath = TrimStorageSeparator(value[runtimeToken.Length..]);
        }
        else if (!Path.IsPathRooted(value))
        {
            mSelectedStorageRoot = STORAGE_ROOT_PROJECT;
            mStorageSubPath = TrimStorageSeparator(value);
        }
        else
        {
            mSelectedStorageRoot = STORAGE_ROOT_CUSTOM;
            mStorageSubPath = value;
        }
        OnPropertyChanged(nameof(SelectedStorageRoot));
        OnPropertyChanged(nameof(StorageSubPath));
    }

    /// <summary>应用磁盘配置并同步下拉框草稿。</summary>
    private void ApplyStoragePath(string value)
    {
        mUpdatingStorageDraft = true;
        try
        {
            StoragePath = value;
        }
        finally
        {
            mUpdatingStorageDraft = false;
        }
        ProjectStorageDraft(value);
    }

    /// <summary>按当前根类型重新组合持久化路径。</summary>
    private void ComposeStorageDraft()
    {
        if (mUpdatingStorageDraft || string.IsNullOrWhiteSpace(EngineId)) return;
        string value = mSelectedStorageRoot switch
        {
            STORAGE_ROOT_PROJECT => TrimStorageSeparator(mStorageSubPath),
            STORAGE_ROOT_CUSTOM => mStorageSubPath.Trim(),
            _ => (EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase) ? "${userDataDir}" : "${persistentDataPath}")
                 + "/" + TrimStorageSeparator(mStorageSubPath)
        };
        mUpdatingStorageDraft = true;
        try { StoragePath = value; }
        finally { mUpdatingStorageDraft = false; }
    }

    /// <summary>将下拉框展示文本转换为稳定的内部根类型。</summary>
    private string NormalizeStorageRoot(string? value)
    {
        int index = StorageRootOptions.IndexOf(value ?? string.Empty);
        return index switch
        {
            1 => STORAGE_ROOT_PROJECT,
            2 => STORAGE_ROOT_CUSTOM,
            _ => STORAGE_ROOT_RUNTIME
        };
    }

    /// <summary>获取当前根类型在当前语言下的下拉框展示文本。</summary>
    private string GetStorageRootDisplay(string root)
    {
        int index = root switch
        {
            STORAGE_ROOT_PROJECT => 1,
            STORAGE_ROOT_CUSTOM => 2,
            _ => 0
        };
        return index < StorageRootOptions.Count ? StorageRootOptions[index] : string.Empty;
    }

    /// <summary>规范化根目录拼接所需的分隔符。</summary>
    private static string TrimStorageSeparator(string value)
    {
        return value.Trim().Trim('/', '\\');
    }

    /// <summary>从当前语言资源读取 SaveKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }
}
