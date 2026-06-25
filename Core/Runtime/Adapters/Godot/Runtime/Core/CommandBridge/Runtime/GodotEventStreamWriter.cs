#if GODOT
using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// 将 Godot 适配器事件追加写入 .yokiframe/events/<type>.jsonl。
    /// </summary>
    public static class GodotEventStreamWriter
    {
        private const string ENGINE_ID = "godot-runtime";

        private static readonly object sWriteLock = new object();
        private static readonly Dictionary<string, long> sStreamSequences = new Dictionary<string, long>();
        private static string sEventsDir;
        private static bool sInitialized;
        private static string sStreamId;

        public static void Init(string yokiframeRoot)
        {
            lock (sWriteLock)
            {
                if (string.IsNullOrEmpty(yokiframeRoot))
                    return;

                var fullRoot = Path.GetFullPath(yokiframeRoot);
                sEventsDir = Path.Combine(fullRoot, "events");
                try
                {
                    FileBridgeFileSystem.CreateDirectoryInRoot(fullRoot, sEventsDir);
                    sInitialized = true;
                    if (string.IsNullOrEmpty(sStreamId))
                        sStreamId = CreateStreamId();
                }
                catch (Exception e)
                {
                    sInitialized = false;
                    sEventsDir = null;
                    GD.PushWarning("[YokiFrame][GodotEventStreamWriter] 创建 events 目录失败: " + e.Message);
                }
            }
        }

        public static void Write(string type, string payloadJson)
        {
            if (!sInitialized || string.IsNullOrEmpty(sEventsDir))
                return;

            if (!IsSafeEventType(type))
            {
                GD.PushWarning("[YokiFrame][GodotEventStreamWriter] 非法事件类型: " + type);
                return;
            }

            lock (sWriteLock)
            {
                try
                {
                    var path = Path.Combine(sEventsDir, type + ".jsonl");
                    var line = BuildEventLine(type, payloadJson, NextSequence(type));
                    FileBridgeFileSystem.AppendLineAndFlushInRoot(sEventsDir, path, line);
                }
                catch (Exception e)
                {
                    GD.PushWarning("[YokiFrame][GodotEventStreamWriter] 写入事件失败: " + e.Message);
                }
            }
        }

        internal static string BuildEventLine(string type, string payloadJson, long sequence)
        {
            var payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
            return "{\"protocolVersion\":2,\"engineId\":\"" + ENGINE_ID +
                   "\",\"type\":\"" + JsonHelper.EscapeString(type) +
                   "\",\"streamId\":\"" + JsonHelper.EscapeString(sStreamId) +
                   "\",\"seq\":" + sequence +
                   ",\"timestamp\":\"" + JsonHelper.EscapeString(DateTime.UtcNow.ToString("O")) +
                   "\",\"payload\":" + payload + "}";
        }

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
