using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 从最终 Godot 包投影选择 C# 与 GDScript，并规划 Installer 拥有的 UID sidecar。
/// </summary>
public sealed class GodotUidProjectionBuilder
{
    private const string PACKAGE_RESOURCE_ROOT = "res://addons/yokiframe/package/YokiFrame";
    private readonly GodotUidSidecarBuilder mSidecarBuilder = new();

    /// <summary>
    /// 只为基础投影中的 .cs 与 .gd 规划 sidecar，并按相对路径稳定排序。
    /// </summary>
    /// <param name="projection">已过滤的 Godot 基础包投影。</param>
    /// <param name="targetPackageRoot">正式目标包根，用于读取既有 sidecar。</param>
    /// <returns>待加入最终投影和 owner manifest 的 UID sidecar。</returns>
    public IReadOnlyList<GodotUidSidecar> Build(PackageProjection projection, string targetPackageRoot)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var fullTargetRoot = InstallerPathGuard.RequireFullPath(targetPackageRoot, nameof(targetPackageRoot));
        List<GodotUidSidecar> sidecars = new();
        foreach (var file in projection.Files.Where(static file => IsScriptResource(file.RelativePath)))
        {
            var relativePath = NormalizeRelativePath(file.RelativePath);
            var sidecarRelativePath = relativePath + ".uid";
            var existingPath = InstallerPathGuard.CombineInside(
                fullTargetRoot,
                sidecarRelativePath.Replace('/', Path.DirectorySeparatorChar));
            sidecars.Add(mSidecarBuilder.Build(
                sidecarRelativePath,
                PACKAGE_RESOURCE_ROOT + "/" + relativePath,
                existingPath));
        }

        sidecars.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return sidecars;
    }

    /// <summary>
    /// 判断投影文件是否是 Godot 需要 UID sidecar 的 C# 或 GDScript 资源。
    /// </summary>
    /// <param name="relativePath">包相对路径。</param>
    /// <returns>扩展名为 .cs 或 .gd 时返回 true。</returns>
    private static bool IsScriptResource(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".gd", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 统一资源路径分隔符，保证 res 路径和 UID 哈希跨平台稳定。
    /// </summary>
    /// <param name="relativePath">平台相关包相对路径。</param>
    /// <returns>使用正斜杠的相对路径。</returns>
    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
