using System.Collections.ObjectModel;
using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 TableKit Luban 配置、控制台、验证预览和生成操作。</summary>
public sealed partial class TableKitPageViewModel : ViewModelBase
{
    private readonly string mProjectRoot;
    private readonly TableKitApplicationService mService;
    private readonly TableKitSettingsService mSettingsService = new();
    private readonly TableKitResourceLocationResolver mResourceLocationResolver = new();
    private readonly Func<string, Task>? mCopyTextAsync;
    private readonly IInstallerFolderPicker? mFolderPicker;
    private readonly ITableKitLubanFilePicker? mLubanFilePicker;
    private readonly TableKitOptions mDefaultOptions;
    private string mConfigPath = string.Empty;
    private string mLubanExecutablePath = string.Empty;
    private string mLubanWorkDir = string.Empty;
    private string mTargetName = string.Empty;
    private string mCodeTarget = string.Empty;
    private string mDataTarget = string.Empty;
    private string mOutputCodeDir = string.Empty;
    private string mOutputDataDir = string.Empty;
    private bool mIsAddressable;
    private string mRuntimePathPattern = string.Empty;
    private bool mRuntimePathPatternIsCustom;
    private bool mCustomEditorDataPath;
    private string mEditorDataPath = string.Empty;
    private bool mUseAsyncLoading;
    private bool mUseRawResourceLoading;
    private bool mGenerateExternalTypeUtil;
    private bool mUseAssemblyDefinition;
    private string mAssemblyName = string.Empty;
    private string mStatusText = "等待验证";
    private string mStatusDetailText = "检查 Luban 环境后即可开始生成。";
    private string mLubanStatusText = "Luban OFF";
    private string mEnvironmentMessage = "尚未检查当前项目的 Luban 工具路径。";
    private string mCommandPreviewText = "尚未构建 Luban 命令。";
    private string mTablesType = "未解析";
    private string mDataExtension = "未解析";
    private string mPreviewDirectory = string.Empty;
    private string mPreviewSearch = string.Empty;
    private TableKitPreviewTableViewModel? mSelectedPreviewTable;
    private TableKitPreviewRecordViewModel? mSelectedPreviewRecord;
    private int mSelectedWorkspaceIndex;
    private bool mIsConsoleExpanded;
    private bool mLubanAvailable;
    private string mPackageName = "com.code-philosophy.luban";
    private string mAsmdefName = "Luban.Runtime";
    private string mLubanTypeName = "Luban.ByteBuf";
    private string mLoaderSummary = string.Empty;

    /// <summary>创建绑定当前工作目录的 TableKit 页面。</summary>
    public TableKitPageViewModel() : this(Directory.GetCurrentDirectory(), new TableKitApplicationService(), null, null, null) { }

    /// <summary>创建绑定指定项目根的 TableKit 页面。</summary>
    /// <param name="projectRoot">当前宿主项目根。</param>
    /// <param name="service">TableKit Application 用例。</param>
    /// <param name="copyTextAsync">可选的系统剪贴板回调。</param>
    /// <param name="folderPicker">可选的宿主目录选择器。</param>
    /// <param name="lubanFilePicker">可选的 Luban.dll 文件选择器。</param>
    public TableKitPageViewModel(
        string projectRoot,
        TableKitApplicationService service,
        Func<string, Task>? copyTextAsync = null,
        IInstallerFolderPicker? folderPicker = null,
        ITableKitLubanFilePicker? lubanFilePicker = null)
    {
        mProjectRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot);
        mService = service ?? throw new ArgumentNullException(nameof(service));
        mCopyTextAsync = copyTextAsync;
        mFolderPicker = folderPicker;
        mLubanFilePicker = lubanFilePicker;
        mDefaultOptions = CreateDefaultOptions();
        TargetOptions = new ObservableCollection<string>(new[] { "client", "server", "all" });
        CodeTargetOptions = new ObservableCollection<string>(new[] { "cs-bin", "cs-simple-json", "cs-dotnet-json", "cs-newtonsoft-json" });
        ExtraCodeTargetOptions = new ObservableCollection<string>(new[]
        {
            "cs-bin", "cs-simple-json", "cs-newtonsoft-json", "cs-dotnet-json", "java-bin", "java-json",
            "go-bin", "go-json", "python-json", "cpp-bin", "rust-bin", "rust-json", "lua-lua", "lua-bin",
            "typescript-bin", "typescript-json"
        });
        DataTargetOptions = new ObservableCollection<string>(new[] { "bin", "bin-offset", "json", "json2", "lua", "xml", "yaml", "bson", "msgpack", "protobuf2-bin", "protobuf3-bin", "protobuf2-json", "protobuf3-json" });
        ConsoleEntries = new ObservableCollection<TableKitConsoleEntryViewModel>();
        ConsoleEntries.CollectionChanged += OnConsoleEntriesChanged;
        ExtraOutputTargets = new ObservableCollection<TableKitExtraOutputViewModel>();
        PreviewTables = new ObservableCollection<TableKitPreviewTableViewModel>();
        PreviewTables.CollectionChanged += OnPreviewTablesChanged;
        ApplyOptions(mSettingsService.Load(mProjectRoot, mDefaultOptions));
        ValidateCommand = new AsyncRelayCommand(ValidateAsync);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        RefreshConfigCommand = new RelayCommand(RefreshConfiguration);
        SaveCommand = new RelayCommand(SaveConfiguration);
        ResetCommand = new RelayCommand(ResetConfiguration);
        AddExtraOutputCommand = new RelayCommand(AddExtraOutput);
        CopyConsoleCommand = new AsyncRelayCommand(CopyConsoleAsync);
        ClearConsoleCommand = new RelayCommand(ClearConsole);
        BrowseLubanWorkDirCommand = new AsyncRelayCommand(BrowseLubanWorkDirAsync);
        BrowseLubanExecutableCommand = new AsyncRelayCommand(BrowseLubanExecutableAsync);
        BrowseOutputDataCommand = new AsyncRelayCommand(BrowseOutputDataAsync);
        BrowseOutputCodeCommand = new AsyncRelayCommand(BrowseOutputCodeAsync);
        BrowseEditorDataCommand = new AsyncRelayCommand(BrowseEditorDataAsync);
        OpenConfigDirectoryCommand = new AsyncRelayCommand(OpenConfigDirectoryAsync);
        RefreshEnvironment();
    }

    /// <summary>可选的 Luban target 名称集合；配置刷新后会追加用户 target。</summary>
    public ObservableCollection<string> TargetOptions { get; }
    /// <summary>常见 C# code target 集合，同时允许 ComboBox 输入自定义值。</summary>
    public ObservableCollection<string> CodeTargetOptions { get; }
    /// <summary>额外输出可使用的跨语言 code target 集合。</summary>
    public ObservableCollection<string> ExtraCodeTargetOptions { get; }
    /// <summary>常见 Luban data target 集合，同时允许 ComboBox 输入自定义值。</summary>
    public ObservableCollection<string> DataTargetOptions { get; }
    /// <summary>持久化的控制台条目。</summary>
    public ObservableCollection<TableKitConsoleEntryViewModel> ConsoleEntries { get; }
    /// <summary>额外输出目标集合。</summary>
    public ObservableCollection<TableKitExtraOutputViewModel> ExtraOutputTargets { get; }
    /// <summary>获取是否已经配置额外输出目标。</summary>
    public bool HasExtraOutputTargets => ExtraOutputTargets.Count > 0;
    /// <summary>验证阶段的 JSON 预览表集合。</summary>
    public ObservableCollection<TableKitPreviewTableViewModel> PreviewTables { get; }

    /// <summary>获取或设置 luban.conf 路径。</summary>
    public string ConfigPath { get => mConfigPath; set { if (SetProperty(ref mConfigPath, value)) RefreshEnvironment(); } }
    /// <summary>获取或设置 Luban 可执行文件或 DLL 路径。</summary>
    public string LubanExecutablePath { get => mLubanExecutablePath; set { if (SetProperty(ref mLubanExecutablePath, value)) RefreshEnvironment(); } }
    /// <summary>获取或设置 Luban 工作目录。</summary>
    public string LubanWorkDir { get => mLubanWorkDir; set { if (SetProperty(ref mLubanWorkDir, value)) RefreshEnvironment(); } }
    /// <summary>获取或设置 Luban target 名称。</summary>
    public string TargetName { get => mTargetName; set => SetProperty(ref mTargetName, value); }
    /// <summary>获取或设置 Luban code target。</summary>
    public string CodeTarget { get => mCodeTarget; set => SetProperty(ref mCodeTarget, value); }
    /// <summary>获取或设置 Luban data target。</summary>
    public string DataTarget { get => mDataTarget; set => SetProperty(ref mDataTarget, value); }
    /// <summary>获取或设置代码输出目录。</summary>
    public string OutputCodeDir { get => mOutputCodeDir; set => SetProperty(ref mOutputCodeDir, value); }
    /// <summary>获取或设置数据输出目录。</summary>
    public string OutputDataDir
    {
        get => mOutputDataDir;
        set
        {
            if (!SetProperty(ref mOutputDataDir, value)) return;
            if (!mRuntimePathPatternIsCustom) RefreshInferredRuntimePathPattern();
            if (!mCustomEditorDataPath) RefreshInferredEditorDataPath();
            OnPropertyChanged(nameof(RuntimeLocationPreview));
        }
    }
    /// <summary>获取或设置是否直接以 Luban 表名作为资源地址。</summary>
    public bool IsAddressable
    {
        get => mIsAddressable;
        set
        {
            if (!SetProperty(ref mIsAddressable, value)) return;
            OnPropertyChanged(nameof(IsRuntimePathVisible));
            OnPropertyChanged(nameof(RuntimeLocationPreview));
        }
    }
    /// <summary>获取或设置非可寻址模式交给 Loader 的运行时路径模板。</summary>
    public string RuntimePathPattern
    {
        get => mRuntimePathPattern;
        set
        {
            if (!SetProperty(ref mRuntimePathPattern, value)) return;
            mRuntimePathPatternIsCustom = true;
            OnPropertyChanged(nameof(RuntimeLocationPreview));
        }
    }
    /// <summary>获取是否显示非可寻址模式的路径模板输入。</summary>
    public bool IsRuntimePathVisible => !mIsAddressable;
    /// <summary>获取当前配置可解析出的运行时定位摘要。</summary>
    public string RuntimeLocationPreview => ResolveRuntimeLocationPreview();
    /// <summary>获取或设置是否使用自定义编辑器数据路径。</summary>
    public bool CustomEditorDataPath
    {
        get => mCustomEditorDataPath;
        set
        {
            if (!SetProperty(ref mCustomEditorDataPath, value) || value) return;
            RefreshInferredEditorDataPath();
        }
    }
    /// <summary>获取或设置编辑器读取的配置数据路径。</summary>
    public string EditorDataPath { get => mEditorDataPath; set => SetProperty(ref mEditorDataPath, value); }
    /// <summary>获取或设置异步加载入口开关。</summary>
    public bool UseAsyncLoading { get => mUseAsyncLoading; set => SetProperty(ref mUseAsyncLoading, value); }
    /// <summary>获取或设置原始资源读取开关。</summary>
    public bool UseRawResourceLoading
    {
        get => mUseRawResourceLoading;
        set
        {
            if (SetProperty(ref mUseRawResourceLoading, value)) OnPropertyChanged(nameof(LoaderText));
        }
    }
    /// <summary>获取或设置 Luban 外部类型 helper 生成开关。</summary>
    public bool GenerateExternalTypeUtil { get => mGenerateExternalTypeUtil; set => SetProperty(ref mGenerateExternalTypeUtil, value); }
    /// <summary>获取或设置 Unity asmdef 生成开关；Godot 使用 csproj 项目边界。</summary>
    public bool UseAssemblyDefinition { get => mUseAssemblyDefinition; set => SetProperty(ref mUseAssemblyDefinition, value); }
    /// <summary>获取或设置 Unity asmdef 或 Godot csproj 使用的程序集名称。</summary>
    public string AssemblyName { get => mAssemblyName; set => SetProperty(ref mAssemblyName, value); }
    /// <summary>获取最近操作状态。</summary>
    public string StatusText
    {
        get => mStatusText;
        private set
        {
            if (SetProperty(ref mStatusText, value)) OnPropertyChanged(nameof(ConsoleSummaryText));
        }
    }
    /// <summary>获取最近操作的补充说明。</summary>
    public string StatusDetailText { get => mStatusDetailText; private set => SetProperty(ref mStatusDetailText, value); }
    /// <summary>获取 Luban ON/OFF 状态文本。</summary>
    public string LubanStatusText { get => mLubanStatusText; private set => SetProperty(ref mLubanStatusText, value); }
    /// <summary>获取当前环境诊断文本。</summary>
    public string EnvironmentMessage { get => mEnvironmentMessage; private set => SetProperty(ref mEnvironmentMessage, value); }
    /// <summary>获取可复制的当前 Luban 命令预览。</summary>
    public string CommandPreviewText { get => mCommandPreviewText; private set => SetProperty(ref mCommandPreviewText, value); }
    /// <summary>获取解析出的完整表管理器类型。</summary>
    public string TablesType { get => mTablesType; private set => SetProperty(ref mTablesType, value); }
    /// <summary>获取解析出的实际数据扩展名。</summary>
    public string DataExtension { get => mDataExtension; private set => SetProperty(ref mDataExtension, value); }
    /// <summary>获取验证预览目录。</summary>
    public string PreviewDirectory { get => mPreviewDirectory; private set => SetProperty(ref mPreviewDirectory, value); }
    /// <summary>获取或设置预览搜索关键字。</summary>
    public string PreviewSearch { get => mPreviewSearch; set { if (SetProperty(ref mPreviewSearch, value)) OnPropertyChanged(nameof(FilteredPreviewTables)); } }
    /// <summary>获取过滤后的预览表集合。</summary>
    public IEnumerable<TableKitPreviewTableViewModel> FilteredPreviewTables => string.IsNullOrWhiteSpace(PreviewSearch)
        ? PreviewTables
        : PreviewTables.Where(table => table.Name.Contains(PreviewSearch, StringComparison.OrdinalIgnoreCase));
    /// <summary>获取当前选中的预览表。</summary>
    public TableKitPreviewTableViewModel? SelectedPreviewTable
    {
        get => mSelectedPreviewTable;
        set
        {
            if (SetProperty(ref mSelectedPreviewTable, value))
            {
                SelectedPreviewRecord = value?.Records.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedPreviewJson));
                OnPropertyChanged(nameof(HasPreviewSelection));
                OnPropertyChanged(nameof(SelectedPreviewRecords));
                OnPropertyChanged(nameof(SelectedPreviewTableSummary));
            }
        }
    }
    /// <summary>获取当前选中的记录。</summary>
    public TableKitPreviewRecordViewModel? SelectedPreviewRecord
    {
        get => mSelectedPreviewRecord;
        set
        {
            if (SetProperty(ref mSelectedPreviewRecord, value))
            {
                OnPropertyChanged(nameof(SelectedPreviewJson));
                OnPropertyChanged(nameof(SelectedPreviewFields));
                OnPropertyChanged(nameof(HasPreviewRecord));
                OnPropertyChanged(nameof(SelectedPreviewRecordSummary));
            }
        }
    }
    /// <summary>获取当前表的记录集合。</summary>
    public IReadOnlyList<TableKitPreviewRecordViewModel> SelectedPreviewRecords =>
        SelectedPreviewTable?.Records ?? Array.Empty<TableKitPreviewRecordViewModel>();
    /// <summary>获取当前记录的结构化字段。</summary>
    public IReadOnlyList<TableKitPreviewFieldViewModel> SelectedPreviewFields =>
        SelectedPreviewRecord?.Fields ?? Array.Empty<TableKitPreviewFieldViewModel>();
    /// <summary>获取当前记录 JSON；记录为空时回落到完整表 JSON。</summary>
    public string SelectedPreviewJson => SelectedPreviewRecord?.PreviewJson ?? SelectedPreviewTable?.PreviewJson ?? string.Empty;
    /// <summary>获取当前是否存在预览表。</summary>
    public bool HasPreviewTables => PreviewTables.Count > 0;
    /// <summary>获取验证预览表数量摘要。</summary>
    public string PreviewCountText => HasPreviewTables ? PreviewTables.Count + " 张表" : "等待验证";
    /// <summary>获取当前是否存在预览选择。</summary>
    public bool HasPreviewSelection => SelectedPreviewTable != null;
    /// <summary>获取当前是否存在可浏览记录。</summary>
    public bool HasPreviewRecord => SelectedPreviewRecord != null;
    /// <summary>获取当前表的记录数量摘要。</summary>
    public string SelectedPreviewTableSummary => SelectedPreviewTable?.RecordSummary ?? "未选择表";
    /// <summary>获取当前记录的字段数量摘要。</summary>
    public string SelectedPreviewRecordSummary => SelectedPreviewRecord?.FieldCountText ?? "未选择记录";
    /// <summary>获取预览状态摘要。</summary>
    public string PreviewStatusText => HasPreviewTables ? PreviewDirectory : "验证配置后显示 Luban 临时 JSON 预览。";
    /// <summary>获取控制台是否为空。</summary>
    public bool IsConsoleEmpty => ConsoleEntries.Count == 0;
    /// <summary>获取控制台区域的紧凑状态摘要。</summary>
    public string ConsoleCountText => IsConsoleEmpty ? "等待操作" : ConsoleEntries.Count + " 条日志";
    /// <summary>获取控制台错误条目数量。</summary>
    public int ConsoleErrorCount => ConsoleEntries.Count(entry => string.Equals(entry.Level, "ERROR", StringComparison.OrdinalIgnoreCase));
    /// <summary>获取收起控制台时的一行状态摘要。</summary>
    public string ConsoleSummaryText => IsConsoleEmpty
        ? StatusText + " · 等待操作 · 0 错误"
        : StatusText + " · " + PreviewCountText + " · " + ConsoleCountText + " · " + ConsoleErrorCount + " 错误";
    /// <summary>获取或设置控制台是否展开显示日志正文。</summary>
    public bool IsConsoleExpanded
    {
        get => mIsConsoleExpanded;
        set
        {
            if (SetProperty(ref mIsConsoleExpanded, value)) OnPropertyChanged(nameof(ConsoleToggleText));
        }
    }
    /// <summary>获取控制台展开按钮文本。</summary>
    public string ConsoleToggleText => IsConsoleExpanded ? "收起" : "展开";
    /// <summary>获取或设置当前任务工作区，0 为配置，1 为数据。</summary>
    public int SelectedWorkspaceIndex
    {
        get => mSelectedWorkspaceIndex;
        set => SetProperty(ref mSelectedWorkspaceIndex, value);
    }
    /// <summary>获取当前 Luban 是否可用。</summary>
    public bool LubanAvailable { get => mLubanAvailable; private set => SetProperty(ref mLubanAvailable, value); }
    /// <summary>获取当前 Luban 是否不可用，用于状态提示样式。</summary>
    public bool LubanUnavailable => !LubanAvailable;
    /// <summary>获取 Luban 包标识；宿主未上报时显示默认安装契约。</summary>
    public string PackageName { get => mPackageName; private set => SetProperty(ref mPackageName, value); }
    /// <summary>获取 Luban Runtime asmdef 名称。</summary>
    public string AsmdefName { get => mAsmdefName; private set => SetProperty(ref mAsmdefName, value); }
    /// <summary>获取 Luban 运行时类型摘要。</summary>
    public string LubanTypeName { get => mLubanTypeName; private set => SetProperty(ref mLubanTypeName, value); }
    /// <summary>获取当前加载器策略摘要。</summary>
    public string LoaderText => string.IsNullOrWhiteSpace(mLoaderSummary)
        ? (UseRawResourceLoading ? "ResKit.LoadRaw / LoadRawText" : "ResKit.Load<TextAsset>")
        : mLoaderSummary;

    /// <summary>读取 Luban 配置并显示临时 JSON 预览。</summary>
    public AsyncRelayCommand ValidateCommand { get; }
    /// <summary>执行 Luban 正式生成。</summary>
    public AsyncRelayCommand GenerateCommand { get; }
    /// <summary>重新读取 target 列表和环境状态。</summary>
    public ICommand RefreshConfigCommand { get; }
    /// <summary>保存 Workbench-only 配置。</summary>
    public ICommand SaveCommand { get; }
    /// <summary>还原当前项目默认配置。</summary>
    public ICommand ResetCommand { get; }
    /// <summary>添加额外导出目标。</summary>
    public ICommand AddExtraOutputCommand { get; }
    /// <summary>复制控制台日志。</summary>
    public AsyncRelayCommand CopyConsoleCommand { get; }
    /// <summary>清空控制台日志。</summary>
    public ICommand ClearConsoleCommand { get; }
    /// <summary>选择 Luban 工作目录。</summary>
    public AsyncRelayCommand BrowseLubanWorkDirCommand { get; }
    /// <summary>选择实际 Luban.dll 文件。</summary>
    public AsyncRelayCommand BrowseLubanExecutableCommand { get; }
    /// <summary>选择数据输出目录。</summary>
    public AsyncRelayCommand BrowseOutputDataCommand { get; }
    /// <summary>选择代码输出目录。</summary>
    public AsyncRelayCommand BrowseOutputCodeCommand { get; }
    /// <summary>选择编辑器数据目录。</summary>
    public AsyncRelayCommand BrowseEditorDataCommand { get; }
    /// <summary>在系统文件管理器中打开配置表目录。</summary>
    public AsyncRelayCommand OpenConfigDirectoryCommand { get; }
}
