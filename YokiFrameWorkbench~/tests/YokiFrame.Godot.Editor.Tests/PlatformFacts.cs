using Xunit;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// 仅在 Windows 上执行。xUnit 2 不能在测试体内动态 Skip，发现阶段设置 Skip，避免非 Windows 直接 return 被记为通过。
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <summary>
    /// 当前进程不是 Windows 时跳过该用例。
    /// </summary>
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "当前平台不提供 Windows 文件锁或命名内存映射。";
        }
    }
}

/// <summary>
/// 仅在 Linux 或 macOS 上执行 Unix Domain Socket 用例。
/// </summary>
public sealed class UnixFactAttribute : FactAttribute
{
    /// <summary>
    /// 当前进程不是 Linux 或 macOS 时跳过该用例。
    /// </summary>
    public UnixFactAttribute()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Skip = "当前平台不提供 Unix Domain Socket FastChannel。";
        }
    }
}

/// <summary>
/// 仅在当前进程能够创建符号链接时执行 reparse point 用例。
/// </summary>
public sealed class SymbolicLinkFactAttribute : FactAttribute
{
    private static readonly bool sCanCreateFileLink = Probe(SymbolicLinkProbeKind.File);
    private static readonly bool sCanCreateDirectoryLink = Probe(SymbolicLinkProbeKind.Directory);

    /// <summary>
    /// 按文件或目录链接能力决定是否跳过。
    /// </summary>
    /// <param name="kind">用例实际创建的链接类型。</param>
    public SymbolicLinkFactAttribute(SymbolicLinkProbeKind kind)
    {
        var supported = kind == SymbolicLinkProbeKind.File
            ? sCanCreateFileLink
            : sCanCreateDirectoryLink;
        if (!supported)
        {
            Skip = "当前进程不能创建符号链接，无法验证 reparse point 拒绝。";
        }
    }

    /// <summary>
    /// 在临时目录探测一次符号链接能力，不把探测失败当成用例通过。
    /// </summary>
    /// <param name="kind">要探测的链接类型。</param>
    /// <returns>当前进程可以创建该类型链接时返回 true。</returns>
    private static bool Probe(SymbolicLinkProbeKind kind)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-symlink-capability-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "link");
        try
        {
            Directory.CreateDirectory(root);
            if (kind == SymbolicLinkProbeKind.File)
            {
                File.WriteAllBytes(target, Array.Empty<byte>());
                File.CreateSymbolicLink(link, target);
            }
            else
            {
                Directory.CreateDirectory(target);
                Directory.CreateSymbolicLink(link, target);
            }

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                          or PlatformNotSupportedException
                                          or NotSupportedException
                                          or IOException)
        {
            return false;
        }
        finally
        {
            DeleteLinkOnly(link, kind);
            DeleteIfExists(target, recursive: true);
            DeleteIfExists(root, recursive: true);
        }
    }

    /// <summary>
    /// 只删除探测创建的链接本身，不跟随到链接目标。
    /// </summary>
    /// <param name="linkPath">符号链接路径。</param>
    /// <param name="kind">链接类型。</param>
    private static void DeleteLinkOnly(string linkPath, SymbolicLinkProbeKind kind)
    {
        if (kind == SymbolicLinkProbeKind.File)
        {
            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }

            return;
        }

        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath, recursive: false);
        }
    }

    /// <summary>
    /// 删除探测路径。符号链接只解除链接，避免递归删除跟随到目标目录。
    /// </summary>
    /// <param name="path">待删除路径。</param>
    /// <param name="recursive">普通目录是否递归删除。</param>
    private static void DeleteIfExists(string path, bool recursive)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.Delete(path, recursive: false);
                return;
            }

            File.Delete(path);
            return;
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            Directory.Delete(path, recursive);
            return;
        }

        File.Delete(path);
    }
}

/// <summary>
/// 符号链接探测类型，与用例里实际创建的链接种类一致。
/// </summary>
public enum SymbolicLinkProbeKind
{
    /// <summary>文件符号链接。</summary>
    File,

    /// <summary>目录符号链接。</summary>
    Directory
}

