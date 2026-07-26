using System.Security.Cryptography;
using System.Text;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>承载 LocalizationKit standalone JSON 的路径边界、跨进程写锁和原子提交实现。</summary>
public sealed partial class LocalizationKitApplicationService
{
    private const string SOURCE_WRITE_MUTEX_PREFIX = "YokiFrame.LocalizationKit.Source.";

    /// <summary>解析并校验项目根内的路径，拒绝父目录遍历以及 symlink、junction 等重解析点。</summary>
    /// <param name="projectRoot">当前引擎项目根。</param>
    /// <param name="path">绝对或项目根相对路径。</param>
    /// <returns>经过词法边界和文件系统边界校验的绝对路径。</returns>
    private static string ResolveContainedPath(string projectRoot, string path)
    {
        return ResolveContainedPath(projectRoot, path, "LocalizationKit");
    }

    /// <summary>按调用方语义解析项目内路径，并检查根目录到目标之间所有已存在的重解析点。</summary>
    /// <param name="projectRoot">当前引擎项目根。</param>
    /// <param name="path">绝对或项目根相对路径。</param>
    /// <param name="description">错误信息使用的路径语义。</param>
    /// <returns>经过词法边界和文件系统边界校验的绝对路径。</returns>
    private static string ResolveContainedPath(string projectRoot, string path, string description)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("项目根不能为空。", nameof(projectRoot));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(description + " 路径不能为空。", nameof(path));

        string root = Path.GetFullPath(projectRoot);
        string fullPath = Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(root, path));
        string relativePath = Path.GetRelativePath(root, fullPath);
        if (IsOutsideRoot(relativePath))
        {
            throw new InvalidDataException(description + " 路径越出项目根。");
        }

        EnsureNoReparsePoint(root, relativePath, description);
        return fullPath;
    }

    /// <summary>判断 Path.GetRelativePath 的结果是否表示目标位于根目录之外。</summary>
    /// <param name="relativePath">相对于受控根目录的路径。</param>
    /// <returns>路径逃逸或仍为完整限定路径时返回 true。</returns>
    private static bool IsOutsideRoot(string relativePath)
    {
        return relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relativePath);
    }

    /// <summary>检查受控根自身和目标沿途已存在节点，拒绝通过目录链接把 IO 重定向到项目外。</summary>
    /// <param name="root">规范化后的受控根目录。</param>
    /// <param name="relativePath">已确认位于根目录内的相对路径。</param>
    /// <param name="description">错误信息使用的路径语义。</param>
    private static void EnsureNoReparsePoint(string root, string relativePath, string description)
    {
        string current = root;
        if (TryGetAttributes(current, out FileAttributes attributes)
            && attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException(description + " 路径包含重解析点: " + current);
        }

        foreach (string segment in relativePath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            current = Path.Combine(current, segment);
            if (!TryGetAttributes(current, out attributes)) break;
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException(description + " 路径包含重解析点: " + current);
            }
        }
    }

    /// <summary>尝试读取文件或目录属性；路径尚未创建时允许调用方停止检查后续节点。</summary>
    /// <param name="path">待检查的绝对路径。</param>
    /// <param name="attributes">节点存在时返回其文件系统属性。</param>
    /// <returns>节点存在并成功读取属性时返回 true。</returns>
    private static bool TryGetAttributes(string path, out FileAttributes attributes)
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
    }

    /// <summary>创建不泄露绝对路径、且在 Windows 上忽略路径大小写差异的稳定跨进程 Mutex 名称。</summary>
    /// <param name="sourcePath">LocalizationKit JSON 源文件绝对路径。</param>
    /// <returns>绑定该源文件的命名 Mutex 名称。</returns>
    internal static string CreateSourceWriteMutexName(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("源文件路径不能为空。", nameof(sourcePath));

        string normalizedPath = Path.GetFullPath(sourcePath);
        if (OperatingSystem.IsWindows()) normalizedPath = normalizedPath.ToUpperInvariant();
        byte[] bytes = Encoding.UTF8.GetBytes(normalizedPath);
        return SOURCE_WRITE_MUTEX_PREFIX + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>等待当前源文件的跨进程独占写锁，并把进程异常退出留下的 abandoned Mutex 视为已取得。</summary>
    /// <param name="sourcePath">已规范化的 JSON 源文件路径。</param>
    /// <param name="cancellationToken">取消令牌；超时时抛出 OperationCanceledException。</param>
    /// <returns>负责释放 Mutex 所有权和句柄的租约。</returns>
    private static SourceWriteLock AcquireSourceWriteLock(string sourcePath, CancellationToken cancellationToken = default)
    {
        Mutex mutex = new(false, CreateSourceWriteMutexName(sourcePath));
        try
        {
            try
            {
                while (!mutex.WaitOne(TimeSpan.FromMilliseconds(250)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (AbandonedMutexException)
            {
                // 异常表示当前线程已经取得所有权，继续在锁内重读磁盘最新内容。
            }

            return new SourceWriteLock(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    /// <summary>使用同目录临时文件、持久化刷新和原子替换提交 JSON，避免中断破坏旧文件。</summary>
    /// <param name="path">已通过路径守卫校验的目标文件。</param>
    /// <param name="content">已经通过完整 schema 复核的 JSON 文本。</param>
    private static void WriteAtomically(string path, string content)
    {
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(path)) File.Replace(temporaryPath, path, null); else File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    /// <summary>持有单个源文件的命名 Mutex，并确保释放动作幂等。</summary>
    private sealed class SourceWriteLock : IDisposable
    {
        private readonly Mutex mMutex;
        private int mDisposed;

        /// <summary>记录已经由当前线程取得所有权的 Mutex。</summary>
        /// <param name="mutex">当前源文件对应的命名 Mutex。</param>
        internal SourceWriteLock(Mutex mutex)
        {
            mMutex = mutex;
        }

        /// <summary>释放 Mutex 所有权和操作系统句柄；重复调用不会再次释放。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref mDisposed, 1) != 0) return;
            try
            {
                mMutex.ReleaseMutex();
            }
            finally
            {
                mMutex.Dispose();
            }
        }
    }
}
