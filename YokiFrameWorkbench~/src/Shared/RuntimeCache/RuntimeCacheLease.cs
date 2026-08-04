namespace YokiFrame.RuntimeCache;

/// <summary>
/// 持有当前 Runtime fingerprint 的进程级文件 lease，防止后台清理删除正在运行的缓存目录。
/// </summary>
public sealed class RuntimeCacheLease : IDisposable
{
    internal const string LEASE_FILE_NAME = ".runtime.lease";
    private readonly FileStream mStream;
    private bool mDisposed;

    private RuntimeCacheLease(FileStream stream)
    {
        mStream = stream;
    }

    /// <summary>
    /// 尝试为指定项目的当前源码指纹取得 lease；目录不存在或已被其它进程占用时返回空。
    /// </summary>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <param name="sourceFingerprint">当前 Runtime 源码指纹。</param>
    /// <returns>持有 lease 的对象；不可用时为空。</returns>
    public static RuntimeCacheLease? TryAcquire(string projectRoot, string sourceFingerprint)
    {
        if (string.IsNullOrWhiteSpace(sourceFingerprint))
        {
            return null;
        }

        try
        {
            var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, sourceFingerprint);
            if (!Directory.Exists(runtimeRoot))
            {
                return null;
            }

            var leasePath = Path.Combine(runtimeRoot, LEASE_FILE_NAME);
            var stream = new FileStream(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                GetLeaseShareMode(),
                bufferSize: 1,
                FileOptions.SequentialScan);
            try
            {
                stream.SetLength(1);
                AcquireLeaseLock(stream);
                stream.Flush(flushToDisk: true);
                return new RuntimeCacheLease(stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 判断 Runtime fingerprint 是否存在一个无法立即取得的活动 lease。
    /// </summary>
    /// <param name="runtimeRoot">待清理的 fingerprint 根目录。</param>
    /// <returns>lease 被其它进程持有时返回 true。</returns>
    internal static bool IsInUse(string runtimeRoot)
    {
        var leasePath = Path.Combine(runtimeRoot, LEASE_FILE_NAME);
        if (!File.Exists(leasePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                leasePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                GetProbeShareMode(),
                bufferSize: 1,
                FileOptions.SequentialScan);
            ProbeLeaseLock(stream);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// 释放文件锁；lease 文件留在 fingerprint 根中，下一次清理会一并删除。
    /// </summary>
    public void Dispose()
    {
        if (mDisposed)
        {
            return;
        }

        mDisposed = true;
        try
        {
            ReleaseLeaseLock(mStream);
        }
        catch (IOException)
        {
            // 进程退出或底层文件系统已释放句柄时无需再次传播释放异常。
        }
        finally
        {
            mStream.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 选择当前平台的 lease 文件共享模式；macOS 不支持 FileStream.Lock，使用独占打开模拟同等语义。
    /// </summary>
    /// <returns>持有 lease 时使用的共享模式。</returns>
    private static FileShare GetLeaseShareMode()
    {
        return OperatingSystem.IsMacOS() ? FileShare.None : FileShare.Read;
    }

    /// <summary>
    /// 选择探测 lease 是否占用时的共享模式。
    /// </summary>
    /// <returns>探测句柄使用的共享模式。</returns>
    private static FileShare GetProbeShareMode()
    {
        return OperatingSystem.IsMacOS() ? FileShare.None : FileShare.None;
    }

    /// <summary>
    /// 在支持字节范围锁的平台取得独占锁；macOS 由独占文件打开完成占用判定。
    /// </summary>
    /// <param name="stream">lease 文件流。</param>
    private static void AcquireLeaseLock(FileStream stream)
    {
        if (!OperatingSystem.IsMacOS())
        {
            stream.Lock(0, 1);
        }
    }

    /// <summary>
    /// 检查 lease 文件是否被其它进程占用。
    /// </summary>
    /// <param name="stream">探测文件流。</param>
    private static void ProbeLeaseLock(FileStream stream)
    {
        if (!OperatingSystem.IsMacOS())
        {
            stream.Lock(0, 1);
            stream.Unlock(0, 1);
        }
    }

    /// <summary>
    /// 释放支持字节范围锁的平台上的 lease 锁。
    /// </summary>
    /// <param name="stream">lease 文件流。</param>
    private static void ReleaseLeaseLock(FileStream stream)
    {
        if (!OperatingSystem.IsMacOS())
        {
            stream.Unlock(0, 1);
        }
    }
}
