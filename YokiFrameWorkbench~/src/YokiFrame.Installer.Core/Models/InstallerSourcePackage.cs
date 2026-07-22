namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述待安装的 YokiFrame 源包根目录。
/// </summary>
public sealed class InstallerSourcePackage
{
    /// <summary>
    /// 创建源包描述。
    /// </summary>
    /// <param name="packageRoot">源包根目录。</param>
    public InstallerSourcePackage(string packageRoot)
    {
        PackageRoot = packageRoot;
    }

    /// <summary>
    /// 获取源包根目录。
    /// </summary>
    public string PackageRoot { get; }

}
