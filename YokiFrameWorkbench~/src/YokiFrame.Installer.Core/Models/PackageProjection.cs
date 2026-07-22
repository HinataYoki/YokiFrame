namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述从同一 YokiFrame 包根生成的确定性文件投影。
/// </summary>
public sealed class PackageProjection
{
    /// <summary>
    /// 创建包投影。
    /// </summary>
    /// <param name="sourcePackageRoot">源包根绝对路径。</param>
    /// <param name="runtimeProfile">投影保留的 WorkbenchRuntime profile。</param>
    /// <param name="files">按相对路径稳定排序的投影文件。</param>
    public PackageProjection(
        string sourcePackageRoot,
        string runtimeProfile,
        IReadOnlyList<PackageProjectionFile> files)
    {
        SourcePackageRoot = sourcePackageRoot;
        RuntimeProfile = runtimeProfile;
        Files = files;
    }

    /// <summary>
    /// 获取源包根绝对路径。
    /// </summary>
    public string SourcePackageRoot { get; }

    /// <summary>
    /// 获取投影保留的 WorkbenchRuntime profile。
    /// </summary>
    public string RuntimeProfile { get; }

    /// <summary>
    /// 获取按相对路径稳定排序的投影文件。
    /// </summary>
    public IReadOnlyList<PackageProjectionFile> Files { get; }
}
