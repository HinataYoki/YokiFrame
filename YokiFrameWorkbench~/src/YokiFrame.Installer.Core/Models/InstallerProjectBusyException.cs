namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示目标项目已经被另一个 Installer 写事务占用。
/// </summary>
public sealed class InstallerProjectBusyException : IOException
{
    /// <summary>
    /// 创建项目锁竞争异常。
    /// </summary>
    /// <param name="projectRoot">被占用的规范化项目根。</param>
    /// <param name="lockPath">项目锁文件路径。</param>
    /// <param name="innerException">底层文件共享异常。</param>
    public InstallerProjectBusyException(
        string projectRoot,
        string lockPath,
        Exception innerException)
        : base("Installer project is busy: " + projectRoot, innerException)
    {
        ProjectRoot = projectRoot;
        LockPath = lockPath;
    }

    /// <summary>
    /// 获取被占用的规范化项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取项目级锁文件路径。
    /// </summary>
    public string LockPath { get; }
}
