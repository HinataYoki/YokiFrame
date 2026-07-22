namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次 Unity 安装计划和执行所需的完整输入。
/// </summary>
public sealed class UnityInstallRequest
{
    /// <summary>
    /// 创建 Unity 安装请求；具体路径、版本、URL 和来源约束由计划阶段统一验证。
    /// </summary>
    /// <param name="sourcePackageRoot">embedded 模式使用的 YokiFrame 源包根；Git 模式允许为空。</param>
    /// <param name="projectRoot">目标 Unity 项目根目录。</param>
    /// <param name="runtimeProfile">embedded 投影保留的 WorkbenchRuntime profile。</param>
    /// <param name="mode">目标安装来源。</param>
    /// <param name="gitUrl">Git 模式写入 manifest 的 URL。</param>
    /// <param name="unmanagedPackagePolicy">已有 legacy embedded 包的接管策略。</param>
    public UnityInstallRequest(
        string sourcePackageRoot,
        string projectRoot,
        string runtimeProfile,
        UnityInstallMode mode,
        string? gitUrl,
        UnmanagedPackagePolicy unmanagedPackagePolicy)
    {
        SourcePackageRoot = sourcePackageRoot;
        ProjectRoot = projectRoot;
        RuntimeProfile = runtimeProfile;
        Mode = mode;
        GitUrl = gitUrl;
        UnmanagedPackagePolicy = unmanagedPackagePolicy;
    }

    /// <summary>
    /// 获取 embedded 模式使用的源包根。
    /// </summary>
    public string SourcePackageRoot { get; }

    /// <summary>
    /// 获取目标 Unity 项目根目录。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取 embedded 投影保留的 Runtime profile。
    /// </summary>
    public string RuntimeProfile { get; }

    /// <summary>
    /// 获取目标安装来源。
    /// </summary>
    public UnityInstallMode Mode { get; }

    /// <summary>
    /// 获取 Git 模式写入 manifest 的 URL。
    /// </summary>
    public string? GitUrl { get; }

    /// <summary>
    /// 获取已有 legacy embedded 包的接管策略。
    /// </summary>
    public UnmanagedPackagePolicy UnmanagedPackagePolicy { get; }
}
