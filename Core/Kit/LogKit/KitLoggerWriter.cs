using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 日志文件写入器 - 负责队列管理、文件IO、加密
    /// </summary>
    public static class KitLoggerWriter
    {
        public struct LogCommand
        {
            public DateTime Time;
            public LogType Type;
            public string Message;
            public string RawStack;
        }

        private static BlockingCollection<LogCommand> sQueue;
        private static CancellationTokenSource sCts;
        private static Task sWriteTask;
        private static string sFilePath;
        private static volatile bool sInitialized;

        // 公开给编辑器工具使用
        public static string LogDirectory => Path.Combine(Application.persistentDataPath, "LogFiles");

        // 加密相关
        private static Aes sCachedAes;
        private static readonly byte[] sKey = Encoding.UTF8.GetBytes("0123456789ABCDEF");
        private static readonly byte[] sIV = Encoding.UTF8.GetBytes("FEDCBA9876543210");
        private static byte[] sSharedBuffer;
        private const int INITIAL_BUFFER_SIZE = 64 * 1024;

        // 堆栈清理
        private static readonly Regex sStackCleanRegex = new(@"^\s*at\s+(.*?)(?=\s*\[|\s*in\s|<|$)", RegexOptions.Compiled);
        [ThreadStatic] private static StringBuilder sCachedStackSb;

        // 去重过滤
        private static string sLastLogString;
        private static int sSameLogCounter;
        private static readonly object sFilterLock = new();

        public static void Initialize()
        {
            if (sInitialized) return;
            if (Application.isEditor && !KitLogger.SaveLogInEditor) return;

            sInitialized = true;

            sFilePath = Path.Combine(LogDirectory, Application.isEditor ? "editor.log" : "player.log");

            sQueue = new BlockingCollection<LogCommand>(KitLogger.MaxQueueSize);
            sCts = new CancellationTokenSource();

            InitAes();
            sSharedBuffer = new byte[INITIAL_BUFFER_SIZE];

            RepairLastLine();
            CleanUpOldLogs();

            Application.logMessageReceivedThreaded += HandleUnityLog;
            Application.quitting += Shutdown;

            sWriteTask = Task.Factory.StartNew(ProcessQueue, sCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static void Shutdown()
        {
            if (!sInitialized) return;
            sInitialized = false;

            Application.logMessageReceivedThreaded -= HandleUnityLog;
            Application.quitting -= Shutdown;

            sCts?.Cancel();
            sQueue?.CompleteAdding();
            try { sWriteTask?.Wait(500); } catch { }

            sCts?.Dispose();
            sCts = null;
            sCachedAes?.Dispose();
            sCachedAes = null;
            sSharedBuffer = null;
        }

        public static void Enqueue(LogCommand cmd)
        {
            if (!sInitialized || sQueue == null || sQueue.IsAddingCompleted) return;
            sQueue.TryAdd(cmd);
        }

        private static void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            // 重复日志熔断
            bool skipLog = false;
            lock (sFilterLock)
            {
                if (logString == sLastLogString)
                {
                    sSameLogCounter++;
                    if (sSameLogCounter > KitLogger.MaxSameLogCount) skipLog = true;
                }
                else
                {
                    sLastLogString = logString;
                    sSameLogCounter = 0;
                }
            }
            if (skipLog) return;

            bool needStack = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            string capturedStack = null;
            if (needStack)
            {
                capturedStack = string.IsNullOrEmpty(stackTrace) ? Environment.StackTrace : stackTrace;
            }

            Enqueue(new LogCommand
            {
                Time = DateTime.Now,
                Type = type,
                Message = logString,
                RawStack = capturedStack
            });
        }

        #region 队列处理

        private static void ProcessQueue()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sFilePath));
                var sb = new StringBuilder(16 * 1024);

                while (!sCts.IsCancellationRequested)
                {
                    if (sQueue.TryTake(out LogCommand cmd, 100, sCts.Token))
                    {
                        using var fs = new FileStream(sFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                        using var writer = new StreamWriter(fs, Encoding.UTF8);

                        ProcessSingleLog(writer, ref cmd, sb);

                        int batchCount = 0;
                        while (batchCount < 500 && sQueue.TryTake(out LogCommand nextCmd))
                        {
                            ProcessSingleLog(writer, ref nextCmd, sb);
                            batchCount++;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (Application.isPlaying) Debug.LogWarning($"[KitLogger] Worker Error: {e.Message}");
            }
        }

        private static void ProcessSingleLog(StreamWriter writer, ref LogCommand cmd, StringBuilder sb)
        {
            sb.Clear();
            sb.Append('[').Append(cmd.Time.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
              .Append('[').Append(cmd.Type.ToString()).Append("] ")
              .Append(cmd.Message);

            if (!string.IsNullOrEmpty(cmd.RawStack))
            {
                sb.AppendLine();
                sb.Append(CleanStackTrace(cmd.RawStack));
            }

            string finalLine = sb.ToString();
            if (KitLogger.EnableEncryption && sCachedAes != null)
            {
                finalLine = EncryptString(finalLine);
            }
            writer.WriteLine(finalLine);
        }

        #endregion

        #region 加密解密

        private static void InitAes()
        {
            if (sCachedAes != null) return;
            sCachedAes = Aes.Create();
            sCachedAes.Key = sKey;
            sCachedAes.IV = sIV;
            sCachedAes.Padding = PaddingMode.PKCS7;
            sCachedAes.Mode = CipherMode.CBC;
        }

        private static string EncryptString(string text)
        {
            try
            {
                int byteCount = Encoding.UTF8.GetByteCount(text);
                if (sSharedBuffer.Length < byteCount)
                {
                    Array.Resize(ref sSharedBuffer, Math.Max(byteCount, sSharedBuffer.Length * 2));
                }
                Encoding.UTF8.GetBytes(text, 0, text.Length, sSharedBuffer, 0);

                using var encryptor = sCachedAes.CreateEncryptor();
                byte[] output = encryptor.TransformFinalBlock(sSharedBuffer, 0, byteCount);
                return Convert.ToBase64String(output);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KitLogger] Encrypt failed: {ex.Message}");
                return text;
            }
        }

        public static string DecryptString(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            try
            {
                using var aes = Aes.Create();
                aes.Key = sKey;
                aes.IV = sIV;
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;

                using var decryptor = aes.CreateDecryptor();
                byte[] input = Convert.FromBase64String(text);
                byte[] output = decryptor.TransformFinalBlock(input, 0, input.Length);
                return Encoding.UTF8.GetString(output);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KitLogger] Decrypt failed: {ex.Message}");
                return $"[DECRYPT_FAIL] {text}";
            }
        }

        #endregion

        #region 辅助方法

        private static string CleanStackTrace(string rawStack)
        {
            if (string.IsNullOrEmpty(rawStack)) return "";

            sCachedStackSb ??= new StringBuilder(1024);
            sCachedStackSb.Clear();

            var lines = rawStack.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("UnityEngine.Application") ||
                    line.Contains("UnityEngine.Logger") ||
                    line.Contains("UnityEngine.Debug") ||
                    line.Contains("YokiFrame.KitLogger") ||
                    line.Contains("System.Environment")) continue;

                var match = sStackCleanRegex.Match(line);
                if (match.Success) sCachedStackSb.AppendLine(match.Groups[1].Value.Trim());
                else sCachedStackSb.AppendLine(line.Trim());
            }
            return sCachedStackSb.ToString();
        }

        private static void RepairLastLine()
        {
            try
            {
                if (!File.Exists(sFilePath)) return;
                var info = new FileInfo(sFilePath);
                if (info.Length > 0)
                {
                    using var fs = new FileStream(sFilePath, FileMode.Append, FileAccess.Write);
                    byte[] newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                    fs.Write(newline, 0, newline.Length);
                    fs.Flush();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KitLogger] RepairLastLine failed: {ex.Message}");
            }
        }

        private static void CleanUpOldLogs()
        {
            try
            {
                if (File.Exists(sFilePath))
                {
                    var info = new FileInfo(sFilePath);
                    if (info.Length > KitLogger.MaxFileBytes || (DateTime.Now - info.LastWriteTime).TotalDays > KitLogger.MaxRetentionDays)
                    {
                        File.Delete(sFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KitLogger] CleanUpOldLogs failed: {ex.Message}");
            }
        }

        #endregion
    }
}
