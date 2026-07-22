namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次包安装事务成功提交后的稳定结果。
/// </summary>
public sealed class PackageInstallTransactionResult
{
    /// <summary>
    /// 创建事务结果。
    /// </summary>
    /// <param name="targetPackageRoot">正式受管包根目录。</param>
    /// <param name="ownerManifestPath">成功提交的 owner manifest 路径。</param>
    /// <param name="replacedExistingPackage">是否替换了已有包目录。</param>
    public PackageInstallTransactionResult(
        string targetPackageRoot,
        string ownerManifestPath,
        bool replacedExistingPackage)
    {
        TargetPackageRoot = targetPackageRoot;
        OwnerManifestPath = ownerManifestPath;
        ReplacedExistingPackage = replacedExistingPackage;
    }

    /// <summary>
    /// 获取正式受管包根目录。
    /// </summary>
    public string TargetPackageRoot { get; }

    /// <summary>
    /// 获取成功提交的 owner manifest 路径。
    /// </summary>
    public string OwnerManifestPath { get; }

    /// <summary>
    /// 获取是否替换了已有包目录。
    /// </summary>
    public bool ReplacedExistingPackage { get; }
}
