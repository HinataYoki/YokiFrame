namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次 Godot .NET 安装计划和执行所需的完整输入。
/// </summary>
public sealed class GodotInstallRequest
{
    /// <summary>
    /// 创建 Godot 本地投影安装请求；路径、项目和所有权约束由计划阶段统一验证。
    /// </summary>
    /// <param name="sourcePackageRoot">YokiFrame 完整源包根。</param>
    /// <param name="projectRoot">目标 Godot 项目根。</param>
    /// <param name="runtimeProfile">安装前必须已在项目缓存生成的 Workbench Runtime profile。</param>
    /// <param name="repairProjectSettings">是否维护 project.godot 中的 YokiFrame owner 项。</param>
    /// <param name="enablePlugin">repair 开启时是否登记 YokiFrame editor plugin。</param>
    /// <param name="unmanagedPackagePolicy">无 owner manifest 旧包的接管策略。</param>
    public GodotInstallRequest(
        string sourcePackageRoot,
        string projectRoot,
        string runtimeProfile,
        bool repairProjectSettings,
        bool enablePlugin,
        UnmanagedPackagePolicy unmanagedPackagePolicy)
    {
        SourcePackageRoot = sourcePackageRoot;
        ProjectRoot = projectRoot;
        RuntimeProfile = runtimeProfile;
        RepairProjectSettings = repairProjectSettings;
        EnablePlugin = enablePlugin;
        UnmanagedPackagePolicy = unmanagedPackagePolicy;
    }

    /// <summary>获取 YokiFrame 完整源包根。</summary>
    public string SourcePackageRoot { get; }

    /// <summary>获取目标 Godot 项目根。</summary>
    public string ProjectRoot { get; }

    /// <summary>获取安装前必须已在项目缓存生成的 Runtime profile。</summary>
    public string RuntimeProfile { get; }

    /// <summary>获取是否维护 project.godot 中的 YokiFrame owner 项。</summary>
    public bool RepairProjectSettings { get; }

    /// <summary>获取 repair 开启时是否登记 YokiFrame editor plugin。</summary>
    public bool EnablePlugin { get; }

    /// <summary>获取无 owner manifest 旧包的接管策略。</summary>
    public UnmanagedPackagePolicy UnmanagedPackagePolicy { get; }
}
