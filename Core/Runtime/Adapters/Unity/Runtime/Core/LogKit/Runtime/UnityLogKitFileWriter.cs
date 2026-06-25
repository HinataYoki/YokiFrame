#if !GODOT
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 日志文件写入器：捕获全局 Unity 日志，后台批量写入，避免日志热路径同步磁盘 I/O。
    /// </summary>
    public static partial class UnityLogKitFileWriter
    {
        private const int BATCH_LIMIT = 500;
        private const int TAKE_TIMEOUT_MS = 100;

        private static readonly object sLock = new();
        private static readonly object sFilterLock = new();

        private static BlockingCollection<LogCommand> sQueue;
        private static CancellationTokenSource sCancellation;
        private static Task sWriteTask;
        private static UnityLogKitOptions sOptions;
        private static string sFilePath;
        private static bool sInitialized;
        private static string sLastLogString;
        private static int sSameLogCounter;
        private static int sPendingWriteCount;
        private static int sDroppedCount;

        /// <summary>
        /// 获取日志文件写入器是否已经初始化。
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (sLock)
                    return sInitialized;
            }
        }

        /// <summary>
        /// 获取当前日志文件路径。
        /// </summary>
        public static string CurrentLogFilePath
        {
            get
            {
                lock (sLock)
                    return sFilePath;
            }
        }

        /// <summary>
        /// 获取因队列满或写入器关闭而丢弃的日志数量。
        /// </summary>
        public static int DroppedCount
        {
            get { return Volatile.Read(ref sDroppedCount); }
        }

        /// <summary>
        /// 初始化日志文件写入器。
        /// </summary>
        /// <param name="options">Unity LogKit 写入配置。</param>
        public static void Initialize(UnityLogKitOptions options)
        {
            var normalized = options != null ? options.Clone() : UnityLogKitOptions.CreateDefault();
            normalized.Normalize();
            if (!normalized.ShouldSaveForCurrentRuntime())
            {
                Shutdown();
                return;
            }

            Shutdown();

            lock (sLock)
            {
                sOptions = normalized;
                sFilePath = normalized.ResolveLogFilePath();
                sQueue = new(normalized.MaxQueueSize);
                sCancellation = new();
                sSharedBuffer = new byte[INITIAL_BUFFER_SIZE];
                sLastLogString = null;
                sSameLogCounter = 0;
                sPendingWriteCount = 0;
                sDroppedCount = 0;

                RepairLastLine(sFilePath);
                CleanUpLogFile(sFilePath, normalized, DateTime.Now);

                Application.logMessageReceivedThreaded -= HandleUnityLog;
                Application.logMessageReceivedThreaded += HandleUnityLog;
                Application.quitting -= Shutdown;
                Application.quitting += Shutdown;

                sInitialized = true;
                sWriteTask = Task.Factory.StartNew(
                    ProcessQueue,
                    sCancellation.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
        }

        /// <summary>
        /// 停止日志文件写入器并释放后台资源。
        /// </summary>
        public static void Shutdown()
        {
            BlockingCollection<LogCommand> queue;
            CancellationTokenSource cancellation;
            Task writeTask;

            lock (sLock)
            {
                if (!sInitialized && sQueue == null)
                    return;

                Application.logMessageReceivedThreaded -= HandleUnityLog;
                Application.quitting -= Shutdown;
                sInitialized = false;

                queue = sQueue;
                cancellation = sCancellation;
                writeTask = sWriteTask;
            }

            Flush(TimeSpan.FromMilliseconds(500));

            if (queue != null)
                queue.CompleteAdding();
            if (cancellation != null)
                cancellation.Cancel();

            try
            {
                if (writeTask != null)
                    writeTask.Wait(500);
            }
            catch
            {
            }

            if (queue != null)
                queue.Dispose();
            if (cancellation != null)
                cancellation.Dispose();

            lock (sLock)
            {
                if (ReferenceEquals(sQueue, queue))
                    sQueue = null;
                if (ReferenceEquals(sCancellation, cancellation))
                    sCancellation = null;
                if (ReferenceEquals(sWriteTask, writeTask))
                    sWriteTask = null;
                sOptions = null;
                sFilePath = null;
                sSharedBuffer = null;
                sLastLogString = null;
                sSameLogCounter = 0;
                sPendingWriteCount = 0;
            }
        }

        /// <summary>
        /// 等待当前队列中的日志写入完成。
        /// </summary>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>在超时前写入完成时返回 true。</returns>
        public static bool Flush(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (Volatile.Read(ref sPendingWriteCount) > 0)
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                Thread.Sleep(5);
            }

            return true;
        }

        /// <summary>
        /// 入队一条日志写入命令。
        /// </summary>
        /// <param name="command">日志写入命令。</param>
        public static void Enqueue(LogCommand command)
        {
            BlockingCollection<LogCommand> queue;
            lock (sLock)
            {
                if (!sInitialized || sQueue == null || sQueue.IsAddingCompleted)
                    return;

                queue = sQueue;
            }

            Interlocked.Increment(ref sPendingWriteCount);
            try
            {
                if (!queue.TryAdd(command))
                {
                    Interlocked.Decrement(ref sPendingWriteCount);
                    Interlocked.Increment(ref sDroppedCount);
                }
            }
            catch (InvalidOperationException)
            {
                Interlocked.Decrement(ref sPendingWriteCount);
                Interlocked.Increment(ref sDroppedCount);
            }
        }

        /// <summary>
        /// 按配置清理过期或过大的日志文件。
        /// </summary>
        /// <param name="filePath">日志文件路径。</param>
        /// <param name="options">Unity LogKit 写入配置。</param>
        /// <param name="now">当前时间。</param>
        public static void CleanUpLogFile(string filePath, UnityLogKitOptions options, DateTime now)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            var normalized = options != null ? options.Clone() : UnityLogKitOptions.CreateDefault();
            normalized.Normalize();

            var info = new FileInfo(filePath);
            if (info.Length > normalized.MaxFileBytes ||
                (now - info.LastWriteTime).TotalDays > normalized.MaxRetentionDays)
            {
                File.Delete(filePath);
            }
        }

        private static void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            if (ShouldSuppressRepeatedLog(logString))
                return;

            var needStack = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            Enqueue(new LogCommand
            {
                Time = DateTime.Now,
                Type = type,
                Message = logString ?? string.Empty,
                RawStack = needStack ? stackTrace : null
            });
        }

        private static bool ShouldSuppressRepeatedLog(string logString)
        {
            UnityLogKitOptions options;
            lock (sLock)
                options = sOptions;

            var maxSameLogCount = options != null ? options.MaxSameLogCount : 0;
            if (maxSameLogCount < 0)
                maxSameLogCount = 0;

            lock (sFilterLock)
            {
                if (string.Equals(logString, sLastLogString, StringComparison.Ordinal))
                {
                    sSameLogCounter++;
                    return sSameLogCounter > maxSameLogCount;
                }

                sLastLogString = logString;
                sSameLogCounter = 0;
                return false;
            }
        }

        private static void ProcessQueue()
        {
            BlockingCollection<LogCommand> queue;
            CancellationTokenSource cancellation;
            lock (sLock)
            {
                queue = sQueue;
                cancellation = sCancellation;
            }

            if (queue == null || cancellation == null)
                return;

            try
            {
                var directory = Path.GetDirectoryName(CurrentLogFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var builder = new StringBuilder(16 * 1024);
                while (!cancellation.IsCancellationRequested)
                {
                    LogCommand command;
                    if (!queue.TryTake(out command, TAKE_TIMEOUT_MS, cancellation.Token))
                        continue;

                    WriteBatch(queue, ref command, builder);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void WriteBatch(BlockingCollection<LogCommand> queue, ref LogCommand firstCommand, StringBuilder builder)
        {
            var path = CurrentLogFilePath;
            if (string.IsNullOrEmpty(path))
            {
                MarkWritten();
                return;
            }

            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                WriteSingle(writer, ref firstCommand, builder);
                MarkWritten();

                var batchCount = 0;
                LogCommand nextCommand;
                while (batchCount < BATCH_LIMIT && queue.TryTake(out nextCommand))
                {
                    WriteSingle(writer, ref nextCommand, builder);
                    MarkWritten();
                    batchCount++;
                }
            }
        }

        private static void WriteSingle(StreamWriter writer, ref LogCommand command, StringBuilder builder)
        {
            builder.Length = 0;
            builder.Append('[');
            builder.Append(command.Time.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append("] [");
            builder.Append(command.Type.ToString());
            builder.Append("] ");
            builder.Append(command.Message);

            if (!string.IsNullOrEmpty(command.RawStack))
            {
                builder.AppendLine();
                builder.Append(CleanStackTrace(command.RawStack));
            }

            var finalLine = builder.ToString();
            UnityLogKitOptions options;
            lock (sLock)
                options = sOptions;

            if (options != null && options.EnableEncryption)
                finalLine = EncryptString(finalLine, options);

            writer.WriteLine(finalLine);
        }

        private static void MarkWritten()
        {
            Interlocked.Decrement(ref sPendingWriteCount);
        }

    }
}
#endif
