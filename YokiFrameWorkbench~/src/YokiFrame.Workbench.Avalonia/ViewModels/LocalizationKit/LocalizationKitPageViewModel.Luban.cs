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
                InvalidateCatalog("Luban 工作目录已变更，点击刷新");
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
            StatusText = "Luban 配置读取失败: " + exception.Message;
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
            StatusText = "Luban 配置保存失败: " + exception.Message;
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
            StatusText = "当前窗口没有可用的目录选择器。";
            return;
        }

        string? selectedDirectory = await mFolderPicker.PickFolderAsync(
            "选择 Luban 工作目录",
            suggestedPath: GetLubanWorkDirPickerStartDirectory());
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }

        if (!TryConvertToProjectRelativeDirectory(selectedDirectory, out string relativeDirectory))
        {
            StatusText = "Luban 工作目录必须位于当前项目内。";
            return;
        }

        LubanWorkDir = relativeDirectory;
        if (TryPersistLubanWorkspaceSettings())
        {
            StatusText = "Luban 工作目录已保存，点击刷新读取 Excel。";
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
        StatusText = "正在定位 Excel 作者目录";
        LocalizationLubanWorkspaceResult result = await Task.Run(() =>
            mService.ResolveLubanWorkspace(projectRoot, lubanWorkDir));
        if (!result.Succeeded)
        {
            StatusText = "失败: " + string.Join("; ", result.Diagnostics);
            return;
        }

        if (!Directory.Exists(result.WorkbookDirectory))
        {
            StatusText = "Excel 作者目录尚不存在，请先创建模板。";
            return;
        }

        if (mOpenDirectoryAsync == null)
        {
            StatusText = "当前窗口没有可用的目录打开服务。";
            return;
        }

        await mOpenDirectoryAsync(result.WorkbookDirectory);
        StatusText = "已打开 Excel 作者目录。";
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
        StatusText = "正在创建 Luban 本地化模板";
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
                StatusText = "失败: " + string.Join("; ", result.Diagnostics);
                return;
            }

            StatusText = result.Diagnostics.Count == 0
                ? "Luban 本地化模板已创建"
                : "模板已创建: " + string.Join("; ", result.Diagnostics);
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
}
