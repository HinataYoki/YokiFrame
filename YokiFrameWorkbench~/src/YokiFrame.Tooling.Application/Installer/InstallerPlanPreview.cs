namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Application 可显示的统一安装动作类型。
/// </summary>
public enum InstallerPlanActionKind
{
    /// <summary>
    /// 安装或更新受管包投影。
    /// </summary>
    InstallPackage,

    /// <summary>
    /// 移除与目标来源冲突的现有包。
    /// </summary>
    RemovePackage,

    /// <summary>
    /// 设置 Unity manifest 中的 embedded package 本地 file 依赖。
    /// </summary>
    SetEmbeddedDependency,

    /// <summary>
    /// 设置 Unity manifest Git 依赖。
    /// </summary>
    SetGitDependency,

    /// <summary>
    /// patch Godot 主 C# 项目。
    /// </summary>
    PatchProjectFile,

    /// <summary>
    /// patch project.godot 中 YokiFrame 管理项。
    /// </summary>
    PatchProjectSettings
}

/// <summary>
/// 描述统一安装预览中的一个可审阅动作。
/// </summary>
public sealed class InstallerPlanActionPreview
{
    /// <summary>
    /// 创建安装动作预览。
    /// </summary>
    /// <param name="kind">统一动作类型。</param>
    /// <param name="targetPath">动作影响的目标路径。</param>
    /// <param name="value">动作可选目标值。</param>
    /// <param name="description">动作原因或语义说明。</param>
    public InstallerPlanActionPreview(
        InstallerPlanActionKind kind,
        string targetPath,
        string? value,
        string description)
    {
        Kind = kind;
        TargetPath = targetPath;
        Value = value;
        Description = description;
    }

    /// <summary>
    /// 获取统一动作类型。
    /// </summary>
    public InstallerPlanActionKind Kind { get; }

    /// <summary>
    /// 获取动作影响的目标路径。
    /// </summary>
    public string TargetPath { get; }

    /// <summary>
    /// 获取动作可选目标值。
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// 获取动作原因或语义说明。
    /// </summary>
    public string Description { get; }
}

/// <summary>
/// 描述 UI 与 CLI 可消费的统一安装计划预览。
/// </summary>
public sealed class InstallerPlanPreview
{
    /// <summary>
    /// 创建不携带 Core 执行令牌的安装预览，供 fake gateway 和只读消费者使用。
    /// </summary>
    /// <param name="engine">目标宿主。</param>
    /// <param name="mode">安装模式。</param>
    /// <param name="source">本地包根或 Git URL。</param>
    /// <param name="targetProjectRoot">目标项目根。</param>
    /// <param name="packageTarget">YokiFrame 包目标根。</param>
    /// <param name="actions">统一动作列表。</param>
    /// <param name="warnings">非终止警告。</param>
    public InstallerPlanPreview(
        InstallerTargetKind engine,
        InstallerInstallMode mode,
        string source,
        string targetProjectRoot,
        string packageTarget,
        IReadOnlyList<InstallerPlanActionPreview> actions,
        IReadOnlyList<string> warnings)
        : this(engine, mode, source, targetProjectRoot, packageTarget, actions, warnings, null)
    {
    }

    /// <summary>
    /// 创建由生产 gateway 生成且携带内部 typed plan 令牌的安装预览。
    /// </summary>
    /// <param name="engine">目标宿主。</param>
    /// <param name="mode">安装模式。</param>
    /// <param name="source">本地包根或 Git URL。</param>
    /// <param name="targetProjectRoot">目标项目根。</param>
    /// <param name="packageTarget">YokiFrame 包目标根。</param>
    /// <param name="actions">统一动作列表。</param>
    /// <param name="warnings">非终止警告。</param>
    /// <param name="executionToken">gateway 私有 typed plan 令牌。</param>
    internal InstallerPlanPreview(
        InstallerTargetKind engine,
        InstallerInstallMode mode,
        string source,
        string targetProjectRoot,
        string packageTarget,
        IReadOnlyList<InstallerPlanActionPreview> actions,
        IReadOnlyList<string> warnings,
        object? executionToken)
    {
        Engine = engine;
        Mode = mode;
        Source = source;
        TargetProjectRoot = targetProjectRoot;
        PackageTarget = packageTarget;
        Actions = actions.ToArray();
        Warnings = warnings.ToArray();
        ExecutionToken = executionToken;
    }

    /// <summary>
    /// 获取目标宿主。
    /// </summary>
    public InstallerTargetKind Engine { get; }

    /// <summary>
    /// 获取安装模式。
    /// </summary>
    public InstallerInstallMode Mode { get; }

    /// <summary>
    /// 获取本地包根或 Git URL。
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// 获取目标项目根。
    /// </summary>
    public string TargetProjectRoot { get; }

    /// <summary>
    /// 获取 YokiFrame 包目标根。
    /// </summary>
    public string PackageTarget { get; }

    /// <summary>
    /// 获取统一动作列表快照。
    /// </summary>
    public IReadOnlyList<InstallerPlanActionPreview> Actions { get; }

    /// <summary>
    /// 获取非终止警告快照。
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// 获取仅供生产 gateway 复核执行来源使用的内部 typed plan 令牌。
    /// </summary>
    internal object? ExecutionToken { get; }
}
