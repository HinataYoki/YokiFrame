namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次成功安装后由 Installer 拥有的相对文件集合。
/// </summary>
public sealed class PackageOwnerManifest
{
    /// <summary>
    /// 创建 owner manifest。
    /// </summary>
    /// <param name="schemaVersion">manifest schema 版本。</param>
    /// <param name="runtimeProfile">安装时保留的 Runtime profile。</param>
    /// <param name="files">按相对路径稳定排序的受管文件。</param>
    public PackageOwnerManifest(int schemaVersion, string runtimeProfile, IReadOnlyList<PackageOwnerFile> files)
    {
        SchemaVersion = schemaVersion;
        RuntimeProfile = runtimeProfile;
        Files = files;
    }

    /// <summary>
    /// 获取 manifest schema 版本。
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// 获取安装时保留的 Runtime profile。
    /// </summary>
    public string RuntimeProfile { get; }

    /// <summary>
    /// 获取按相对路径稳定排序的受管文件。
    /// </summary>
    public IReadOnlyList<PackageOwnerFile> Files { get; }
}
