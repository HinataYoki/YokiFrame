using System;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        /// <summary>保存 Master 音量并在后端已存在时立即应用。</summary>
        public static void SetGlobalVolume(float volume)
        {
            float normalized = NormalizeUnitValue(volume, nameof(volume));
            IAudioBackend backend;
            lock (sLock)
            {
                sMasterVolume = normalized;
                backend = sBackend;
            }

            if (backend != null) backend.SetBusVolume(AudioBus.Master, GetEffectiveMasterVolume());
#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordVolume(AudioBus.Master, normalized);
#endif
        }

        /// <summary>获取保存的 Master 配置音量，不受静音状态影响。</summary>
        public static float GetGlobalVolume()
        {
            lock (sLock) return sMasterVolume;
        }

        /// <summary>设置 Master 静音并在后端已存在时立即应用。</summary>
        public static void MuteAll(bool muted)
        {
            IAudioBackend backend;
            lock (sLock)
            {
                sMasterMuted = muted;
                backend = sBackend;
            }

            if (backend != null) backend.SetBusVolume(AudioBus.Master, GetEffectiveMasterVolume());
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>获取当前 Master 是否静音。</summary>
        public static bool IsMuted()
        {
            lock (sLock) return sMasterMuted;
        }

        /// <summary>保存普通逻辑总线音量，不会仅因配置而创建默认后端。</summary>
        public static void SetBusVolume(string bus, float volume)
        {
            string normalizedBus = NormalizeBus(bus, AudioBus.Sfx);
            if (string.Equals(normalizedBus, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                SetGlobalVolume(volume);
                return;
            }

            float normalizedVolume = NormalizeUnitValue(volume, nameof(volume));
            IAudioBackend backend;
            lock (sLock)
            {
                sBusVolumes[normalizedBus] = normalizedVolume;
                backend = sBackend;
            }

            if (backend != null) backend.SetBusVolume(normalizedBus, GetEffectiveBusVolume(normalizedBus));
#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordVolume(normalizedBus, normalizedVolume);
#endif
        }

        /// <summary>获取普通总线逻辑音量；静音时返回零。</summary>
        public static float GetBusVolume(string bus)
        {
            string normalizedBus = NormalizeBus(bus, AudioBus.Sfx);
            if (string.Equals(normalizedBus, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                return GetEffectiveMasterVolume();
            }

            return GetEffectiveBusVolume(normalizedBus);
        }

        /// <summary>设置普通总线静音并保留其配置音量。</summary>
        public static void MuteBus(string bus, bool muted)
        {
            string normalizedBus = NormalizeBus(bus, AudioBus.Sfx);
            if (string.Equals(normalizedBus, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                MuteAll(muted);
                return;
            }

            IAudioBackend backend;
            lock (sLock)
            {
                if (muted) sMutedBuses.Add(normalizedBus);
                else sMutedBuses.Remove(normalizedBus);
                backend = sBackend;
            }

            if (backend != null) backend.SetBusVolume(normalizedBus, GetEffectiveBusVolume(normalizedBus));
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>获取普通逻辑总线是否静音。</summary>
        public static bool IsBusMuted(string bus)
        {
            string normalizedBus = NormalizeBus(bus, AudioBus.Sfx);
            if (string.Equals(normalizedBus, AudioBus.Master, StringComparison.OrdinalIgnoreCase)) return IsMuted();
            lock (sLock) return sMutedBuses.Contains(normalizedBus);
        }

        /// <summary>读取考虑静音后的 Master 有效音量。</summary>
        private static float GetEffectiveMasterVolume()
        {
            lock (sLock) return sMasterMuted ? 0f : sMasterVolume;
        }

        /// <summary>读取考虑静音后的普通总线有效音量。</summary>
        private static float GetEffectiveBusVolume(string bus)
        {
            lock (sLock)
            {
                if (sMutedBuses.Contains(bus)) return 0f;
                return sBusVolumes.TryGetValue(bus, out float volume) ? volume : 1f;
            }
        }
    }
}
