namespace YokiFrame.Packaging.Services;

/// <summary>
/// 为项目 Runtime 包根提供跨进程独占发布锁，覆盖所有 fingerprint 与共享 current.json。
/// </summary>
internal static class RuntimePublishLock
{
    private const string LOCK_FILE_NAME = ".publish.lock";

    /// <summary>
    /// 获取项目 Runtime 包根级独占锁；锁被占用时立即失败，避免不同 fingerprint 竞争共享指针。
    /// </summary>
    /// <param name="runtimeCacheRoot">`.yokiframe/runtime/com.hinatayoki.yokiframe` 包级缓存根。</param>
    /// <returns>持有锁的文件流；调用方必须在整个发布过程结束后释放。</returns>
    internal static FileStream Acquire(string runtimeCacheRoot)
    {
        var fullCacheRoot = Path.GetFullPath(runtimeCacheRoot);
        Directory.CreateDirectory(fullCacheRoot);
        var lockPath = Path.Combine(fullCacheRoot, LOCK_FILE_NAME);
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new IOException("Another WorkbenchRuntime publish is already using this project Runtime cache.", exception);
        }
    }

    /// <summary>
    /// 从 fingerprint Runtime 根解析包级缓存根并取得同一发布锁，供直接 RuntimePublishService 调用使用。
    /// </summary>
    /// <param name="runtimeRoot">单个 sourceFingerprint 的 Runtime 根。</param>
    /// <returns>持有包级发布锁的文件流。</returns>
    internal static FileStream AcquireForRuntimeRoot(string runtimeRoot)
    {
        var fullRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var runtimeCacheRoot = Directory.GetParent(fullRuntimeRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Project Runtime cache root could not be resolved: " + fullRuntimeRoot);
        return Acquire(runtimeCacheRoot);
    }
}
