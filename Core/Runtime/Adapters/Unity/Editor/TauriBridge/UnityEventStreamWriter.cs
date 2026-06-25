#if !GODOT
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将引擎事件追加写入 .yokiframe/events/<type>.jsonl（JSON Lines），
    /// 供 Tauri 文件监视器读取并转发给前端。
    ///
    /// 替代旧的 YokiWsClient.EnqueuePush（WebSocket 推送）。
    /// Domain Reload 后自动重新初始化。
    /// </summary>
    [InitializeOnLoad]
    internal static class UnityEventStreamWriter
    {
        private static string sEventsDir;
        private static readonly object sWriteLock = new();
        private static readonly Dictionary<string, long> sStreamSequences = new();
        private static bool sInitialized;
        private static string sStreamId;
        private const string ENGINE_ID = "unity-editor";
        private const long DEFAULT_MAX_EVENT_FILE_BYTES = 4 * 1024 * 1024;
        private static readonly TimeSpan DefaultMaxEventFileAge = TimeSpan.FromDays(1);
        private static long sMaxEventFileBytes = DEFAULT_MAX_EVENT_FILE_BYTES;
        private static TimeSpan sMaxEventFileAge = DefaultMaxEventFileAge;

        static UnityEventStreamWriter()
        {
            EditorApplication.delayCall += LazyInit;
        }

        private static void LazyInit()
        {
            var projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            var yokiframeRoot = Path.Combine(projectRoot, ".yokiframe");
            Init(yokiframeRoot);
        }

        internal static void Init(string yokiframeRoot)
        {
            lock (sWriteLock)
            {
                var fullRoot = Path.GetFullPath(yokiframeRoot);
                sEventsDir = Path.Combine(fullRoot, "events");
                try { FileBridgeFileSystem.CreateDirectoryInRoot(fullRoot, sEventsDir); }
                catch (Exception e)
                {
                    LogKit.Warning($"[EventStreamWriter] 创建 events 目录失败: {e.Message}");
                    sEventsDir = null;
                }
                sInitialized = sEventsDir != null;
                if (sInitialized && string.IsNullOrEmpty(sStreamId))
                    sStreamId = CreateStreamId();
            }
        }

        /// <summary>写入一条事件到 .yokiframe/events/<type>.jsonl</summary>
        public static void Write(string type, string payloadJson)
        {
            if (!sInitialized || sEventsDir == null) return;
            if (!IsSafeEventType(type))
            {
                LogKit.Warning($"[EventStreamWriter] 非法事件类型: {type}");
                return;
            }

            lock (sWriteLock)
            {
                try
                {
                    var path = Path.Combine(sEventsDir, $"{type}.jsonl");
                    var line = BuildEventLine(type, payloadJson, NextSequence(type));
                    RotateEventFileIfNeeded(path, line);
                    FileBridgeFileSystem.AppendLineAndFlushInRoot(sEventsDir, path, line);
                }
                catch (Exception e)
                {
                    LogKit.Warning($"[EventStreamWriter] 写入事件失败: {e.Message}");
                }
            }
        }

        internal static void ConfigureRotation(long maxEventFileBytes, TimeSpan maxEventFileAge)
        {
            lock (sWriteLock)
            {
                sMaxEventFileBytes = maxEventFileBytes;
                sMaxEventFileAge = maxEventFileAge;
            }
        }

        internal static void Reset()
        {
            lock (sWriteLock)
            {
                sEventsDir = null;
                sInitialized = false;
                sStreamId = null;
                sMaxEventFileBytes = DEFAULT_MAX_EVENT_FILE_BYTES;
                sMaxEventFileAge = DefaultMaxEventFileAge;
                sStreamSequences.Clear();
            }
        }

        internal static string BuildEventLine(string type, string payloadJson, long sequence)
        {
            var payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
            return $"{{\"protocolVersion\":2,\"engineId\":\"{ENGINE_ID}\",\"type\":\"{EscapeJson(type)}\",\"streamId\":\"{EscapeJson(sStreamId)}\",\"seq\":{sequence},\"timestamp\":\"{DateTime.UtcNow:O}\",\"payload\":{payload}}}";
        }

        private static string EscapeJson(string s)
            => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static long NextSequence(string type)
        {
            long current;
            sStreamSequences.TryGetValue(type, out current);
            current++;
            sStreamSequences[type] = current;
            return current;
        }

        private static string CreateStreamId()
        {
            return ENGINE_ID + "-" + Guid.NewGuid().ToString("N");
        }

        private static void RotateEventFileIfNeeded(string path, string nextLine)
        {
            if (!File.Exists(path))
                return;

            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0)
                return;

            var shouldRotateBySize = sMaxEventFileBytes > 0 &&
                                     info.Length + EstimateLineBytes(nextLine) > sMaxEventFileBytes;
            var shouldRotateByAge = sMaxEventFileAge >= TimeSpan.Zero &&
                                    DateTime.UtcNow - info.LastWriteTimeUtc >= sMaxEventFileAge;
            if (!shouldRotateBySize && !shouldRotateByAge)
                return;

            var archivePath = BuildArchivePath(path);
            FileBridgeFileSystem.ReplaceFileInRoot(sEventsDir, path, archivePath);
        }

        private static long EstimateLineBytes(string line)
        {
            var content = line ?? string.Empty;
            if (!content.EndsWith("\n", StringComparison.Ordinal))
                content += "\n";
            return System.Text.Encoding.UTF8.GetByteCount(content);
        }

        private static string BuildArchivePath(string path)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff");
            var archivePath = path + "." + timestamp + "." + Guid.NewGuid().ToString("N") + ".archive";
            FileBridgeFileSystem.EnsurePathWithinRoot(sEventsDir, archivePath);
            return archivePath;
        }

        private static bool IsSafeEventType(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128)
                return false;
            if (value == "." || value == "..")
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var isLetter = c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
                var isDigit = c >= '0' && c <= '9';
                if (isLetter || isDigit || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }
    }
}
#endif
