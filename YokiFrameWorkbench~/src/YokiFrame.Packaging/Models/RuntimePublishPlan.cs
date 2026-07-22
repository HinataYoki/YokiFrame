namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述从独立 YokiFrame 包根发布当前平台项目 Runtime 缓存的路径计划。
/// </summary>
public sealed class RuntimePublishPlan
{
    /// <summary>
    /// 创建当前平台发布计划。
    /// </summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <param name="configuration">构建配置。</param>
    /// <param name="profile">当前平台 profile。</param>
    /// <param name="workbenchRoot">工具链源码根。</param>
    /// <param name="guiProjectPath">Workbench GUI 项目路径。</param>
    /// <param name="cliProjectPath">CLI 项目路径。</param>
    /// <param name="runtimeRoot">项目级 Runtime 缓存根。</param>
    /// <param name="stagingRoot">当前平台 staging 目录。</param>
    /// <param name="publishRoot">当前平台正式发布目录。</param>
    /// <param name="manifestPath">工具 manifest 路径。</param>
    internal RuntimePublishPlan(
        string packageRoot,
        string configuration,
        RuntimePublishProfile profile,
        string workbenchRoot,
        string guiProjectPath,
        string cliProjectPath,
        string runtimeRoot,
        string stagingRoot,
        string publishRoot,
        string manifestPath)
    {
        PackageRoot = packageRoot;
        Configuration = configuration;
        Profile = profile;
        WorkbenchRoot = workbenchRoot;
        GuiProjectPath = guiProjectPath;
        CliProjectPath = cliProjectPath;
        RuntimeRoot = runtimeRoot;
        StagingRoot = stagingRoot;
        PublishRoot = publishRoot;
        ManifestPath = manifestPath;
    }

    /// <summary>
    /// 获取 YokiFrame 包根。
    /// </summary>
    public string PackageRoot { get; }

    /// <summary>
    /// 获取构建配置。
    /// </summary>
    public string Configuration { get; }

    /// <summary>
    /// 获取当前平台发布 profile。
    /// </summary>
    public RuntimePublishProfile Profile { get; }

    /// <summary>
    /// 获取工具链源码根。
    /// </summary>
    public string WorkbenchRoot { get; }

    /// <summary>
    /// 获取 Workbench GUI 项目路径。
    /// </summary>
    public string GuiProjectPath { get; }

    /// <summary>
    /// 获取 CLI 项目路径。
    /// </summary>
    public string CliProjectPath { get; }

    /// <summary>
    /// 获取项目级 Runtime 缓存根。
    /// </summary>
    public string RuntimeRoot { get; }

    /// <summary>
    /// 获取当前平台 staging 目录。
    /// </summary>
    public string StagingRoot { get; }

    /// <summary>
    /// 获取当前平台正式发布目录。
    /// </summary>
    public string PublishRoot { get; }

    /// <summary>
    /// 获取工具 manifest 路径。
    /// </summary>
    public string ManifestPath { get; }
}
