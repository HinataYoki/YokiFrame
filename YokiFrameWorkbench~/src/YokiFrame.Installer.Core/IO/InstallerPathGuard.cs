namespace YokiFrame.Installer.Core.IO;

/// <summary>
/// 提供安装器路径规范化和项目内路径组合能力。
/// </summary>
internal static class InstallerPathGuard
{
    /// <summary>
    /// 规范化根目录为绝对路径。
    /// </summary>
    /// <param name="path">输入路径。</param>
    /// <param name="parameterName">参数名。</param>
    /// <returns>绝对路径。</returns>
    public static string RequireFullPath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    /// <summary>
    /// 在根目录内组合路径，并阻止相对片段逃逸。
    /// </summary>
    /// <param name="root">根目录。</param>
    /// <param name="segments">相对路径片段。</param>
    /// <returns>组合后的绝对路径。</returns>
    public static string CombineInside(string root, params string[] segments)
    {
        var fullRoot = Path.GetFullPath(root);
        string combined;
        if (segments.Length == 0)
        {
            combined = fullRoot;
        }
        else
        {
            var parts = new string[segments.Length + 1];
            parts[0] = fullRoot;
            segments.CopyTo(parts, 1);
            combined = Path.Combine(parts);
        }
        var fullPath = Path.GetFullPath(combined);
        if (!IsInside(fullRoot, fullPath))
        {
            throw new IOException("Installer path escaped the expected root.");
        }

        EnsureNoReparsePoint(fullRoot, fullPath);
        return fullPath;
    }

    /// <summary>
    /// 判断目标路径是否位于根目录内。
    /// </summary>
    /// <param name="root">根目录。</param>
    /// <param name="path">目标路径。</param>
    /// <returns>位于根目录内时返回 true。</returns>
    private static bool IsInside(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.Equals(normalizedRoot, comparison)
            || path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison)
            || path.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// 拒绝根目录以下已经存在的 symlink 或 junction，避免后续安装事务通过重解析点写出项目边界。
    /// </summary>
    /// <param name="root">调用方信任的项目或包根。</param>
    /// <param name="path">已通过词法包含校验的目标路径。</param>
    private static void EnsureNoReparsePoint(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return;
        }

        var currentPath = Path.GetFullPath(root);
        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!TryReadAttributes(currentPath, out var attributes))
            {
                return;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Installer target path contains a symbolic link or junction: " + currentPath);
            }
        }
    }

    /// <summary>
    /// 读取已存在路径的属性；遇到首个尚未创建的层级时通知调用方停止向下扫描。
    /// </summary>
    /// <param name="path">候选文件或目录路径。</param>
    /// <param name="attributes">路径存在时返回属性。</param>
    /// <returns>路径存在并成功读取属性时返回 true。</returns>
    private static bool TryReadAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            attributes = default;
            return false;
        }
    }
}
