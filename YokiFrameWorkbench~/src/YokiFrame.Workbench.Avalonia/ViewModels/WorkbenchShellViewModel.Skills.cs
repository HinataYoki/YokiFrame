using System.Windows.Input;
using YokiFrame.Tooling.Application.Skills;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 维护 Workbench 总览页中的 AI Skill 安装状态和操作命令。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    private const int DEFAULT_SKILL_TARGET_COUNT = 6;
    private static readonly IReadOnlyList<string> sDefaultSkillNames = new[]
    {
        "yokiframe",
        "yokiframe-cli",
        "yokiframe-workbench"
    };
    private readonly SkillInstallationService mSkillInstallationService = new();
    private IReadOnlyList<string> mSkillNames = sDefaultSkillNames;
    private IReadOnlyList<WorkbenchSkillOption> mSkillOptions = Array.Empty<WorkbenchSkillOption>();
    private IReadOnlyList<WorkbenchMetricCard> mSkillStatusCards = Array.Empty<WorkbenchMetricCard>();
    private IReadOnlyList<WorkbenchSkillTarget> mSkillTargets = Array.Empty<WorkbenchSkillTarget>();
    private SkillInstallationStatus? mSkillInstallStatus;
    private string mCustomSkillPath = "custom/skills";
    private string mCustomSkillStatusText = GetString(RelativeRootHintKey, "相对项目根目录");
    private string mSelectedSkillName = "yokiframe";
    private string mSkillInstallStatusText = GetString(WaitingProjectStatusKey, "等待项目状态");
    private string mSkillProjectRoot = string.Empty;

    /// <summary>
    /// 获取可安装的包内 Skill 名称列表。
    /// </summary>
    public IReadOnlyList<string> SkillNames
    {
        get => mSkillNames;
        private set => SetProperty(ref mSkillNames, value);
    }

    /// <summary>
    /// 获取包内 Skill 的卡片式选择入口。
    /// </summary>
    public IReadOnlyList<WorkbenchSkillOption> SkillOptions
    {
        get => mSkillOptions;
        private set => SetProperty(ref mSkillOptions, value);
    }

    /// <summary>
    /// 获取或设置当前准备安装的 Skill 名称。
    /// </summary>
    public string SelectedSkillName
    {
        get => mSelectedSkillName;
        set
        {
            var nextName = string.IsNullOrWhiteSpace(value) ? "yokiframe" : value;
            if (SetProperty(ref mSelectedSkillName, nextName))
            {
                RefreshSkillOptions();
                RefreshSkillTargets();
            }
        }
    }

    /// <summary>
    /// 获取安装器右侧顶部的来源、当前 Skill 和目标统计卡片。
    /// </summary>
    public IReadOnlyList<WorkbenchMetricCard> SkillStatusCards
    {
        get => mSkillStatusCards;
        private set => SetProperty(ref mSkillStatusCards, value);
    }

    /// <summary>
    /// 获取当前选中 Skill 在各 AI 目标中的安装状态。
    /// </summary>
    public IReadOnlyList<WorkbenchSkillTarget> SkillTargets
    {
        get => mSkillTargets;
        private set => SetProperty(ref mSkillTargets, value);
    }

    /// <summary>
    /// 获取 Skill 安装面板的摘要状态。
    /// </summary>
    public string SkillInstallStatusText
    {
        get => mSkillInstallStatusText;
        private set => SetProperty(ref mSkillInstallStatusText, value);
    }

    /// <summary>
    /// 获取或设置自定义 Skill 安装目录；该目录相对当前项目根。
    /// </summary>
    public string CustomSkillPath
    {
        get => mCustomSkillPath;
        set
        {
            if (SetProperty(ref mCustomSkillPath, value ?? string.Empty))
            {
                RefreshCustomSkillStatus();
            }
        }
    }

    /// <summary>
    /// 获取自定义目录当前安装状态文本。
    /// </summary>
    public string CustomSkillStatusText
    {
        get => mCustomSkillStatusText;
        private set => SetProperty(ref mCustomSkillStatusText, value);
    }

    /// <summary>
    /// 获取把当前 Skill 安装到自定义目录的命令。
    /// </summary>
    public ICommand InstallCustomSkillCommand { get; private set; } = null!;

    /// <summary>
    /// 获取从自定义目录卸载当前 Skill 的命令。
    /// </summary>
    public ICommand UninstallCustomSkillCommand { get; private set; } = null!;

    /// <summary>
    /// 初始化 Skill 安装面板的默认状态。
    /// </summary>
    private void InitializeSkillInstaller()
    {
        InstallCustomSkillCommand = new RelayCommand(InstallCustomSkill);
        UninstallCustomSkillCommand = new RelayCommand(UninstallCustomSkill);
        RefreshSkillOptions();
        RefreshSkillTargets();
    }

    /// <summary>
    /// 当 dashboard 切换项目根时刷新 Skill 安装状态。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    private void UpdateSkillProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.Equals(mSkillProjectRoot, projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mSkillProjectRoot = projectRoot;
        RefreshSkillStatusForCurrentProject();
    }

    /// <summary>
    /// 使用当前 dashboard 项目根刷新 Skill 状态。
    /// </summary>
    private void RefreshSkillStatusForCurrentProject()
    {
        if (string.IsNullOrWhiteSpace(mSkillProjectRoot))
        {
            SkillInstallStatusText = GetString(WaitingProjectStatusKey, "等待项目状态");
            RefreshSkillTargets();
            return;
        }

        try
        {
            mSkillInstallStatus = mSkillInstallationService.GetStatus(mSkillProjectRoot);
            RefreshSkillNames();
            RefreshSkillTargets();
            AddLogLine(GetString(StatusRefreshedKey, "Skill 状态已刷新。"));
        }
        catch (Exception exception)
        {
            SkillInstallStatusText = string.Format(GetString(StatusReadFailedTemplateKey, "Skill 状态读取失败: {0}"), exception.Message);
            AddLogLine(SkillInstallStatusText);
        }
    }

    /// <summary>
    /// 根据包内扫描结果刷新可安装 Skill 名称。
    /// </summary>
    private void RefreshSkillNames()
    {
        var names = mSkillInstallStatus?.Skills.Count > 0
            ? mSkillInstallStatus.Skills.Select(static skill => skill.Name).ToArray()
            : sDefaultSkillNames;
        SkillNames = names;
        if (!names.Contains(SelectedSkillName, StringComparer.Ordinal))
        {
            SelectedSkillName = names[0];
            return;
        }

        RefreshSkillOptions();
    }

    /// <summary>
    /// 根据当前选中 Skill 刷新目标卡片。
    /// </summary>
    private void RefreshSkillTargets()
    {
        var selectedSkill = mSkillInstallStatus?.Skills.FirstOrDefault(skill => skill.Name == SelectedSkillName);
        var isPackaged = selectedSkill?.Packaged == true;
        var targets = mSkillInstallStatus?.Targets
            .Where(static target => !target.SupportsCustomPath)
            .ToArray();
        SkillTargets = targets is { Length: > 0 }
            ? targets.Select(target => CreateSkillTarget(target, isPackaged)).ToArray()
            : CreateFallbackSkillTargets();
        SkillInstallStatusText = CreateSkillInstallStatusText(isPackaged, targets);
        SkillStatusCards = CreateSkillStatusCards(isPackaged, targets);
        RefreshCustomSkillStatus();
    }

    /// <summary>
    /// 根据 Skill 名称列表刷新顶部卡片入口。
    /// </summary>
    private void RefreshSkillOptions()
    {
        SkillOptions = SkillNames.Select(CreateSkillOption).ToArray();
    }

    /// <summary>
    /// 创建一个 Skill 选择卡片，并绑定选择命令。
    /// </summary>
    /// <param name="name">Skill 目录名。</param>
    /// <returns>Skill 选择卡片。</returns>
    private WorkbenchSkillOption CreateSkillOption(string name)
    {
        var skillName = string.IsNullOrWhiteSpace(name) ? "yokiframe" : name;
        return new WorkbenchSkillOption(
            skillName,
            CreateSkillOptionLabel(skillName),
            skillName,
            string.Equals(skillName, SelectedSkillName, StringComparison.Ordinal),
            new RelayCommand(() => SelectedSkillName = skillName));
    }

    /// <summary>
    /// 创建一个目标卡片，并为当前选中 Skill 绑定安装/卸载命令。
    /// </summary>
    /// <param name="target">服务返回的目标状态。</param>
    /// <param name="isPackaged">当前选中 Skill 是否已随包提供。</param>
    /// <returns>Workbench 目标卡片。</returns>
    private WorkbenchSkillTarget CreateSkillTarget(SkillInstallationTarget target, bool isPackaged)
    {
        var isInstalled = target.InstalledSkills.Contains(SelectedSkillName, StringComparer.Ordinal);
        return new WorkbenchSkillTarget(
            target.Id,
            target.Label,
            target.RelativePath,
            isInstalled
                ? GetString("String.Overview.Installed", "已安装")
                : GetString("String.Overview.NotInstalled", "未安装"),
            isInstalled,
            new RelayCommand(() => InstallSkill(target.Id), () => isPackaged),
            new RelayCommand(() => UninstallSkill(target.Id), () => isInstalled));
    }

    /// <summary>
    /// 创建 dashboard 尚未到达时显示的静态目标卡片。
    /// </summary>
    /// <returns>目标卡片。</returns>
    private IReadOnlyList<WorkbenchSkillTarget> CreateFallbackSkillTargets()
    {
        return new[]
        {
            CreateDisabledSkillTarget("claude", "Claude Code", ".claude/skills"),
            CreateDisabledSkillTarget("codex", "Codex", ".codex/skills"),
            CreateDisabledSkillTarget("cursor", "Cursor", ".cursor/skills"),
            CreateDisabledSkillTarget("windsurf", "Windsurf", ".windsurf/skills"),
            CreateDisabledSkillTarget("github-copilot", "GitHub Copilot", ".github/skills"),
            CreateDisabledSkillTarget("agents", "Agents", ".agents/skills")
        };
    }

    /// <summary>
    /// 创建安装面板顶部三张状态卡片。
    /// </summary>
    /// <param name="isPackaged">当前选中 Skill 是否已随包提供。</param>
    /// <param name="targets">安装目标状态。</param>
    /// <returns>状态卡片。</returns>
    private IReadOnlyList<WorkbenchMetricCard> CreateSkillStatusCards(bool isPackaged, IReadOnlyList<SkillInstallationTarget>? targets)
    {
        var installedCount = CountInstalledTargets(targets);
        var targetCount = targets?.Count ?? DEFAULT_SKILL_TARGET_COUNT;
        return new[]
        {
            new WorkbenchMetricCard(
                GetString("String.Overview.BuiltinPackage", "包内源"),
                CreateSkillSourceHeadline(),
                GetString("String.Overview.AutoDetect", "自动探测 Unity/Godot 包内 Skills"),
                isPositive: mSkillInstallStatus?.Skills.Count > 0),
            new WorkbenchMetricCard(
                GetString("String.Overview.CurrentSkill", "当前 Skill"),
                CreateSkillOptionLabel(SelectedSkillName),
                isPackaged ? GetString("String.Overview.Preloaded", "已随包提供") : GetString(PackagedMissingKey, "包内未提供"),
                isPositive: isPackaged,
                isAccent: true),
            new WorkbenchMetricCard(
                GetString(TargetsLabelKey, "安装目标"),
                installedCount + "/" + targetCount,
                GetString(TargetCoverageHintKey, "当前 Skill 的预设目标覆盖"),
                isPositive: installedCount > 0)
        };
    }

    /// <summary>
    /// 统计当前选中 Skill 已安装到多少个目标。
    /// </summary>
    /// <param name="targets">安装目标状态。</param>
    /// <returns>已安装目标数量。</returns>
    private int CountInstalledTargets(IReadOnlyList<SkillInstallationTarget>? targets)
    {
        return targets?.Count(target => target.InstalledSkills.Contains(SelectedSkillName, StringComparer.Ordinal)) ?? 0;
    }

    /// <summary>
    /// 创建包内 Skill 源目录摘要。
    /// </summary>
    /// <returns>源目录摘要。</returns>
    private string CreateSkillSourceHeadline()
    {
        if (string.IsNullOrWhiteSpace(mSkillInstallStatus?.SourceRoot))
        {
            return GetString(SourceWaitingKey, "等待扫描");
        }

        return "Core/Editor/Skills";
    }

    /// <summary>
    /// 把 Skill 目录名转换成面向用户的卡片标题。
    /// </summary>
    /// <param name="name">Skill 目录名。</param>
    /// <returns>卡片标题。</returns>
    private static string CreateSkillOptionLabel(string name)
    {
        return name switch
        {
            "yokiframe" => GetString(GuideYokiframeKey, "使用指南"),
            "yokiframe-cli" => GetString(GuideCliKey, "CLI 指南"),
            "yokiframe-workbench" => GetString(GuideWorkbenchKey, "工作台指南"),
            _ => name
        };
    }

    /// <summary>
    /// 创建未加载状态下的禁用目标卡片。
    /// </summary>
    /// <param name="id">目标标识。</param>
    /// <param name="label">显示名。</param>
    /// <param name="relativePath">目标目录。</param>
    /// <returns>禁用目标卡片。</returns>
    private static WorkbenchSkillTarget CreateDisabledSkillTarget(string id, string label, string relativePath)
    {
        return new WorkbenchSkillTarget(
            id,
            label,
            relativePath,
            "等待扫描",
            false,
            new RelayCommand(static () => { }, static () => false),
            new RelayCommand(static () => { }, static () => false));
    }

    /// <summary>
    /// 执行安装并刷新目标状态。
    /// </summary>
    /// <param name="targetId">目标标识。</param>
    private void InstallSkill(string targetId)
    {
        ExecuteSkillMutation(targetId, install: true);
    }

    /// <summary>
    /// 执行卸载并刷新目标状态。
    /// </summary>
    /// <param name="targetId">目标标识。</param>
    private void UninstallSkill(string targetId)
    {
        ExecuteSkillMutation(targetId, install: false);
    }

    /// <summary>
    /// 把当前选中的 Skill 安装到用户输入的自定义相对目录。
    /// </summary>
    private void InstallCustomSkill()
    {
        ExecuteSkillMutation("custom", install: true, CustomSkillPath);
    }

    /// <summary>
    /// 从用户输入的自定义相对目录卸载当前选中的 Skill。
    /// </summary>
    private void UninstallCustomSkill()
    {
        ExecuteSkillMutation("custom", install: false, CustomSkillPath);
    }

    /// <summary>
    /// 统一执行 Skill 安装或卸载文件操作，并把结果写入运行日志。
    /// </summary>
    /// <param name="targetId">目标标识。</param>
    /// <param name="install">为 true 时安装，否则卸载。</param>
    private void ExecuteSkillMutation(string targetId, bool install)
    {
        ExecuteSkillMutation(targetId, install, customPath: null);
    }

    /// <summary>
    /// 统一执行 Skill 安装或卸载文件操作，并把结果写入运行日志。
    /// </summary>
    /// <param name="targetId">目标标识。</param>
    /// <param name="install">为 true 时安装，否则卸载。</param>
    /// <param name="customPath">自定义目标相对目录；仅 custom 目标使用。</param>
    private void ExecuteSkillMutation(string targetId, bool install, string? customPath)
    {
        if (string.IsNullOrWhiteSpace(mSkillProjectRoot))
        {
            SkillInstallStatusText = GetString(WaitingProjectStatusKey, "等待项目状态");
            if (targetId == "custom")
            {
                CustomSkillStatusText = SkillInstallStatusText;
            }

            return;
        }

        try
        {
            var result = install
                ? mSkillInstallationService.Install(mSkillProjectRoot, targetId, SelectedSkillName, customPath)
                : mSkillInstallationService.Uninstall(mSkillProjectRoot, targetId, SelectedSkillName, customPath);
            SkillInstallStatusText = result.Log;
            if (targetId == "custom")
            {
                CustomSkillStatusText = result.Log;
            }

            AddLogLine(result.Log);
            RefreshSkillStatusForCurrentProject();
        }
        catch (Exception exception)
        {
            SkillInstallStatusText = string.Format(GetString(OperationFailedTemplateKey, "Skill 操作失败: {0}"), exception.Message);
            if (targetId == "custom")
            {
                CustomSkillStatusText = SkillInstallStatusText;
            }

            AddLogLine(SkillInstallStatusText);
        }
    }

    /// <summary>
    /// 根据当前自定义目录和选中 Skill 刷新状态文本；路径非法时只提示，不主动创建目录。
    /// </summary>
    private void RefreshCustomSkillStatus()
    {
        if (string.IsNullOrWhiteSpace(mSkillProjectRoot))
        {
            CustomSkillStatusText = GetString(WaitingProjectStatusKey, "等待项目状态");
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomSkillPath))
        {
            CustomSkillStatusText = GetString(EnterRelativePathKey, "请输入相对目录");
            return;
        }

        var normalizedPath = CustomSkillPath.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalizedPath) || normalizedPath.Contains(':', StringComparison.Ordinal))
        {
            CustomSkillStatusText = GetString(MustBeRelativeKey, "必须是项目内相对目录");
            return;
        }

        var skillFile = Path.Combine(mSkillProjectRoot, normalizedPath, SelectedSkillName, "SKILL.md");
        CustomSkillStatusText = File.Exists(skillFile)
            ? GetString("String.Overview.Installed", "已安装")
            : GetString("String.Overview.NotInstalled", "未安装");
    }

    /// <summary>
    /// 创建 Skill 安装面板摘要文本。
    /// </summary>
    /// <param name="isPackaged">当前选中 Skill 是否已随包提供。</param>
    /// <param name="targets">安装目标状态。</param>
    /// <returns>摘要文本。</returns>
    private string CreateSkillInstallStatusText(bool isPackaged, IReadOnlyList<SkillInstallationTarget>? targets)
    {
        if (mSkillInstallStatus == null)
        {
            return GetString(WaitingProjectStatusKey, "等待项目状态");
        }

        if (!isPackaged)
        {
            return string.Format(GetString(NotProvidedTemplateKey, "包内 Skill 未提供: {0}"), SelectedSkillName);
        }

        var installedCount = targets?.Count(target => target.InstalledSkills.Contains(SelectedSkillName, StringComparer.Ordinal)) ?? 0;
        var targetCount = targets?.Count ?? 0;
        return string.Format(
            GetString(InstallSummaryTemplateKey, "{0} 已安装 {1}/{2}"),
            SelectedSkillName, installedCount, targetCount);
    }

    /// <summary>相对项目根目录提示资源 key。</summary>
    private const string RelativeRootHintKey = "String.Skills.RelativeRootHint";

    /// <summary>等待项目状态占位资源 key。</summary>
    private const string WaitingProjectStatusKey = "String.Skills.WaitingProjectStatus";

    /// <summary>状态已刷新提示资源 key。</summary>
    private const string StatusRefreshedKey = "String.Skills.StatusRefreshed";

    /// <summary>状态读取失败模板资源 key。</summary>
    private const string StatusReadFailedTemplateKey = "String.Skills.StatusReadFailedTemplate";

    /// <summary>包内未提供占位资源 key。</summary>
    private const string PackagedMissingKey = "String.Skills.PackagedMissing";

    /// <summary>安装目标卡片标题资源 key。</summary>
    private const string TargetsLabelKey = "String.Skills.TargetsLabel";

    /// <summary>预设目标覆盖说明资源 key。</summary>
    private const string TargetCoverageHintKey = "String.Skills.TargetCoverageHint";

    /// <summary>等待扫描占位资源 key。</summary>
    private const string SourceWaitingKey = "String.Skills.SourceWaiting";

    /// <summary>yokiframe 指南标题资源 key。</summary>
    private const string GuideYokiframeKey = "String.Skills.GuideYokiframe";

    /// <summary>CLI 指南标题资源 key。</summary>
    private const string GuideCliKey = "String.Skills.GuideCli";

    /// <summary>工作台指南标题资源 key。</summary>
    private const string GuideWorkbenchKey = "String.Skills.GuideWorkbench";

    /// <summary>操作失败模板资源 key。</summary>
    private const string OperationFailedTemplateKey = "String.Skills.OperationFailedTemplate";

    /// <summary>请输入相对目录提示资源 key。</summary>
    private const string EnterRelativePathKey = "String.Skills.EnterRelativePath";

    /// <summary>必须是项目内相对目录提示资源 key。</summary>
    private const string MustBeRelativeKey = "String.Skills.MustBeRelative";

    /// <summary>包内未提供模板资源 key。</summary>
    private const string NotProvidedTemplateKey = "String.Skills.NotProvidedTemplate";

    /// <summary>安装摘要模板资源 key。</summary>
    private const string InstallSummaryTemplateKey = "String.Skills.InstallSummaryTemplate";
}
