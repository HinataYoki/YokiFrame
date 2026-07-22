#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        private const int MAX_HISTORY_COUNT = 128;
        private static readonly Queue<AudioHistoryEntry> sHistory = new(MAX_HISTORY_COUNT);
        private static long sDiagnosticVersion;
        private static long sHistorySequence;

        /// <summary>获取 AudioKit 工具状态的单调版本。</summary>
        public static long DiagnosticVersion
        {
            get
            {
                lock (sLock) return sDiagnosticVersion;
            }
        }

        /// <summary>获取当前会话累计写入的历史数量，用于识别有界队列裁剪。</summary>
        public static long HistoryTotalCount
        {
            get
            {
                lock (sLock) return sHistorySequence;
            }
        }

        /// <summary>复制当前 active voice 状态；读取不会创建默认后端。</summary>
        public static void GetActiveVoices(List<AudioVoiceSnapshot> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            IAudioBackend backend;
            long generation;
            lock (sLock)
            {
                backend = sBackend;
                generation = sBackendGeneration;
            }

            if (backend == null) return;
            backend.GetActiveVoices(result);
            for (var index = 0; index < result.Count; index++)
            {
                result[index].BackendGeneration = generation;
            }
        }

        /// <summary>按最新优先顺序复制有界历史；锁内直接枚举，避免中间 ToArray 分配。</summary>
        public static void GetHistory(List<AudioHistoryEntry> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            lock (sLock)
            {
                // Queue 枚举顺序为最旧→最新；先按该顺序填入，再原地反转为最新优先。
                foreach (AudioHistoryEntry entry in sHistory)
                {
                    result.Add(entry);
                }

                result.Reverse();
            }
        }

        /// <summary>清空工具历史并推进诊断版本。</summary>
        public static void ClearHistory()
        {
            lock (sLock)
            {
                sHistory.Clear();
                BumpDiagnosticVersionLocked();
            }
        }

        /// <summary>创建默认和动态逻辑总线的诊断列表；内部会再抓取一次 active voices。</summary>
        public static void GetBuses(List<AudioBusSnapshot> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            List<AudioVoiceSnapshot> voices = new(32);
            GetActiveVoices(voices);
            GetBuses(result, voices);
        }

        /// <summary>使用调用方已捕获的 active voices 组装总线列表，避免 snapshot 路径双重抓取。</summary>
        public static void GetBuses(List<AudioBusSnapshot> result, List<AudioVoiceSnapshot> voices)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (voices == null) throw new ArgumentNullException(nameof(voices));
            result.Clear();
            List<string> buses = new(16);
            CollectBusNames(buses, voices);
            buses.Sort(CompareBusNames);
            for (var index = 0; index < buses.Count; index++)
            {
                result.Add(CreateBusSnapshot(buses[index], voices));
            }
        }

        /// <summary>把默认、配置、静音和 active voice 总线合并为唯一名称集合。</summary>
        private static void CollectBusNames(List<string> buses, List<AudioVoiceSnapshot> voices)
        {
            AddBusName(buses, AudioBus.Master);
            AddDefaultBuses(buses);
            lock (sLock)
            {
                foreach (string bus in sRegisteredBuses) AddBusName(buses, bus);
                foreach (string bus in sBusVolumes.Keys) AddBusName(buses, bus);
                foreach (string bus in sMutedBuses) AddBusName(buses, bus);
            }

            for (var index = 0; index < voices.Count; index++) AddBusName(buses, voices[index].Bus);
        }

        /// <summary>创建单个逻辑总线诊断状态。</summary>
        private static AudioBusSnapshot CreateBusSnapshot(string bus, List<AudioVoiceSnapshot> voices)
        {
            bool master = string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase);
            return new AudioBusSnapshot
            {
                Name = bus,
                Volume = master ? GetGlobalVolume() : GetStoredBusVolume(bus),
                EffectiveVolume = master ? GetEffectiveMasterVolume() : GetEffectiveBusVolume(bus),
                Muted = master ? IsMuted() : IsBusMuted(bus),
                IsMaster = master,
                IsBuiltIn = IsBuiltInBus(bus),
                IsRegistered = IsBusRegistered(bus),
                ActiveVoiceCount = master ? voices.Count : CountVoices(voices, bus)
            };
        }

        /// <summary>统计指定逻辑总线的 active voice 数量。</summary>
        private static int CountVoices(List<AudioVoiceSnapshot> voices, string bus)
        {
            var count = 0;
            for (var index = 0; index < voices.Count; index++)
            {
                if (string.Equals(voices[index].Bus, bus, StringComparison.OrdinalIgnoreCase)) count++;
            }

            return count;
        }

        /// <summary>让 Master 和默认总线稳定排列在自定义总线之前。</summary>
        private static int CompareBusNames(string left, string right)
        {
            int leftOrder = GetBusOrder(left);
            int rightOrder = GetBusOrder(right);
            return leftOrder == rightOrder
                ? string.Compare(left, right, StringComparison.OrdinalIgnoreCase)
                : leftOrder.CompareTo(rightOrder);
        }

        /// <summary>返回默认总线稳定排序值，自定义总线返回一百。</summary>
        private static int GetBusOrder(string bus)
        {
            if (string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(bus, AudioBus.Music, StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(bus, AudioBus.Sfx, StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(bus, AudioBus.Voice, StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(bus, AudioBus.Ambience, StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(bus, AudioBus.UI, StringComparison.OrdinalIgnoreCase)) return 5;
            return 100;
        }

        /// <summary>记录一次成功播放及其规范化参数。</summary>
        private static void RecordPlayback(
            string eventType,
            AudioVoiceHandle handle,
            string path,
            AudioPlayOptions options)
        {
            if (!handle.IsValid) return;
            EnqueueHistory(new AudioHistoryEntry
            {
                EventType = eventType,
                BackendGeneration = handle.BackendGeneration,
                VoiceId = handle.VoiceId,
                Path = path,
                Bus = options.Bus,
                Volume = options.Volume
            });
        }

        /// <summary>记录停止、总线停止或全停控制事件。</summary>
        private static void RecordControl(string eventType, AudioVoiceHandle handle, string bus)
        {
            EnqueueHistory(new AudioHistoryEntry
            {
                EventType = eventType,
                BackendGeneration = handle.BackendGeneration,
                VoiceId = handle.VoiceId,
                Bus = bus
            });
        }

        /// <summary>记录逻辑总线配置音量变化。</summary>
        private static void RecordVolume(string bus, float volume)
        {
            EnqueueHistory(new AudioHistoryEntry
            {
                EventType = "volume_changed",
                Bus = bus,
                Volume = volume
            });
        }

        /// <summary>为记录补齐序号和 UTC 时间并维持固定容量。</summary>
        private static void EnqueueHistory(AudioHistoryEntry entry)
        {
            lock (sLock)
            {
                entry.Sequence = ++sHistorySequence;
                entry.TimestampUtc = DateTime.UtcNow.ToString("O");
                while (sHistory.Count >= MAX_HISTORY_COUNT) sHistory.Dequeue();
                sHistory.Enqueue(entry);
                BumpDiagnosticVersionLocked();
            }
        }

        /// <summary>在任意调用点安全推进诊断版本。</summary>
        private static void BumpDiagnosticVersion()
        {
            lock (sLock) BumpDiagnosticVersionLocked();
        }

        /// <summary>由宿主后端在自然结束等非门面状态变化后通知 Tools 刷新；Player 不编译此入口。</summary>
        internal static void NotifyBackendDiagnosticStateChanged()
        {
            BumpDiagnosticVersion();
        }

        /// <summary>在状态锁内推进非零单调诊断版本。</summary>
        private static void BumpDiagnosticVersionLocked()
        {
            sDiagnosticVersion++;
            if (sDiagnosticVersion <= 0) sDiagnosticVersion = 1;
        }

        /// <summary>在状态锁内重置工具诊断状态。</summary>
        private static void ResetDiagnosticsLocked()
        {
            sHistory.Clear();
            sHistorySequence = 0;
            BumpDiagnosticVersionLocked();
        }
    }
}
#endif
