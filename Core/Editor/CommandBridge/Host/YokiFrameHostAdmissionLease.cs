#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>描述 Host admission 尝试的结果。</summary>
    internal enum YokiFrameHostAdmissionResult
    {
        /// <summary>当前进程已取得 admission lease。</summary>
        Acquired,
        /// <summary>另一个进程当前持有 admission lease。</summary>
        AlreadyOwned,
        /// <summary>由于存储错误无法创建锁。</summary>
        StorageError
    }

    /// <summary>为单个 Host 实例提供由操作系统支持、按项目作用域隔离的 admission lease。</summary>
    internal sealed class YokiFrameHostAdmissionLease : IDisposable
    {
        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_LOCK_VIOLATION = 33;
        private FileStream mStream;

        private YokiFrameHostAdmissionLease(FileStream stream, string lockPath)
        {
            mStream = stream;
            LockPath = lockPath;
        }

        /// <summary>获取此 lease 使用的锁路径。</summary>
        public string LockPath { get; }

        /// <summary>尝试取得独占文件句柄。</summary>
        public static YokiFrameHostAdmissionResult TryAcquire(
            string lockPath,
            out YokiFrameHostAdmissionLease lease,
            out Exception storageException)
        {
            lease = null;
            storageException = null;
            if (string.IsNullOrWhiteSpace(lockPath))
            {
                storageException = new ArgumentException("Host admission lock path is required.", nameof(lockPath));
                return YokiFrameHostAdmissionResult.StorageError;
            }

            try
            {
                var directoryPath = Path.GetDirectoryName(lockPath);
                if (string.IsNullOrEmpty(directoryPath))
                {
                    storageException = new DirectoryNotFoundException("Host admission lock path has no directory.");
                    return YokiFrameHostAdmissionResult.StorageError;
                }

                Directory.CreateDirectory(directoryPath);
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                lease = new YokiFrameHostAdmissionLease(stream, lockPath);
                return YokiFrameHostAdmissionResult.Acquired;
            }
            catch (IOException exception)
            {
                if (IsSharingViolation(exception))
                {
                    return YokiFrameHostAdmissionResult.AlreadyOwned;
                }

                storageException = exception;
                return YokiFrameHostAdmissionResult.StorageError;
            }
            catch (UnauthorizedAccessException exception)
            {
                storageException = exception;
                return YokiFrameHostAdmissionResult.StorageError;
            }
        }

        /// <summary>
        /// 只把操作系统明确报告的共享冲突归类为已有 Host，避免把磁盘或权限故障伪装成竞争。
        /// </summary>
        /// <param name="exception">FileStream 打开失败异常。</param>
        /// <returns>确认为共享冲突时返回 true。</returns>
        private static bool IsSharingViolation(IOException exception)
        {
            var nativeError = exception.HResult & 0xFFFF;
            return nativeError == ERROR_SHARING_VIOLATION || nativeError == ERROR_LOCK_VIOLATION;
        }

        /// <summary>释放操作系统文件句柄。</summary>
        public void Dispose()
        {
            var stream = mStream;
            mStream = null;
            if (stream != null)
            {
                stream.Dispose();
            }
        }
    }

    /// <summary>表示同一项目与 engineId 已由其它 Host 持有。</summary>
    internal sealed class YokiFrameHostAlreadyOwnedException : InvalidOperationException
    {
        /// <summary>创建稳定 HostAlreadyOwned 错误。</summary>
        /// <param name="engineId">发生冲突的 engineId。</param>
        public YokiFrameHostAlreadyOwnedException(string engineId)
            : base("HostAlreadyOwned: " + engineId)
        {
        }
    }
}
#endif
