namespace YokiFrame.Installer.Core.IO;

/// <summary>
/// 拒绝把 YokiFrame 源目录所在项目选为安装目标，避免安装事务覆盖开发源码。
/// </summary>
public static class InstallerSourceTargetGuard
{
    /// <summary>
    /// 源目录与目标项目重叠时返回给 UI 和 CLI 的稳定说明。
    /// </summary>
    public const string OverlapMessage = "目标项目包含 YokiFrame 源目录，不能安装到开发项目自身。";

    /// <summary>
    /// 在任何安装写入前拒绝源目录与目标项目相同或互相包含。
    /// </summary>
    /// <param name="sourcePackageRoot">本地 YokiFrame 源目录；Git 模式为空时不检查。</param>
    /// <param name="targetProjectRoot">目标 Unity 或 Godot 项目根。</param>
    public static void RejectIfSourceOverlapsTarget(string? sourcePackageRoot, string? targetProjectRoot)
    {
        if (Overlaps(sourcePackageRoot, targetProjectRoot))
        {
            throw new InvalidOperationException(OverlapMessage);
        }
    }

    /// <summary>
    /// 判断两个目录是否指向同一位置，或其中一个位于另一个内部。
    /// </summary>
    /// <param name="left">第一个目录。</param>
    /// <param name="right">第二个目录。</param>
    /// <returns>路径相同或嵌套时返回 true；任一路径为空或无法规范化时返回 false。</returns>
    public static bool Overlaps(string? left, string? right)
    {
        if (!TryNormalize(left, out var fullLeft) || !TryNormalize(right, out var fullRight))
        {
            return false;
        }

        return Contains(fullLeft, fullRight) || Contains(fullRight, fullLeft);
    }

    /// <summary>
    /// 把目录规范为绝对路径；空白或非法路径不参与重叠判断。
    /// </summary>
    /// <param name="path">待规范化目录。</param>
    /// <param name="fullPath">成功时返回绝对路径。</param>
    /// <returns>路径可用时返回 true。</returns>
    private static bool TryNormalize(string? path, out string fullPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            fullPath = string.Empty;
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException)
        {
            fullPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            fullPath = string.Empty;
            return false;
        }
        catch (PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
        catch (IOException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// 判断子路径是否等于父路径，或位于父路径的下级目录。
    /// </summary>
    /// <param name="parent">可能包含另一路径的目录。</param>
    /// <param name="child">待比较的目录。</param>
    /// <returns>相同或是下级目录时返回 true。仅共享路径前缀的兄弟目录返回 false。</returns>
    private static bool Contains(string parent, string child)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedParent = Path.TrimEndingDirectorySeparator(parent);
        var normalizedChild = Path.TrimEndingDirectorySeparator(child);
        return normalizedChild.Equals(normalizedParent, comparison)
            || normalizedChild.StartsWith(normalizedParent + Path.DirectorySeparatorChar, comparison);
    }
}
