using System.Diagnostics;
using System.Text;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 管理一个目标项目的跨进程 Installer 写事务锁。
/// </summary>
public static class InstallerProjectLock
{
    private const string INSTALLER_DIRECTORY_NAME = "installer";
    private const string LOCK_FILE_NAME = "project.lock";

    /// <summary>
    /// 在目标项目下取得独占锁；锁句柄由返回的 lease 持有。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <returns>持有独占文件句柄的锁租约。</returns>
    /// <exception cref="InstallerProjectBusyException">项目已被其他进程占用。</exception>
    public static InstallerProjectLockLease Acquire(string projectRoot)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        if (!Directory.Exists(fullProjectRoot))
        {
            throw new DirectoryNotFoundException("Target project root was not found: " + fullProjectRoot);
        }

        var installerRoot = InstallerPathGuard.CombineInside(
            fullProjectRoot,
            ".yokiframe",
            INSTALLER_DIRECTORY_NAME);
        Directory.CreateDirectory(installerRoot);
        var lockPath = InstallerPathGuard.CombineInside(installerRoot, LOCK_FILE_NAME);

        FileStream stream;
        try
        {
            stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough);
        }
        catch (IOException exception)
        {
            throw new InstallerProjectBusyException(fullProjectRoot, lockPath, exception);
        }

        try
        {
            WriteOwnerMetadata(stream);
            return new InstallerProjectLockLease(fullProjectRoot, lockPath, stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 将当前进程信息写入锁文件，供人工诊断使用；锁有效性仍由文件句柄决定。
    /// </summary>
    /// <param name="stream">已取得独占访问的锁文件流。</param>
    private static void WriteOwnerMetadata(FileStream stream)
    {
        var metadata = "pid=" + Environment.ProcessId + Environment.NewLine
            + "process=" + Process.GetCurrentProcess().ProcessName + Environment.NewLine
            + "acquiredUtc=" + DateTimeOffset.UtcNow.ToString("O") + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(metadata);
        stream.Position = 0;
        stream.SetLength(0);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }
}

/// <summary>
/// 持有 Installer 项目锁文件句柄的可释放租约。
/// </summary>
public sealed class InstallerProjectLockLease : IDisposable
{
    private FileStream? mStream;

    /// <summary>
    /// 创建锁租约。
    /// </summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="lockPath">锁文件路径。</param>
    /// <param name="stream">已独占打开的锁文件流。</param>
    internal InstallerProjectLockLease(string projectRoot, string lockPath, FileStream stream)
    {
        ProjectRoot = projectRoot;
        LockPath = lockPath;
        mStream = stream;
    }

    /// <summary>
    /// 获取锁所属的规范化项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取锁文件路径。
    /// </summary>
    public string LockPath { get; }

    /// <summary>
    /// 释放文件句柄；锁文件本身保留为诊断入口。
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref mStream, null)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
