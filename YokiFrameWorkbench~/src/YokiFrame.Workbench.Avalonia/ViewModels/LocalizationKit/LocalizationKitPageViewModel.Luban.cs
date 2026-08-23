using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.LocalizationKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 LocalizationKit Luban 工作目录配置、模板入口和作者目录打开操作。</summary>
public sealed partial class LocalizationKitPageViewModel
{
    private readonly LocalizationKitSettingsService mSettingsService;
    private readonly IInstallerFolderPicker? mFolderPicker;
    private readonly Func<string, Task>? mOpenDirectoryAsync;
    private string mLubanWorkDir = string.Empty;
    private bool mLubanSettingsDirty;

    /// <summary>当前项目的可选 Luban 工作目录；留空时按项目常见路径自动发现。</summary>
    public string LubanWorkDir
    {
        get => mLubanWorkDir;
        set
        {
            if (SetProperty(ref mLubanWorkDir, value?.Trim() ?? string.Empty))
            {
                mLubanSettingsDirty = true;
                InvalidateCatalog(GetString(WorkDirChangedKey, "Luban 工作目录已变更，点击刷新"));
            }
        }
    }

    /// <summary>打开原生目录选择器配置 Luban 工作目录。</summary>
    public AsyncRelayCommand BrowseLubanWorkDirCommand { get; }

    /// <summary>打开当前 LocalizationKit Excel 作者目录。</summary>
    public AsyncRelayCommand OpenExcelDirectoryCommand { get; }

    /// <summary>从项目级 Workbench 设置读取工作目录，损坏配置回落自动发现而不阻断页面。</summary>
    private void LoadLubanWorkspaceSettings()
    {
        try
        {
            LocalizationKitWorkbenchSettings settings = mSettingsService.Load(mProjectRoot);
            mLubanWorkDir = settings.LubanWorkDir?.Trim() ?? string.Empty;
            mLubanSettingsDirty = false;
            OnPropertyChanged(nameof(LubanWorkDir));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            mLubanWorkDir = string.Empty;
            mLubanSettingsDirty = false;
            OnPropertyChanged(nameof(LubanWorkDir));
            SetStatus(string.Format(GetString(ConfigLoadFailedTemplateKey, "Luban 配置读取失败: {0}"), exception.Message));
        }
    }

    /// <summary>只在用户变更目录后保存项目级配置，避免页面周期刷新重复写入磁盘。</summary>
    /// <returns>已保存或没有待保存变更时返回 true。</returns>
    private bool TryPersistLubanWorkspaceSettings()
    {
        if (!mLubanSettingsDirty)
        {
            return true;
        }

        try
        {
            mSettingsService.Save(mProjectRoot, new LocalizationKitWorkbenchSettings
            {
                LubanWorkDir = LubanWorkDir
            });
            mLubanSettingsDirty = false;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            SetStatus(string.Format(GetString(ConfigSaveFailedTemplateKey, "Luban 配置保存失败: {0}"), exception.Message));
            return false;
        }
    }

    /// <summary>Workbench 关闭前提交尚未触发其它操作的 Luban 工作目录草稿。</summary>
    internal void PersistLubanWorkspaceSettingsOnClose()
    {
        TryPersistLubanWorkspaceSettings();
    }

    /// <summary>通过宿主目录选择器选择项目内 Luban 工作目录，并立即保存该显式覆盖项。</summary>
    /// <returns>目录选择和保存完成任务。</returns>
    private async Task BrowseLubanWorkDirAsync()
    {
        if (mFolderPicker == null)
        {
            SetStatus(GetString(NoFolderPickerKey, "当前窗口没有可用的目录选择器。"));
            return;
        }

        string? selectedDirectory = await mFolderPicker.PickFolderAsync(
            GetString(PickWorkDirTitleKey, "选择 Luban 工作目录"),
            suggestedPath: GetLubanWorkDirPickerStartDirectory());
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }

        if (!TryConvertToProjectRelativeDirectory(selectedDirectory, out string relativeDirectory))
        {
            SetStatus(GetString(WorkDirOutsideProjectKey, "Luban 工作目录必须位于当前项目内。"));
            return;
        }

        LubanWorkDir = relativeDirectory;
        if (TryPersistLubanWorkspaceSettings())
        {
            SetStatus(GetString(WorkDirSavedKey, "Luban 工作目录已保存，点击刷新读取 Excel。"));
        }
    }

    /// <summary>定位并通过宿主回调打开当前 Excel 作者目录；目录不存在时要求用户显式创建模板。</summary>
    /// <returns>目录打开完成任务。</returns>
    private async Task OpenExcelDirectoryAsync()
    {
        if (!TryPersistLubanWorkspaceSettings())
        {
            return;
        }

        string projectRoot = mProjectRoot;
        string lubanWorkDir = LubanWorkDir;
        SetStatus(GetString(LocatingExcelKey, "正在定位 Excel 作者目录"));
        LocalizationLubanWorkspaceResult result = await Task.Run(() =>
            mService.ResolveLubanWorkspace(projectRoot, lubanWorkDir));
        if (!result.Succeeded)
        {
            SetStatus(string.Format(GetString(FailedStatusTemplateKey, "失败: {0}"), string.Join("; ", result.Diagnostics)));
            return;
        }

        if (!Directory.Exists(result.WorkbookDirectory))
        {
            SetStatus(GetString(ExcelDirMissingKey, "Excel 作者目录尚不存在，请先创建模板。"));
            return;
        }

        if (mOpenDirectoryAsync == null)
        {
            SetStatus(GetString(NoDirectoryOpenerKey, "当前窗口没有可用的目录打开服务。"));
            return;
        }

        await mOpenDirectoryAsync(result.WorkbookDirectory);
        SetStatus(GetString(ExcelOpenedKey, "已打开 Excel 作者目录。"));
    }

    /// <summary>显式创建 LocalizationKit 自有 Luban XML 与 Excel 模板，避免刷新操作隐式写入作者文件。</summary>
    /// <returns>模板创建和随后刷新完成任务。</returns>
    private async Task CreateLubanTemplateAsync()
    {
        if (mIsCreatingTemplate)
        {
            return;
        }

        if (!TryPersistLubanWorkspaceSettings())
        {
            return;
        }

        mIsCreatingTemplate = true;
        SetStatus(GetString(CreatingTemplateKey, "正在创建 Luban 本地化模板"));
        try
        {
            string projectRoot = mProjectRoot;
            string lubanWorkDir = LubanWorkDir;
            LocalizationOperationResult result = await Task.Run(() => mService.GenerateLubanTemplate(new LocalizationLubanTemplateRequest
            {
                ProjectRoot = projectRoot,
                LubanWorkDir = lubanWorkDir
            }));
            if (!result.Succeeded)
            {
                SetStatus(string.Format(GetString(FailedStatusTemplateKey, "失败: {0}"), string.Join("; ", result.Diagnostics)));
                return;
            }

            SetStatus(result.Diagnostics.Count == 0
                ? GetString(TemplateCreatedKey, "Luban 本地化模板已创建")
                : string.Format(GetString(TemplateCreatedPathKey, "模板已创建: {0}"), string.Join("; ", result.Diagnostics)));
            await RefreshAsync();
        }
        finally
        {
            mIsCreatingTemplate = false;
        }
    }

    /// <summary>优先以已配置工作目录作为选择器起点；无效或未配置时回退当前项目根。</summary>
    /// <returns>可传给原生目录选择器的绝对起始目录。</returns>
    private string GetLubanWorkDirPickerStartDirectory()
    {
        if (string.IsNullOrWhiteSpace(LubanWorkDir) || Path.IsPathFullyQualified(LubanWorkDir))
        {
            return mProjectRoot;
        }

        string candidate = Path.GetFullPath(Path.Combine(mProjectRoot, LubanWorkDir));
        return Directory.Exists(candidate) ? candidate : mProjectRoot;
    }

    /// <summary>把目录选择结果转换为项目相对路径，并拒绝项目外目录以保持模板输出的 containment 约束。</summary>
    /// <param name="selectedDirectory">原生选择器返回的绝对目录。</param>
    /// <param name="relativeDirectory">成功时返回项目相对目录。</param>
    /// <returns>目录属于当前项目时返回 true。</returns>
    private bool TryConvertToProjectRelativeDirectory(string selectedDirectory, out string relativeDirectory)
    {
        string fullDirectory = Path.GetFullPath(selectedDirectory);
        string relativePath = Path.GetRelativePath(mProjectRoot, fullDirectory);
        bool outsideProject = relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relativePath);
        if (outsideProject)
        {
            relativeDirectory = string.Empty;
            return false;
        }

        relativeDirectory = relativePath;
        return true;
    }

    /// <summary>Luban 工作目录变更提示资源 key。</summary>
    private const string WorkDirChangedKey = "String.LocalizationKit.WorkDirChanged";

    /// <summary>Luban 配置读取失败模板资源 key。</summary>
    private const string ConfigLoadFailedTemplateKey = "String.LocalizationKit.ConfigLoadFailedTemplate";

    /// <summary>Luban 配置保存失败模板资源 key。</summary>
    private const string ConfigSaveFailedTemplateKey = "String.LocalizationKit.ConfigSaveFailedTemplate";

    /// <summary>无目录选择器提示资源 key。</summary>
    private const string NoFolderPickerKey = "String.LocalizationKit.NoFolderPicker";

    /// <summary>工作目录选择器标题资源 key。</summary>
    private const string PickWorkDirTitleKey = "String.LocalizationKit.PickWorkDirTitle";

    /// <summary>工作目录超出项目边界提示资源 key。</summary>
    private const string WorkDirOutsideProjectKey = "String.LocalizationKit.WorkDirOutsideProject";

    /// <summary>工作目录已保存提示资源 key。</summary>
    private const string WorkDirSavedKey = "String.LocalizationKit.WorkDirSaved";

    /// <summary>正在定位 Excel 目录提示资源 key。</summary>
    private const string LocatingExcelKey = "String.LocalizationKit.LocatingExcel";

    /// <summary>Excel 目录不存在提示资源 key。</summary>
    private const string ExcelDirMissingKey = "String.LocalizationKit.ExcelDirMissing";

    /// <summary>无目录打开服务提示资源 key。</summary>
    private const string NoDirectoryOpenerKey = "String.LocalizationKit.NoDirectoryOpener";

    /// <summary>Excel 目录已打开提示资源 key。</summary>
    private const string ExcelOpenedKey = "String.LocalizationKit.ExcelOpened";

    /// <summary>正在创建模板提示资源 key。</summary>
    private const string CreatingTemplateKey = "String.LocalizationKit.CreatingTemplate";

    /// <summary>模板创建完成提示资源 key。</summary>
    private const string TemplateCreatedKey = "String.LocalizationKit.TemplateCreated";

    /// <summary>模板创建路径模板资源 key。</summary>
    private const string TemplateCreatedPathKey = "String.LocalizationKit.TemplateCreatedPathTemplate";

    /// <summary>失败状态模板资源 key。</summary>
    private const string FailedStatusTemplateKey = "String.LocalizationKit.FailedStatusTemplate";
}
