#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>按需创建有界 AudioKit 状态，不在音频帧更新热路径产生诊断分配。</summary>
    internal static class AudioKitSnapshotWriter
    {
        private const int MAX_BUSES = 64;
        private const int MAX_VOICES = 128;
        private const int MAX_HISTORY = 128;
        private const int COMPACT_BUSES = 16;
        private const int MAX_NAME_LENGTH = 128;
        private const int MAX_PATH_LENGTH = 320;

        /// <summary>创建后端、Loader、总线和活动 voice 数量的轻量统计。</summary>
        internal static string WriteStats()
        {
            CaptureState(out List<AudioBusSnapshot> buses, out List<AudioVoiceSnapshot> voices,
                out List<AudioHistoryEntry> history);
            var builder = new StringBuilder(640);
            AppendHeader(builder);
            AppendBackend(builder);
            AppendMaster(builder, buses);
            builder.Append(",\"busCount\":").Append(buses.Count);
            builder.Append(",\"activeVoiceCount\":").Append(voices.Count);
            builder.Append(",\"historyCount\":").Append(history.Count).Append('}');
            return builder.ToString();
        }

        /// <summary>创建适合 Snapshot 与 Shared Memory 的完整 AudioKit 工具状态。</summary>
        internal static string WriteWorkbench()
        {
            CaptureState(out List<AudioBusSnapshot> buses, out List<AudioVoiceSnapshot> voices,
                out List<AudioHistoryEntry> history);
            int voiceLimit = Math.Min(voices.Count, MAX_VOICES);
            int historyLimit = Math.Min(history.Count, MAX_HISTORY);
            while (true)
            {
                string json = WriteWorkbench(buses, voices, history, voiceLimit, historyLimit, MAX_BUSES);
                if (Encoding.UTF8.GetByteCount(json)
                    <= YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES) return json;
                if (historyLimit > 0) historyLimit /= 2;
                else if (voiceLimit > 0) voiceLimit /= 2;
                else return WriteWorkbench(buses, voices, history, 0, 0, COMPACT_BUSES);
            }
        }

        /// <summary>复制一次稳定诊断输入；voices 只抓取一次，交给 GetBuses 复用。</summary>
        private static void CaptureState(
            out List<AudioBusSnapshot> buses,
            out List<AudioVoiceSnapshot> voices,
            out List<AudioHistoryEntry> history)
        {
            buses = new List<AudioBusSnapshot>(16);
            voices = new List<AudioVoiceSnapshot>(32);
            history = new List<AudioHistoryEntry>(MAX_HISTORY);
            AudioKit.GetActiveVoices(voices);
            AudioKit.GetBuses(buses, voices);
            AudioKit.GetHistory(history);
        }

        private static string WriteWorkbench(
            IReadOnlyList<AudioBusSnapshot> buses,
            IReadOnlyList<AudioVoiceSnapshot> voices,
            IReadOnlyList<AudioHistoryEntry> history,
            int voiceLimit,
            int historyLimit,
            int busLimit)
        {
            var builder = new StringBuilder(8192);
            AppendHeader(builder);
            AppendBackend(builder);
            AppendMaster(builder, buses);
            AppendBuses(builder, buses, busLimit);
            AppendVoices(builder, voices, voiceLimit);
            AppendHistory(builder, history, historyLimit);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendHeader(StringBuilder builder)
        {
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(AudioKit.DiagnosticVersion);
        }

        private static void AppendBackend(StringBuilder builder)
        {
            IAudioBackend backend = AudioKit.GetBackend();
            AudioBackendCapabilities capabilities = backend == null
                ? AudioBackendCapabilities.None
                : backend.Capabilities;
            builder.Append(",\"backend\":{\"name\":");
            AppendString(builder, AudioKit.BackendName, MAX_NAME_LENGTH);
            builder.Append(",\"capabilities\":").Append((int)capabilities);
            builder.Append(",\"capabilityNames\":");
            AppendString(builder, capabilities.ToString(), MAX_NAME_LENGTH);
            builder.Append(",\"resourceLoader\":");
            AppendString(builder, AudioKit.ResourceLoaderName, MAX_NAME_LENGTH);
            builder.Append('}');
        }

        private static void AppendMaster(StringBuilder builder, IReadOnlyList<AudioBusSnapshot> buses)
        {
            AudioBusSnapshot master = FindMaster(buses);
            builder.Append(",\"master\":{\"volume\":");
            AppendFloat(builder, master == null ? AudioKit.GetGlobalVolume() : master.Volume);
            builder.Append(",\"effectiveVolume\":");
            AppendFloat(builder, master == null ? GetMasterEffectiveVolume() : master.EffectiveVolume);
            builder.Append(",\"muted\":");
            AppendBool(builder, master == null ? AudioKit.IsMuted() : master.Muted);
            builder.Append(",\"activeVoiceCount\":")
                .Append(master == null ? 0 : master.ActiveVoiceCount).Append('}');
        }

        private static float GetMasterEffectiveVolume() =>
            AudioKit.IsMuted() ? 0f : AudioKit.GetGlobalVolume();

        private static AudioBusSnapshot FindMaster(IReadOnlyList<AudioBusSnapshot> buses)
        {
            for (var index = 0; index < buses.Count; index++)
            {
                if (buses[index].IsMaster) return buses[index];
            }

            return null;
        }

        private static void AppendBuses(StringBuilder builder, IReadOnlyList<AudioBusSnapshot> buses, int limit)
        {
            int count = Math.Min(buses.Count, Math.Max(0, limit));
            builder.Append(",\"buses\":[");
            for (var index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendBus(builder, buses[index]);
            }

            builder.Append("],\"busCount\":").Append(count);
            builder.Append(",\"busTotal\":").Append(buses.Count);
            builder.Append(",\"busesTruncated\":");
            AppendBool(builder, buses.Count > count);
        }

        private static void AppendBus(StringBuilder builder, AudioBusSnapshot bus)
        {
            builder.Append("{\"name\":");
            AppendString(builder, bus.Name, MAX_NAME_LENGTH);
            builder.Append(",\"volume\":");
            AppendFloat(builder, bus.Volume);
            builder.Append(",\"effectiveVolume\":");
            AppendFloat(builder, bus.EffectiveVolume);
            builder.Append(",\"muted\":");
            AppendBool(builder, bus.Muted);
            builder.Append(",\"isMaster\":");
            AppendBool(builder, bus.IsMaster);
            builder.Append(",\"isBuiltIn\":");
            AppendBool(builder, bus.IsBuiltIn);
            builder.Append(",\"isRegistered\":");
            AppendBool(builder, bus.IsRegistered);
            builder.Append(",\"activeVoiceCount\":").Append(bus.ActiveVoiceCount).Append('}');
        }

        private static void AppendVoices(StringBuilder builder, IReadOnlyList<AudioVoiceSnapshot> voices, int limit)
        {
            int count = Math.Min(voices.Count, Math.Max(0, limit));
            builder.Append(",\"voices\":[");
            for (var index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendVoice(builder, voices[index]);
            }

            builder.Append("],\"voiceCount\":").Append(count);
            builder.Append(",\"voiceTotal\":").Append(voices.Count);
            builder.Append(",\"voicesTruncated\":");
            AppendBool(builder, voices.Count > count);
        }

        private static void AppendVoice(StringBuilder builder, AudioVoiceSnapshot voice)
        {
            builder.Append("{\"backendGeneration\":").Append(voice.BackendGeneration);
            builder.Append(",\"voiceId\":").Append(voice.VoiceId).Append(",\"path\":");
            AppendString(builder, voice.Path, MAX_PATH_LENGTH);
            builder.Append(",\"bus\":");
            AppendString(builder, voice.Bus, MAX_NAME_LENGTH);
            builder.Append(",\"backendName\":");
            AppendString(builder, voice.BackendName, MAX_NAME_LENGTH);
            builder.Append(",\"loop\":");
            AppendBool(builder, voice.Loop);
            builder.Append(",\"playing\":");
            AppendBool(builder, voice.IsPlaying);
            builder.Append(",\"paused\":");
            AppendBool(builder, voice.IsPaused);
            builder.Append(",\"volume\":");
            AppendFloat(builder, voice.Volume);
            builder.Append(",\"pitch\":");
            AppendFloat(builder, voice.Pitch);
            builder.Append(",\"duration\":");
            AppendFloat(builder, voice.Duration);
            builder.Append(",\"elapsed\":");
            AppendFloat(builder, voice.Elapsed);
            AppendSpatial(builder, voice);
            builder.Append('}');
        }

        private static void AppendSpatial(StringBuilder builder, AudioVoiceSnapshot voice)
        {
            builder.Append(",\"is3D\":");
            AppendBool(builder, voice.Is3D);
            builder.Append(",\"position\":{\"x\":");
            AppendFloat(builder, voice.Position.X);
            builder.Append(",\"y\":");
            AppendFloat(builder, voice.Position.Y);
            builder.Append(",\"z\":");
            AppendFloat(builder, voice.Position.Z);
            builder.Append('}');
            builder.Append(",\"followTarget\":");
            AppendString(builder, voice.FollowTargetName, MAX_NAME_LENGTH);
            builder.Append(",\"minDistance\":");
            AppendFloat(builder, voice.MinDistance);
            builder.Append(",\"maxDistance\":");
            AppendFloat(builder, voice.MaxDistance);
            builder.Append(",\"rolloffMode\":");
            AppendString(builder, voice.RolloffMode.ToString(), MAX_NAME_LENGTH);
        }

        private static void AppendHistory(
            StringBuilder builder,
            IReadOnlyList<AudioHistoryEntry> history,
            int limit)
        {
            int count = Math.Min(history.Count, Math.Max(0, limit));
            builder.Append(",\"history\":[");
            for (var index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendHistoryEntry(builder, history[index]);
            }

            builder.Append("],\"historyCount\":").Append(count);
            builder.Append(",\"historyTotal\":").Append(AudioKit.HistoryTotalCount);
            builder.Append(",\"historyTruncated\":");
            AppendBool(builder, AudioKit.HistoryTotalCount > count);
        }

        private static void AppendHistoryEntry(StringBuilder builder, AudioHistoryEntry entry)
        {
            builder.Append("{\"sequence\":").Append(entry.Sequence).Append(",\"eventType\":");
            AppendString(builder, entry.EventType, MAX_NAME_LENGTH);
            builder.Append(",\"backendGeneration\":").Append(entry.BackendGeneration);
            builder.Append(",\"voiceId\":").Append(entry.VoiceId).Append(",\"path\":");
            AppendString(builder, entry.Path, MAX_PATH_LENGTH);
            builder.Append(",\"bus\":");
            AppendString(builder, entry.Bus, MAX_NAME_LENGTH);
            builder.Append(",\"volume\":");
            AppendFloat(builder, entry.Volume);
            builder.Append(",\"timestampUtc\":");
            AppendString(builder, entry.TimestampUtc, MAX_NAME_LENGTH);
            builder.Append('}');
        }

        private static void AppendString(StringBuilder builder, string value, int maxLength)
        {
            string safe = value ?? string.Empty;
            if (safe.Length > maxLength) safe = safe.Substring(0, maxLength);
            builder.Append('\"').Append(JsonHelper.EscapeString(safe)).Append('\"');
        }

        private static void AppendBool(StringBuilder builder, bool value) =>
            builder.Append(value ? "true" : "false");

        private static void AppendFloat(StringBuilder builder, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                builder.Append('0');
                return;
            }

            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
#endif
