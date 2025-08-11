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
        #region 日记记录和加密解密
        /// <summary>
        /// 日志文件路径
        /// </summary>
        private static readonly string logFilePath;
        /// <summary>
        /// 日志级别
        /// </summary>
        public enum LogLevel { None, Error, Warning, All }
        /// <summary>
        /// 输出日志的级别，默认为 All
        /// </summary>
        public static LogLevel Level = LogLevel.All;
        /// <summary>
        /// 日志队列
        /// </summary>
        private static readonly BlockingCollection<LogEntry> _queue;
        /// <summary>
        /// 取消令牌源，用于应用退出时停止写盘任务
        /// </summary>
        private static readonly CancellationTokenSource _cts;
        /// <summary>
        /// 是否对日志进行加密，默认为 true
        /// </summary>
        public static bool Encrypt = true;
        /// <summary>
        /// AES 加密密钥与 IV
        /// </summary>
        private static readonly byte[] _key = Encoding.UTF8.GetBytes("0123456789ABCDEF");
        private static readonly byte[] _iv = Encoding.UTF8.GetBytes("FEDCBA9876543210");
        /// <summary>
        /// 最大保存日志天数
        /// </summary>
        public static int MaxLogDays = 100;
        /// <summary>
        /// 最大保存日志文件大小，单位为字节
        /// </summary>
        public static long MaxLogFileSizeBytes = 100 * 1024 * 1024;

        static KitLogger()
        {
            // 设置日志文件路径
            if (Application.isEditor)
            {
                //编辑器下不需要记录日志
                logFilePath = $"{Application.persistentDataPath}/LogFile/log.log";
                return;
            }
            else logFilePath = $"{Application.dataPath}/LogFile/log.log";

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));

            // **按第一条日志时间判断是否需要清空**
            if (File.Exists(logFilePath))
            {
                bool shouldClear = false;
                var info = new FileInfo(logFilePath);

                // 1. 如果文件大小超限，直接标记清空
                if (info.Length > MaxLogFileSizeBytes)
                {
                    shouldClear = true;
                    Debug.Log($"[LogKit] 日志文件大小超限 ({info.Length} 字节) 准备清空");
                }
                else
                {
                    // 2. 文件未超限，尝试读取第一行来解析时间
                    try
                    {
                        using var reader = new StreamReader(logFilePath, Encoding.UTF8);
                        string firstLine = null;
                        // 跳过可能的空行
                        while (!reader.EndOfStream && string.IsNullOrWhiteSpace(firstLine = reader.ReadLine())) { }

                        if (!string.IsNullOrWhiteSpace(firstLine))
                        {
                            // 如果加密，则先解密
                            var raw = Encrypt ? DecryptString(firstLine) : firstLine;

                            // 用正则提取 "[yyyy-MM-dd HH:mm:ss]"
                            var m = Regex.Match(raw, @"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]");
                            if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var firstTime))
                            {
                                var ageDays = (DateTime.Now - firstTime).TotalDays;
                                if (ageDays > MaxLogDays)
                                {
                                    shouldClear = true;
                                    Debug.Log($"[LogKit] 日志首条已 {ageDays:F1} 天，超过阈值 {MaxLogDays} 天，准备清空");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LogKit] 读取首条日志失败，跳过清空判断: {ex.Message}");
                    }
                }

                if (shouldClear)
                {
                    try
                    {
                        File.Delete(logFilePath);
                        Debug.Log("[LogKit] 旧日志文件已清除，重新开始记录");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[LogKit] 清除旧日志文件失败: {ex}");
                    }
                }
            }


            // 订阅 Unity 的日志回调
            Application.logMessageReceived += HandleLog;
            Application.quitting += OnApplicationQuit;

            _queue = new();
            _cts = new();
            // 启动后台写盘任务
            Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        #region 后台写盘日志
        /// <summary>
        /// Unity 日志回调处理，将日志条目推入队列
        /// </summary>
        private static void HandleLog(string logText, string stackTrace, LogType type)
        {
            var entry = new LogEntry
            {
                Time = DateTime.Now,
                Type = type,
                Message = logText,
                StackTrace = stackTrace
            };
            _queue.Add(entry);
        }
        /// <summary>
        /// 应用退出时触发，停止写盘循环并完成队列
        /// </summary>
        private static void OnApplicationQuit()
        {
            _cts.Cancel();
            _queue.CompleteAdding();
        }
        /// <summary>
        /// 后台处理队列并写入磁盘
        /// </summary>
        private static async Task ProcessQueueAsync(CancellationToken token)
        {
            try
            {
                foreach (var entry in _queue.GetConsumingEnumerable(token))
                {
                    // 构造日志文本
                    var builder = new StringBuilder();
                    builder.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss}] [{1}]\n{2}\n", entry.Time, entry.Type, entry.Message);
                    if (!string.IsNullOrEmpty(entry.StackTrace) && entry.Type is not LogType.Log)
                        builder.AppendLine(entry.StackTrace);

                    var logLine = builder.ToString().TrimEnd();

                    if (Encrypt)
                    {
                        logLine = EncryptString(logLine);
                    }

                    // 写入文件（每条日志单独一行）
                    await File.AppendAllTextAsync(logFilePath, logLine + Environment.NewLine, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 应用退出时正常取消
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogKit] 后台写盘出错: {ex}");
            }
        }
        #endregion

        #region 加密解密日志
        /// <summary>
        /// AES 加密并返回 Base64 字符串
        /// </summary>
        public static string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }
        /// <summary>
        /// AES 解密 Base64 字符串
        /// </summary>
        public static string DecryptString(string cipherText)
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        // 日志条目结构体
        private struct LogEntry
        {
            public DateTime Time;
            public LogType Type;
            public string Message;
            public string StackTrace;
        }
        #endregion

#if UNITY_EDITOR
        private const string MenuPath = "Tools/解密log文件";

        [UnityEditor.MenuItem(MenuPath)]
        public static void DecryptLogFileMenu()
        {
            // 弹出文件选择对话框，过滤 .log/.txt 等日志文件
            string path = UnityEditor.EditorUtility.OpenFilePanel("选择加密log文件", logFilePath, "log,txt");
            if (string.IsNullOrEmpty(path))
                return;

            string directory = Path.GetDirectoryName(path);
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string outputPath = Path.Combine(directory, filenameWithoutExt + "Decrypt" + extension);

            try
            {
                var lines = File.ReadAllLines(path);
                using (var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8))
                {
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            writer.WriteLine();
                            continue;
                        }

                        try
                        {
                            // 使用 LogKit 提供的解密方法
                            string plain = DecryptString(line.Trim());
                            writer.WriteLine(plain);
                        }
                        catch
                        {
                            // 解密失败，则原样写入
                            writer.WriteLine(line);
                        }
                    }
                }

                // 在编辑器中显示输出文件
                UnityEditor.EditorUtility.RevealInFinder(outputPath);
            }
            catch (Exception e)
            {
                UnityEditor.EditorUtility.DisplayDialog("Decrypt Log File", $"解密失败:\n{e.Message}", "OK");
            }
        }
#endif

        #endregion


        public static void Log<T>(object message, Object context = null) => Log($"[{typeof(T).Name}] {message}", context);

        public static void Log(object message, Object context = null)
        {
            if (Level > LogLevel.None)
            {
                LogInfo(LogInfoLeve.log, message, context);
            }
        }


        public static void LogWarning<T>(object message, Object context = null) => LogWarning($"[{typeof(T).Name}] {message}", context);

        public static void LogWarning(object message, Object context = null)
        {
            if (Level >= LogLevel.Warning)
            {
                LogInfo(LogInfoLeve.warning, message, context);
            }
        }

        public static void LogError<T>(object message, Object context = null) => LogError($"[{typeof(T).Name}] {message}", context);

        public static void LogError(object message, Object context = null)
        {
            LogInfo(LogInfoLeve.error, message, context);
        }

        public static void LogException(Exception message, Object context = null)
        {
            LogInfo(LogInfoLeve.exception, message, context);
        }

        private static void LogInfo(LogInfoLeve logInfoLeve, object message, Object context = null)
        {
            switch (logInfoLeve)
            {
                case LogInfoLeve.log:
                    Debug.Log(message, context);
                    break;
                case LogInfoLeve.warning:
                    Debug.LogWarning(message, context);
                    break;
                case LogInfoLeve.error:
                    Debug.LogError(message, context);
                    break;
                case LogInfoLeve.exception:
                    if (message is Exception exception)
                    {
                        Debug.LogException(exception, context);
                    }
                    else
                    {
                        Debug.LogError(message, context);
                    }
                    break;
               }
        }

        private enum LogInfoLeve
        {
            log,
            warning,
            error,
            exception
        }
    }
}