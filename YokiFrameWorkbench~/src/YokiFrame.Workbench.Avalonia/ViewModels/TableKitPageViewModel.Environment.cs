using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Services.Luban;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 TableKit 环境检查、目录选择和工具发现。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>重新读取当前 luban.conf 的 target，并刷新环境摘要；磁盘读取离开 UI 线程。</summary>
    private async Task RefreshConfigurationAsync()
    {
        await RefreshEnvironmentAsync();
        string configPath = ResolveInputPath(ConfigPath);
        try
        {
            IReadOnlyList<string> names = await Task.Run(() => mService.ReadLubanTargetNames(configPath));
            foreach (string name in names)
            {
                AddOption(TargetOptions, name);
            }

            AppendConsole("INFO", GetString(TargetsRefreshedKey, "已刷新 Luban target 列表。"), false);
        }
        catch (FileNotFoundException)
        {
            // 首次接入项目尚无 luban.conf 属正常状态，保持静默。
        }
        catch (Exception exception)
        {
            AppendConsole("WARNING", string.Format(GetString(ConfParseFailedTemplateKey, "luban.conf 解析失败: {0}"), exception.Message), false);
        }
    }

    /// <summary>通过跨平台目录选择器设置 Luban 工作目录。</summary>
    private async Task BrowseLubanWorkDirAsync()
    {
        await PickFolderAsync(GetString(PickWorkDirTitleKey, "选择 Luban 工作目录"), LubanWorkDir, false, path =>
        {
            LubanWorkDir = path;
            string currentConfig = ResolveInputPath(ConfigPath);
            if (string.IsNullOrWhiteSpace(ConfigPath) || !mService.InspectEnvironmentPaths(currentConfig, string.Empty).ConfigExists)
            {
                ConfigPath = ToProjectRelativePath(Path.Combine(ResolveInputPath(path), "luban.conf"));
            }
        });
    }

    /// <summary>通过文件选择器设置实际 Luban.dll 路径。</summary>
    private async Task BrowseLubanExecutableAsync()
    {
        if (mLubanFilePicker == null)
        {
            StatusDetailText = GetString(NoLubanFilePickerKey, "当前窗口没有可用的 Luban.dll 文件选择器。");
            return;
        }

        string suggested = TableKitPathUtilities.FindPickerStartDirectory(mProjectRoot, LubanExecutablePath, true);
        string? selected = await mLubanFilePicker.PickLubanDllAsync(GetString(PickLubanDllTitleKey, "选择 Luban.dll"), suggestedPath: suggested);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            LubanExecutablePath = ToProjectRelativePath(selected);
        }
    }

    /// <summary>通过文件选择器设置可选 Luban.Agent 文件。</summary>
    private async Task BrowseLubanAgentAsync()
    {
        await PickLubanToolAsync(
            GetString(PickLubanAgentTitleKey, "选择 Luban.Agent.dll"),
            "Luban.Agent.dll",
            LubanAgentExecutablePath,
            path => LubanAgentExecutablePath = path);
    }

    /// <summary>通过文件选择器设置可选 Luban.Mcp 文件。</summary>
    private async Task BrowseLubanMcpAsync()
    {
        await PickLubanToolAsync(
            GetString(PickLubanMcpTitleKey, "选择 Luban.Mcp.dll"),
            "Luban.Mcp.dll",
            LubanMcpExecutablePath,
            path => LubanMcpExecutablePath = path);
    }

    /// <summary>通过目录选择器设置可选 Luban Skill 源目录。</summary>
    private async Task BrowseLubanSkillsAsync()
    {
        await PickFolderAsync(
            GetString(PickLubanSkillsTitleKey, "选择 Luban Skill 目录"),
            LubanSkillsPath,
            false,
            path => LubanSkillsPath = path);
    }

    /// <summary>根据三个可选路径是否真实存在，生成给用户看的校验摘要。</summary>
    private string CreateOptionalLubanToolsSummary()
    {
        int configured = CountExistingOptionalTools();
        return configured == 0
            ? GetString(OptionalLubanToolsEmptyKey, "未配置官方 Agent、MCP 或 Skill；旧版 Luban 可继续验证和生成。")
            : string.Format(
                GetString(OptionalLubanToolsReadyKey, "已识别 {0}/3 项官方 AI 路径。配表需求由 YokiFrame Skill 自动读取这些路径并导入官方 Skill。"),
                configured);
    }

    /// <summary>统计当前页面已配置且实际存在的官方 AI 路径数量。</summary>
    private int CountExistingOptionalTools()
    {
        return mService.CountExistingOptionalTools(
            ResolveOptionalInputPath(LubanAgentExecutablePath),
            ResolveOptionalInputPath(LubanMcpExecutablePath),
            ResolveOptionalInputPath(LubanSkillsPath));
    }

    /// <summary>把用户选择的 Skill 根或 skills 子目录解析成包含官方 SKILL.md 的目录。</summary>
    /// <param name="path">用户选择的 Skill 根或子目录。</param>
    /// <returns>包含官方 SKILL.md 的目录；无法解析时为空。</returns>
    private string ResolveOfficialSkillsRoot(string path)
    {
        return LubanProjectDiscoveryService.ResolveOfficialSkillsRoot(ResolveOptionalInputPath(path));
    }

    /// <summary>按文件名过滤选择可选 Luban 工具，并将结果转换为项目相对路径。</summary>
    /// <param name="title">文件选择器标题。</param>
    /// <param name="fileName">允许选择的文件名。</param>
    /// <param name="currentPath">字段当前显示的路径。</param>
    /// <param name="apply">接收项目相对路径的更新回调。</param>
    private async Task PickLubanToolAsync(string title, string fileName, string currentPath, Action<string> apply)
    {
        if (mLubanFilePicker == null)
        {
            StatusDetailText = GetString(NoLubanFilePickerKey, "当前窗口没有可用的 Luban 文件选择器。");
            return;
        }

        string suggested = TableKitPathUtilities.FindPickerStartDirectory(mProjectRoot, currentPath, true);
        string? selected = await mLubanFilePicker.PickLubanFileAsync(title, fileName, suggestedPath: suggested);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            apply(ToProjectRelativePath(selected));
        }
    }

    /// <summary>选择正式数据输出目录。</summary>
    private async Task BrowseOutputDataAsync()
    {
        await PickFolderAsync(GetString(PickDataDirTitleKey, "选择 TableKit 数据输出目录"), OutputDataDir, false, path => OutputDataDir = path);
    }

    /// <summary>选择正式代码输出目录。</summary>
    private async Task BrowseOutputCodeAsync()
    {
        await PickFolderAsync(GetString(PickCodeDirTitleKey, "选择 TableKit 代码输出目录"), OutputCodeDir, false, path => OutputCodeDir = path);
    }

    /// <summary>选择编辑器读取的数据目录。</summary>
    private async Task BrowseEditorDataAsync()
    {
        await PickFolderAsync(GetString(PickEditorDataDirTitleKey, "选择 TableKit 编辑器数据目录"), EditorDataPath, false, path => EditorDataPath = path);
    }

    /// <summary>从字段当前路径打开选择器，并转换为项目相对路径。</summary>
    /// <param name="title">原生目录选择器标题。</param>
    /// <param name="currentPath">字段当前显示的路径。</param>
    /// <param name="isFilePath">字段是否指向文件。</param>
    /// <param name="apply">接收相对路径的字段更新回调。</param>
    private async Task PickFolderAsync(string title, string currentPath, bool isFilePath, Action<string> apply)
    {
        if (mFolderPicker == null)
        {
            StatusDetailText = GetString(NoFolderPickerKey, "当前窗口没有可用的目录选择器。");
            return;
        }

        string suggested = TableKitPathUtilities.FindPickerStartDirectory(mProjectRoot, currentPath, isFilePath);
        string? selected = await mFolderPicker.PickFolderAsync(title, suggestedPath: suggested);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            apply(ToProjectRelativePath(selected));
        }
    }

    /// <summary>打开 Luban Datas 目录；不存在时打开工作目录。</summary>
    private async Task OpenConfigDirectoryAsync()
    {
        if (mOpenDirectoryAsync == null)
        {
            StatusDetailText = GetString(ConfigDirOpenFailedTemplateKey, "打开配置表目录失败: {0}");
            return;
        }

        string target = await Task.Run(() => mService.ResolveConfigDirectory(ResolveInputPath(LubanWorkDir)));
        if (string.IsNullOrWhiteSpace(target))
        {
            StatusDetailText = string.Format(GetString(ConfigDirMissingTemplateKey, "Luban 配置目录不存在: {0}"), ResolveInputPath(LubanWorkDir));
            return;
        }

        try
        {
            await mOpenDirectoryAsync(target);
            StatusDetailText = string.Format(GetString(ConfigDirOpenedTemplateKey, "已打开配置表目录: {0}"), target);
        }
        catch (Exception exception)
        {
            StatusDetailText = string.Format(GetString(ConfigDirOpenFailedTemplateKey, "打开配置表目录失败: {0}"), exception.Message);
        }
    }

    /// <summary>在后台检查 Luban 配置和工具路径，回到 UI 后更新摘要。</summary>
    private async Task RefreshEnvironmentAsync()
    {
        string configPath = ResolveInputPath(ConfigPath);
        string executablePath = ResolveInputPath(LubanExecutablePath);
        (bool ConfigExists, bool ExecutableExists) existence = await Task.Run(
            () => mService.InspectEnvironmentPaths(configPath, executablePath));
        LubanAvailable = existence.ConfigExists && existence.ExecutableExists;
        RefreshOptionalLubanTools();
        OnPropertyChanged(nameof(LubanUnavailable));
        LubanStatusText = LubanAvailable ? "Luban ON" : "Luban OFF";
        EnvironmentMessage = LubanAvailable
            ? GetString(EnvironmentReadyKey, "已找到 luban.conf 和 Luban 工具，可以执行验证与生成。")
            : GetString(EnvironmentMissingKey, "请确认工作目录包含 luban.conf，并配置 Luban.dll 或可执行文件路径。");
        CommandPreviewText = CreateCommandPreview();
        OnPropertyChanged(nameof(LoaderText));
    }

    /// <summary>读取可选 AI 伴随工具路径；自动发现只补充空字段，不覆盖用户已配置的路径。</summary>
    private void RefreshOptionalLubanTools()
    {
        LubanToolDiscoveryResult discovery = mService.DiscoverLubanTools(mProjectRoot, LubanWorkDir);
        if (!discovery.Succeeded || discovery.Options == null)
        {
            return;
        }

        LubanToolOptions options = discovery.Options;
        ApplyDiscoveredOptionalPath(ref mLubanAgentExecutablePath, nameof(LubanAgentExecutablePath), options.LubanAgentExecutablePath);
        ApplyDiscoveredOptionalPath(ref mLubanMcpExecutablePath, nameof(LubanMcpExecutablePath), options.LubanMcpExecutablePath);
        ApplyDiscoveredOptionalPath(ref mLubanSkillsPath, nameof(LubanSkillsPath), options.LubanSkillsPath);
        OnPropertyChanged(nameof(AgentAvailable));
        OnPropertyChanged(nameof(OptionalLubanToolsSummary));
    }

    /// <summary>把自动发现结果写入仍为空的可选路径字段，避免刷新覆盖显式选择。</summary>
    /// <param name="currentPath">当前字段的项目相对路径。</param>
    /// <param name="propertyName">需要通知的绑定属性名。</param>
    /// <param name="discoveredPath">自动发现的绝对路径。</param>
    private void ApplyDiscoveredOptionalPath(ref string currentPath, string propertyName, string discoveredPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath) || string.IsNullOrWhiteSpace(discoveredPath))
        {
            return;
        }

        string projectedPath = ToProjectRelativePath(discoveredPath);
        if (string.Equals(currentPath, projectedPath, StringComparison.Ordinal))
        {
            return;
        }

        currentPath = projectedPath;
        OnPropertyChanged(propertyName);
    }

    /// <summary>构建当前配置的命令预览，不执行任何外部进程。</summary>
    /// <returns>便于复制诊断的命令行。</returns>
    private string CreateCommandPreview()
    {
        string executable = string.IsNullOrWhiteSpace(LubanExecutablePath) ? "dotnet Luban.dll" : LubanExecutablePath;
        return executable + " -t " + TargetName + " --conf luban.conf -c " + CodeTarget + " -d " + DataTarget;
    }
}
