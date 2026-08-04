namespace YokiFrame.RuntimeCache;

/// <summary>
/// 回收项目 Runtime 缓存中已不再由 `current.json` 引用的旧源码指纹目录。
/// </summary>
public static class RuntimeCachePruner
{
    /// <summary>
    /// 删除项目缓存根下除保留指纹外的其它合法指纹目录；被运行进程占用的目录留待下次重试。
    /// </summary>
    /// <param name="projectRoot">Unity 或 Godot 项目根。</param>
    /// <param name="retainedFingerprint">必须保留的当前源码指纹。</param>
    /// <returns>因文件占用或权限限制未能删除的目录。</returns>
    public static IReadOnlyList<string> PruneObsolete(
        string projectRoot,
        string retainedFingerprint)
    {
        var retainedRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(
            projectRoot,
            retainedFingerprint);
        var cacheRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetCacheRoot(projectRoot);
        if (!Directory.Exists(cacheRoot))
        {
            return Array.Empty<string>();
        }

        List<string> failures = new();
        foreach (var candidate in Directory.EnumerateDirectories(cacheRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsFingerprintDirectory(candidate)
                || RuntimeManifestPathPolicy.PathComparer.Equals(candidate, retainedRoot))
            {
                continue;
            }

            if (RuntimeCacheLease.IsInUse(candidate))
            {
                failures.Add(candidate);
                continue;
            }

            try
            {
                Directory.Delete(candidate, recursive: true);
            }
            catch (IOException)
            {
                failures.Add(candidate);
            }
            catch (UnauthorizedAccessException)
            {
                failures.Add(candidate);
            }
        }

        return failures;
    }

    /// <summary>
    /// 确认候选为缓存根直接子目录且名称是 64 位小写 SHA-256，避免触及 staging 或其它状态目录。
    /// </summary>
    /// <param name="path">候选目录完整路径。</param>
    /// <returns>只有合法指纹目录时返回 true。</returns>
    private static bool IsFingerprintDirectory(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (name.Length != 64)
        {
            return false;
        }

        foreach (var character in name)
        {
            var isDigit = character >= '0' && character <= '9';
            var isLowerHex = character >= 'a' && character <= 'f';
            if (!isDigit && !isLowerHex)
            {
                return false;
            }
        }

        return true;
    }
}
