using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 运行时更新与诊断查询 API。
    /// </summary>
    public static partial class AudioKit
    {
        /// <summary>
        /// 推进音频后端的运行时更新。
        /// </summary>
        /// <param name="deltaTime">宿主帧间隔，单位秒。</param>
        public static void Update(float deltaTime)
        {
            var backend = GetBackend();
            if (backend == null)
                return;

            backend.Update(deltaTime);
        }

        /// <summary>
        /// 获取当前活跃 voice 列表。
        /// </summary>
        /// <param name="result">接收结果的列表；方法会先清空该列表。</param>
        public static void GetActiveVoices(List<AudioVoiceDebugInfo> result)
        {
            if (result == null)
                return;

            result.Clear();
            var backend = GetBackend();
            if (backend == null)
                return;

            backend.GetActiveVoices(result);
        }

        /// <summary>
        /// 获取最近音频事件历史。
        /// </summary>
        /// <param name="result">接收结果的列表；方法会先清空该列表。</param>
        public static void GetHistory(List<AudioHistoryRecord> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                var records = sHistory.ToArray();
                for (var i = records.Length - 1; i >= 0; i--)
                    result.Add(records[i]);
            }
        }

        /// <summary>
        /// 获取当前已知音频总线诊断信息。
        /// </summary>
        /// <param name="result">接收结果的列表；方法会先清空该列表。</param>
        public static void GetBuses(List<AudioBusDebugInfo> result)
        {
            if (result == null)
                return;

            result.Clear();

            var active = ListPool<AudioVoiceDebugInfo>.Get();
            var busNames = ListPool<string>.Get();
            try
            {
                GetActiveVoices(active);

                AddBusName(busNames, AudioBus.Master);
                AddBusName(busNames, AudioBus.Music);
                AddBusName(busNames, AudioBus.Sfx);
                AddBusName(busNames, AudioBus.Voice);
                AddBusName(busNames, AudioBus.Ambience);
                AddBusName(busNames, AudioBus.UI);

                lock (sLock)
                {
                    foreach (var bus in sBusVolumes.Keys)
                        AddBusName(busNames, bus);

                    foreach (var bus in sMutedBuses)
                        AddBusName(busNames, bus);

                    foreach (var item in sHistory)
                        AddBusName(busNames, item.Bus);
                }

                for (var i = 0; i < active.Count; i++)
                    AddBusName(busNames, active[i].Bus);

                busNames.Sort(CompareBusNames);

                for (var i = 0; i < busNames.Count; i++)
                {
                    var bus = busNames[i];
                    result.Add(new AudioBusDebugInfo
                    {
                        Name = bus,
                        Volume = GetStoredDebugVolume(bus),
                        EffectiveVolume = GetEffectiveDebugVolume(bus),
                        Muted = IsDebugMuted(bus),
                        IsMaster = string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase),
                        IsDefault = IsDefaultBus(bus),
                        ActiveVoiceCount = CountActiveVoices(active, bus)
                    });
                }
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
                ListPool<string>.Release(busNames);
            }
        }

        /// <summary>
        /// 清空最近音频事件历史。
        /// </summary>
        public static void ClearHistory()
        {
            lock (sLock)
                sHistory.Clear();
        }

        /// <summary>
        /// 获取 AudioKit 当前运行统计。
        /// </summary>
        /// <returns>AudioKit 统计快照。</returns>
        public static AudioKitStats GetStats()
        {
            var backend = GetBackend();
            var active = ListPool<AudioVoiceDebugInfo>.Get();
            try
            {
                if (backend != null)
                    backend.GetActiveVoices(active);

                lock (sLock)
                {
                    return new AudioKitStats
                    {
                        BackendName = backend != null ? backend.BackendName : "None",
                        ActiveVoiceCount = active.Count,
                        HistoryCount = sHistory.Count,
                        MasterVolume = backend != null ? backend.GetBusVolume(AudioBus.Master) : GetEffectiveMasterVolume(),
                        MusicVolume = backend != null ? backend.GetBusVolume(AudioBus.Music) : GetEffectiveBusVolume(AudioBus.Music),
                        SfxVolume = backend != null ? backend.GetBusVolume(AudioBus.Sfx) : GetEffectiveBusVolume(AudioBus.Sfx),
                        VoiceVolume = backend != null ? backend.GetBusVolume(AudioBus.Voice) : GetEffectiveBusVolume(AudioBus.Voice),
                        AmbienceVolume = backend != null ? backend.GetBusVolume(AudioBus.Ambience) : GetEffectiveBusVolume(AudioBus.Ambience),
                        UiVolume = backend != null ? backend.GetBusVolume(AudioBus.UI) : GetEffectiveBusVolume(AudioBus.UI)
                    };
                }
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
            }
        }
        private static void Record(string eventType, AudioVoiceDebugInfo info)
        {
            if (info == null)
                return;

            EnqueueHistory(new AudioHistoryRecord
            {
                EventType = eventType,
                VoiceId = info.VoiceId,
                Path = info.Path,
                ClipName = info.ClipName,
                Bus = info.Bus,
                BackendName = info.BackendName,
                Volume = info.Volume,
                Pitch = info.Pitch,
                FadeOutDuration = info.FadeOutDuration,
                Loop = info.Loop,
                Is3D = info.Is3D,
                Position = info.Position,
                HasFollowTarget = info.HasFollowTarget,
                FollowTargetName = info.FollowTargetName,
                MinDistance = info.MinDistance,
                MaxDistance = info.MaxDistance,
                RolloffMode = info.RolloffMode,
                TimestampUtc = DateTime.UtcNow.ToString("O")
            });
        }

        private static void RecordVolumeChanged(string backendName, string bus, float volume)
        {
            EnqueueHistory(new AudioHistoryRecord
            {
                EventType = "volume_changed",
                Bus = bus,
                BackendName = backendName,
                Volume = volume,
                TimestampUtc = DateTime.UtcNow.ToString("O")
            });
        }

        private static void EnqueueHistory(AudioHistoryRecord record)
        {
            lock (sLock)
            {
                while (sHistory.Count >= MAX_HISTORY)
                    sHistory.Dequeue();

                sHistory.Enqueue(record);
            }
        }
    }
}
