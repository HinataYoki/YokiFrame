using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    public static class KitLogger
    {
        #region 配置项

        public enum LogLevel { None, Error, Warning, All }

        public static LogLevel Level = LogLevel.All;
        public static bool EnableEncryption = true;
        public static bool AutoEnableWriteLogToFile = false;

        // 限制队列最大数量，防止无限报错导致内存撑爆
        public static int MaxQueueSize = 20000;

        // 连续相同日志的忽略阈值，防止 Update 报错把 IO 也就是磁盘写死
        public static int MaxSameLogCount = 50;

        public static int MaxRetentionDays = 10;
        public static long MaxFileBytes = 50 * 1024 * 1024;

        private static string LogDirectory => Path.Combine(Application.persistentDataPath, "LogFiles");

        private static bool _saveLogInEditor = false;

        public static bool SaveLogInEditor
        {
            get => _saveLogInEditor;
            set
            {
                if (_saveLogInEditor != value)
                {
                    _saveLogInEditor = value;
                    if (Application.isEditor)
                    {
                        if (_saveLogInEditor) LogWriter.Initialize();
                        else LogWriter.Shutdown();
                    }
                }
            }
        }

        #endregion

        #region 公开接口

        [HideInCallstack]
        public static void Log(object msg, Object context = null) => LogInternal(LogType.Log, msg, context);

        [HideInCallstack]
        public static void Warning(object msg, Object context = null) => LogInternal(LogType.Warning, msg, context);

        [HideInCallstack]
        public static void Error(object msg, Object context = null) => LogInternal(LogType.Error, msg, context);

        [HideInCallstack]
        public static void Exception(Exception ex, Object context = null) => LogInternal(LogType.Exception, ex, context);

        [HideInCallstack]
        private static void LogInternal(LogType type, object msg, Object context)
        {
            if (Level == LogLevel.None) return;
            if (Level == LogLevel.Error && type != LogType.Error && type != LogType.Exception) return;
            if (Level == LogLevel.Warning && type == LogType.Log) return;

            switch (type)
            {
                case LogType.Log: Debug.Log(msg, context); break;
                case LogType.Warning: Debug.LogWarning(msg, context); break;
                case LogType.Error: Debug.LogError(msg, context); break;
                case LogType.Assert: Debug.LogAssertion(msg, context); break;
                case LogType.Exception:
                    if (msg is Exception e) Debug.LogException(e, context);
                    else Debug.LogError(msg, context);
                    break;
            }
        }

        #endregion

        #region 系统初始化
        static KitLogger()
        {
            LogWriter.Initialize();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void AutoInit()
        {
            if (AutoEnableWriteLogToFile)
            {
                LogWriter.Initialize();
            }
        }

        // 用于去重的临时变量
        private static string _lastLogString;
        private static int _sameLogCounter;
        private static readonly object _filterLock = new object();

        private static void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            // 重复日志熔断机制
            // 如果在 Update 中疯狂报错，IO 可能会成为瓶颈，这里直接丢弃过多的重复日志
            bool skipLog = false;
            lock (_filterLock) // 确保线程安全
            {
                if (logString == _lastLogString)
                {
                    _sameLogCounter++;
                    if (_sameLogCounter > MaxSameLogCount)
                    {
                        skipLog = true;
                    }
                }
                else
                {
                    _lastLogString = logString;
                    _sameLogCounter = 0;
                }
            }
            if (skipLog) return;


            // 主线程只做最轻量的判断和捕捉，不做正则，不做拼接
            bool needStack = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            string capturedStack = null;

            if (needStack)
            {
                if (string.IsNullOrEmpty(stackTrace))
                {
                    capturedStack = Environment.StackTrace;
                }
                else
                {
                    capturedStack = stackTrace;
                }
            }

            // 投递结构体
            LogWriter.Enqueue(new LogCommand
            {
                Time = DateTime.Now,
                Type = type,
                Message = logString,
                RawStack = capturedStack
            });
        }

        #endregion

        #region 核心写入器

        private struct LogCommand
        {
            public DateTime Time;
            public LogType Type;
            public string Message;
            public string RawStack;
        }

        private static class LogWriter
        {
            private static BlockingCollection<LogCommand> _queue;
            private static CancellationTokenSource _cts;
            private static Task _writeTask;
            private static string _filePath;
            private static volatile bool _initialized = false;

            // 复用 Aes 对象，修复 IV 乱码问题，同时减少 new 的开销
            private static Aes _cachedAes;
            private static readonly byte[] Key = Encoding.UTF8.GetBytes("0123456789ABCDEF");
            private static readonly byte[] IV = Encoding.UTF8.GetBytes("FEDCBA9876543210");

            // 复用 byte 数组，减少 Encoding.UTF8.GetBytes 的 GC
            private static byte[] _sharedBuffer;
            private const int INITIAL_BUFFER_SIZE = 64 * 1024; // 64KB 初始 buffer

            private static readonly Regex _stackCleanRegex = new(@"^\s*at\s+(.*?)(?=\s*\[|\s*in\s|<|$)", RegexOptions.Compiled);

            public static void Initialize()
            {
                if (_initialized) return;
                if (Application.isEditor && !KitLogger.SaveLogInEditor) return;

                _initialized = true;

                _filePath = Path.Combine(KitLogger.LogDirectory, 
                    Application.isEditor ? "editor.log" : "player.log");

                RepairLastLine();

                // 初始化加密组件
                InitAes();

                // 初始化缓冲区
                _sharedBuffer = new byte[INITIAL_BUFFER_SIZE];

                CleanUpOldLogs();

                _queue = new BlockingCollection<LogCommand>(KitLogger.MaxQueueSize);
                _cts = new CancellationTokenSource();

                Application.logMessageReceivedThreaded += HandleUnityLog;
                Application.quitting += Shutdown;

                _writeTask = Task.Factory.StartNew(ProcessQueue, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            private static void InitAes()
            {
                if (_cachedAes != null) return;
                _cachedAes = Aes.Create();
                _cachedAes.Key = Key;
                _cachedAes.IV = IV;
                _cachedAes.Padding = PaddingMode.PKCS7;
                _cachedAes.Mode = CipherMode.CBC;
            }

            private static void RepairLastLine()
            {
                try
                {
                    if (!File.Exists(_filePath)) return;
                    var info = new FileInfo(_filePath);
                    if (info.Length > 0)
                    {
                        using (var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write))
                        {
                            byte[] newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                            fs.Write(newline, 0, newline.Length);
                            fs.Flush();
                        }
                    }
                }
                catch { }
            }

            private static void CleanUpOldLogs()
            {
                try
                {
                    if (File.Exists(_filePath))
                    {
                        var info = new FileInfo(_filePath);
                        if (info.Length > MaxFileBytes || (DateTime.Now - info.LastWriteTime).TotalDays > MaxRetentionDays)
                        {
                            File.Delete(_filePath);
                        }
                    }
                }
                catch { }
            }

            public static void Enqueue(LogCommand cmd)
            {
                if (!_initialized || _queue == null || _queue.IsAddingCompleted) return;
                _queue.TryAdd(cmd);
            }

            private static void ProcessQueue()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

                    // 【优化】加大初始容量，减少扩容 GC
                    var sb = new StringBuilder(16 * 1024);

                    while (!_cts.IsCancellationRequested)
                    {
                        if (_queue.TryTake(out LogCommand cmd, -1, _cts.Token))
                        {
                            using (var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                            using (var writer = new StreamWriter(fs, Encoding.UTF8))
                            {
                                ProcessSingleLog(writer, ref cmd, sb);

                                int batchCount = 0;
                                while (batchCount < 500 && _queue.TryTake(out LogCommand nextCmd))
                                {
                                    ProcessSingleLog(writer, ref nextCmd, sb);
                                    batchCount++;
                                }
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

                if (EnableEncryption && _cachedAes != null)
                {
                    finalLine = EncryptStringInternal(finalLine);
                }

                writer.WriteLine(finalLine);
            }

            private static string CleanStackTrace(string rawStack)
            {
                if (string.IsNullOrEmpty(rawStack)) return "";

                var cleanSb = new StringBuilder();
                var lines = rawStack.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.Contains("UnityEngine.Application") ||
                        line.Contains("UnityEngine.Logger") ||
                        line.Contains("UnityEngine.Debug") ||
                        line.Contains("YokiFrame.KitLogger") ||
                        line.Contains("System.Environment")) continue;

                    var match = _stackCleanRegex.Match(line);
                    if (match.Success) cleanSb.AppendLine(match.Groups[1].Value.Trim());
                    else cleanSb.AppendLine(line.Trim());
                }
                return cleanSb.ToString();
            }

            private static string EncryptStringInternal(string text)
            {
                try
                {
                    // 计算需要的字节数，并检查缓冲区是否需要扩容
                    int byteCount = Encoding.UTF8.GetByteCount(text);
                    if (_sharedBuffer.Length < byteCount)
                    {
                        Array.Resize(ref _sharedBuffer, Math.Max(byteCount, _sharedBuffer.Length * 2));
                    }

                    // 直接写入复用的缓冲区，避免产生 new byte[]
                    Encoding.UTF8.GetBytes(text, 0, text.Length, _sharedBuffer, 0);

                    // 创建一次性 Encryptor (轻量级)
                    // 使用 TransformFinalBlock 兼容性最好，虽然会返回新数组，但省去了 input 的分配
                    using (var encryptor = _cachedAes.CreateEncryptor())
                    {
                        byte[] output = encryptor.TransformFinalBlock(_sharedBuffer, 0, byteCount);
                        return Convert.ToBase64String(output);
                    }
                }
                catch { return text; }
            }

            // 解密依然使用完全独立的对象，保证线程安全和状态隔离
            public static string DecryptString(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return "";
                try
                {
                    using var aes = Aes.Create();
                    aes.Key = Key;
                    aes.IV = IV;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Mode = CipherMode.CBC;

                    using var decryptor = aes.CreateDecryptor();
                    byte[] input = Convert.FromBase64String(text);
                    byte[] output = decryptor.TransformFinalBlock(input, 0, input.Length);
                    return Encoding.UTF8.GetString(output);
                }
                catch { return $"[DECRYPT_FAIL] {text}"; }
            }

            public static void Shutdown()
            {
                if (!_initialized) return;
                _initialized = false;

                Application.logMessageReceivedThreaded -= HandleUnityLog;
                Application.quitting -= Shutdown;

                _cts?.Cancel();
                _queue?.CompleteAdding();
                try { _writeTask?.Wait(500); } catch { }

                _cts?.Dispose();
                _cts = null;

                _cachedAes?.Dispose();
                _cachedAes = null;

                _sharedBuffer = null;
            }
        }

        #endregion

        #region Unity 编辑器工具

#if UNITY_EDITOR
        public static class LogTools
        {
            [UnityEditor.MenuItem("YokiFrame/KitLogger/打开日志目录", priority = 0)]
            public static void OpenLogFolder()
            {
                string dir = KitLogger.LogDirectory;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string filePath = Path.Combine(dir, "editor.log");
                if (File.Exists(filePath)) UnityEditor.EditorUtility.RevealInFinder(filePath);
                else UnityEditor.EditorUtility.RevealInFinder(dir);
            }

            [UnityEditor.MenuItem("YokiFrame/KitLogger/解密日志文件", priority = 1)]
            public static void DecryptLog()
            {
                string dir = KitLogger.LogDirectory;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string path = UnityEditor.EditorUtility.OpenFilePanel("选择日志", dir, "log,txt");
                if (string.IsNullOrEmpty(path)) return;

                var lines = File.ReadAllLines(path);
                var sb = new StringBuilder();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string decoded = LogWriter.DecryptString(line);
                    sb.AppendLine(decoded);
                }

                string outPath = path + ".decoded.log";
                File.WriteAllText(outPath, sb.ToString());
                UnityEditor.EditorUtility.RevealInFinder(outPath);
                Debug.Log($"[KitLogger] 解密完成: {outPath}");
            }
        }
#endif
        #endregion
    }
}