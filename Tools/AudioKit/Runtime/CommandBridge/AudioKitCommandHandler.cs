using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 命令处理器：查询当前播放通道、音量和历史事件。
    /// </summary>
    public sealed class AudioKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        /// <inheritdoc />
        public string KitName => "AudioKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "stats",
            "list_voices",
            "list_buses",
            "get_history",
            "get_workbench_snapshot",
            "clear_history",
            "stop_voice",
            "stop_all",
            "stop_bus",
            "set_master_volume",
            "set_bus_volume",
            "mute_master",
            "mute_bus"
        };

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            return GetStats();
        }

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "list_voices":
                    return ListVoices();
                case "list_buses":
                    return ListBuses();
                case "get_history":
                    return GetHistory();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "clear_history":
                    AudioKit.ClearHistory();
                    return "{\"cleared\":true}";
                case "stop_voice":
                    return StopVoice(payloadJson);
                case "stop_all":
                    AudioKit.StopAll();
                    return "{\"stopped\":true}";
                case "stop_bus":
                    return StopBus(payloadJson);
                case "set_master_volume":
                    return SetMasterVolume(payloadJson);
                case "set_bus_volume":
                    return SetBusVolume(payloadJson);
                case "mute_master":
                    return MuteMaster(payloadJson);
                case "mute_bus":
                    return MuteBus(payloadJson);
                default:
                    throw new NotSupportedException($"Unknown AudioKit action '{action}'");
            }
        }

        private static string GetWorkbenchSnapshot()
        {
            // 工作台、AI 和 snapshot publisher 共享同一份 payload，避免每个调用端重复拼装 AudioKit 状态。
            var stats = GetStats();
            var buses = ListBuses();
            var voices = ListVoices();
            var history = GetHistory();

            var sb = new StringBuilder(stats.Length + buses.Length + voices.Length + history.Length + 72);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"buses\":");
            sb.Append(buses);
            sb.Append(",\"voices\":");
            sb.Append(voices);
            sb.Append(",\"history\":");
            sb.Append(history);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetStats()
        {
            var stats = AudioKit.GetStats();
            var sb = new StringBuilder(192);
            sb.Append("{\"backendName\":\"");
            sb.Append(JsonHelper.EscapeString(stats.BackendName));
            sb.Append("\",\"activeVoiceCount\":");
            sb.Append(stats.ActiveVoiceCount);
            sb.Append(",\"historyCount\":");
            sb.Append(stats.HistoryCount);
            sb.Append(",\"masterVolume\":");
            AppendFloat(sb, stats.MasterVolume);
            sb.Append(",\"musicVolume\":");
            AppendFloat(sb, stats.MusicVolume);
            sb.Append(",\"sfxVolume\":");
            AppendFloat(sb, stats.SfxVolume);
            sb.Append(",\"voiceVolume\":");
            AppendFloat(sb, stats.VoiceVolume);
            sb.Append(",\"ambienceVolume\":");
            AppendFloat(sb, stats.AmbienceVolume);
            sb.Append(",\"uiVolume\":");
            AppendFloat(sb, stats.UiVolume);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListBuses()
        {
            var buses = new List<AudioBusDebugInfo>(16);
            AudioKit.GetBuses(buses);

            var sb = new StringBuilder(256);
            sb.Append("{\"buses\":[");
            for (var i = 0; i < buses.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendBus(sb, buses[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(buses.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListVoices()
        {
            var voices = new List<AudioVoiceDebugInfo>(32);
            AudioKit.GetActiveVoices(voices);

            var sb = new StringBuilder(256);
            sb.Append("{\"voices\":[");
            for (var i = 0; i < voices.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendVoice(sb, voices[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(voices.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetHistory()
        {
            var history = new List<AudioHistoryRecord>(128);
            AudioKit.GetHistory(history);

            var sb = new StringBuilder(256);
            sb.Append("{\"history\":[");
            for (var i = 0; i < history.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendHistory(sb, history[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(history.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string StopVoice(string payloadJson)
        {
            if (!TryExtractInt(payloadJson, "voiceId", out var voiceId) && !TryExtractInt(payloadJson, "id", out voiceId))
                throw new ArgumentException("Missing numeric 'voiceId' in payload");

            var stopped = AudioKit.Stop(voiceId);
            return "{\"stopped\":" + (stopped ? "true" : "false") + ",\"voiceId\":" + voiceId + "}";
        }

        private static string StopBus(string payloadJson)
        {
            var bus = ExtractBus(payloadJson);
            if (string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                AudioKit.StopAll();
            }
            else
            {
                AudioKit.StopBus(bus);
            }

            return "{\"stopped\":true,\"bus\":\"" + JsonHelper.EscapeString(bus) + "\"}";
        }

        private static string SetMasterVolume(string payloadJson)
        {
            if (!TryExtractFloat(payloadJson, "volume", out var volume))
                throw new ArgumentException("Missing numeric 'volume' in payload");

            AudioKit.SetGlobalVolume(volume);
            return "{\"bus\":\"Master\",\"volume\":" + FormatFloat(AudioKit.GetGlobalVolume()) + "}";
        }

        private static string SetBusVolume(string payloadJson)
        {
            var bus = ExtractBus(payloadJson);
            if (!TryExtractFloat(payloadJson, "volume", out var volume))
                throw new ArgumentException("Missing numeric 'volume' in payload");

            if (string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                AudioKit.SetGlobalVolume(volume);
                return "{\"bus\":\"Master\",\"volume\":" + FormatFloat(AudioKit.GetGlobalVolume()) + "}";
            }

            AudioKit.SetBusVolume(bus, volume);
            return "{\"bus\":\"" + JsonHelper.EscapeString(bus) + "\",\"volume\":" + FormatFloat(AudioKit.GetBusVolume(bus)) + "}";
        }

        private static string MuteMaster(string payloadJson)
        {
            var muted = ExtractBool(payloadJson, "muted", true);
            AudioKit.MuteAll(muted);
            return "{\"bus\":\"Master\",\"muted\":" + (AudioKit.IsMuted() ? "true" : "false") + "}";
        }

        private static string MuteBus(string payloadJson)
        {
            var bus = ExtractBus(payloadJson);
            var muted = ExtractBool(payloadJson, "muted", true);
            if (string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                AudioKit.MuteAll(muted);
                return "{\"bus\":\"Master\",\"muted\":" + (AudioKit.IsMuted() ? "true" : "false") + "}";
            }

            AudioKit.MuteBus(bus, muted);
            return "{\"bus\":\"" + JsonHelper.EscapeString(bus) + "\",\"muted\":" + (muted ? "true" : "false") + "}";
        }

        private static string ExtractBus(string payloadJson)
        {
            var bus = JsonHelper.ExtractString(payloadJson ?? string.Empty, "bus");
            if (string.IsNullOrEmpty(bus))
                bus = JsonHelper.ExtractString(payloadJson ?? string.Empty, "name");
            if (string.IsNullOrEmpty(bus))
                throw new ArgumentException("Missing string 'bus' in payload");

            return bus;
        }

        private static bool ExtractBool(string json, string fieldName, bool fallback)
        {
            bool value;
            return JsonHelper.TryExtractBool(json ?? string.Empty, fieldName, out value) ? value : fallback;
        }

        private static bool TryExtractInt(string json, string fieldName, out int value)
        {
            value = 0;

            var stringValue = JsonHelper.ExtractString(json, fieldName);
            if (!string.IsNullOrEmpty(stringValue))
                return int.TryParse(stringValue, out value);

            if (string.IsNullOrEmpty(json))
                return false;

            var search = $"\"{fieldName}\"";
            var index = json.IndexOf(search, StringComparison.Ordinal);
            if (index < 0)
                return false;

            index += search.Length;
            while (index < json.Length && (json[index] == ' ' || json[index] == ':' || json[index] == '\t' || json[index] == '\r' || json[index] == '\n'))
                index++;

            var start = index;
            if (index < json.Length && json[index] == '-')
                index++;

            while (index < json.Length && char.IsDigit(json[index]))
                index++;

            if (index <= start)
                return false;

            return int.TryParse(json.Substring(start, index - start), out value);
        }

        private static bool TryExtractFloat(string json, string fieldName, out float value)
        {
            value = 0f;

            var stringValue = JsonHelper.ExtractString(json ?? string.Empty, fieldName);
            if (!string.IsNullOrEmpty(stringValue))
                return float.TryParse(stringValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);

            if (string.IsNullOrEmpty(json))
                return false;

            var search = $"\"{fieldName}\"";
            var index = json.IndexOf(search, StringComparison.Ordinal);
            if (index < 0)
                return false;

            index += search.Length;
            while (index < json.Length && (json[index] == ' ' || json[index] == ':' || json[index] == '\t' || json[index] == '\r' || json[index] == '\n'))
                index++;

            var start = index;
            if (index < json.Length && json[index] == '-')
                index++;

            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.'))
                index++;

            if (index <= start)
                return false;

            return float.TryParse(json.Substring(start, index - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static void AppendBus(StringBuilder sb, AudioBusDebugInfo bus)
        {
            sb.Append("{\"name\":\"");
            sb.Append(JsonHelper.EscapeString(bus.Name));
            sb.Append("\",\"volume\":");
            AppendFloat(sb, bus.Volume);
            sb.Append(",\"effectiveVolume\":");
            AppendFloat(sb, bus.EffectiveVolume);
            sb.Append(",\"muted\":");
            sb.Append(bus.Muted ? "true" : "false");
            sb.Append(",\"isMaster\":");
            sb.Append(bus.IsMaster ? "true" : "false");
            sb.Append(",\"isDefault\":");
            sb.Append(bus.IsDefault ? "true" : "false");
            sb.Append(",\"activeVoiceCount\":");
            sb.Append(bus.ActiveVoiceCount);
            sb.Append('}');
        }

        private static void AppendVoice(StringBuilder sb, AudioVoiceDebugInfo voice)
        {
            sb.Append("{\"voiceId\":");
            sb.Append(voice.VoiceId);
            sb.Append(",\"path\":\"");
            sb.Append(JsonHelper.EscapeString(voice.Path));
            sb.Append("\",\"clipName\":\"");
            sb.Append(JsonHelper.EscapeString(voice.ClipName));
            sb.Append("\",\"bus\":\"");
            sb.Append(JsonHelper.EscapeString(voice.Bus));
            sb.Append("\",\"backendName\":\"");
            sb.Append(JsonHelper.EscapeString(voice.BackendName));
            sb.Append("\",\"loop\":");
            sb.Append(voice.Loop ? "true" : "false");
            sb.Append(",\"isPlaying\":");
            sb.Append(voice.IsPlaying ? "true" : "false");
            sb.Append(",\"volume\":");
            AppendFloat(sb, voice.Volume);
            sb.Append(",\"pitch\":");
            AppendFloat(sb, voice.Pitch);
            sb.Append(",\"startedAt\":");
            AppendFloat(sb, voice.StartedAt);
            sb.Append(",\"duration\":");
            AppendFloat(sb, voice.Duration);
            sb.Append(",\"elapsed\":");
            AppendFloat(sb, voice.Elapsed);
            sb.Append(",\"is3D\":");
            sb.Append(voice.Is3D ? "true" : "false");
            sb.Append(",\"position\":");
            AppendVector3(sb, voice.Position);
            sb.Append(",\"hasFollowTarget\":");
            sb.Append(voice.HasFollowTarget ? "true" : "false");
            sb.Append(",\"followTargetName\":\"");
            sb.Append(JsonHelper.EscapeString(voice.FollowTargetName));
            sb.Append('"');
            sb.Append(",\"minDistance\":");
            AppendFloat(sb, voice.MinDistance);
            sb.Append(",\"maxDistance\":");
            AppendFloat(sb, voice.MaxDistance);
            sb.Append(",\"rolloffMode\":\"");
            sb.Append(JsonHelper.EscapeString(voice.RolloffMode.ToString()));
            sb.Append('"');
            sb.Append('}');
        }

        private static void AppendHistory(StringBuilder sb, AudioHistoryRecord item)
        {
            sb.Append("{\"eventType\":\"");
            sb.Append(JsonHelper.EscapeString(item.EventType));
            sb.Append("\",\"voiceId\":");
            sb.Append(item.VoiceId);
            sb.Append(",\"path\":\"");
            sb.Append(JsonHelper.EscapeString(item.Path));
            sb.Append("\",\"clipName\":\"");
            sb.Append(JsonHelper.EscapeString(item.ClipName));
            sb.Append("\",\"bus\":\"");
            sb.Append(JsonHelper.EscapeString(item.Bus));
            sb.Append("\",\"backendName\":\"");
            sb.Append(JsonHelper.EscapeString(item.BackendName));
            sb.Append("\",\"volume\":");
            AppendFloat(sb, item.Volume);
            sb.Append(",\"pitch\":");
            AppendFloat(sb, item.Pitch);
            sb.Append(",\"loop\":");
            sb.Append(item.Loop ? "true" : "false");
            sb.Append(",\"is3D\":");
            sb.Append(item.Is3D ? "true" : "false");
            sb.Append(",\"position\":");
            AppendVector3(sb, item.Position);
            sb.Append(",\"hasFollowTarget\":");
            sb.Append(item.HasFollowTarget ? "true" : "false");
            sb.Append(",\"followTargetName\":\"");
            sb.Append(JsonHelper.EscapeString(item.FollowTargetName));
            sb.Append('"');
            sb.Append(",\"minDistance\":");
            AppendFloat(sb, item.MinDistance);
            sb.Append(",\"maxDistance\":");
            AppendFloat(sb, item.MaxDistance);
            sb.Append(",\"rolloffMode\":\"");
            sb.Append(JsonHelper.EscapeString(item.RolloffMode.ToString()));
            sb.Append('"');
            sb.Append(",\"timestampUtc\":\"");
            sb.Append(JsonHelper.EscapeString(item.TimestampUtc));
            sb.Append("\"}");
        }

        private static void AppendVector3(StringBuilder sb, YokiVector3 value)
        {
            sb.Append("{\"x\":");
            AppendFloat(sb, value.X);
            sb.Append(",\"y\":");
            AppendFloat(sb, value.Y);
            sb.Append(",\"z\":");
            AppendFloat(sb, value.Z);
            sb.Append('}');
        }

        private static void AppendFloat(StringBuilder sb, float value)
        {
            sb.Append(FormatFloat(value));
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
