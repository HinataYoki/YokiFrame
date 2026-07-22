using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 解析当前 YokiFrame 源包根目录。
/// </summary>
public sealed class SourcePackageResolver
{
    /// <summary>
    /// 根据包根目录解析源码发布包，并验证可用于项目级 Runtime bootstrap 的 Workbench 输入存在。
    /// </summary>
    /// <param name="packageRoot">源包根目录。</param>
    /// <returns>源包信息。</returns>
    public InstallerSourcePackage Resolve(string packageRoot)
    {
        var fullPackageRoot = InstallerPathGuard.RequireFullPath(packageRoot, nameof(packageRoot));
        if (!Directory.Exists(fullPackageRoot))
        {
            throw new DirectoryNotFoundException("YokiFrame package root was not found: " + fullPackageRoot);
        }

        var documentationRoot = InstallerPathGuard.CombineInside(fullPackageRoot, "Documentation~");
        if (!Directory.Exists(documentationRoot))
        {
            throw new DirectoryNotFoundException("YokiFrame package root must contain Documentation~: " + documentationRoot);
        }

        var workbenchSourceRoot = InstallerPathGuard.CombineInside(fullPackageRoot, "YokiFrameWorkbench~", "src");
        if (!Directory.Exists(workbenchSourceRoot))
        {
            throw new DirectoryNotFoundException("YokiFrame package root must contain YokiFrameWorkbench~/src: " + workbenchSourceRoot);
        }

        return new InstallerSourcePackage(fullPackageRoot);
    }
}
