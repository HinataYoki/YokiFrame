using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 内部状态规范化和工具方法。
    /// </summary>
    public static partial class AudioKit
    {
        private static IAudioBackend EnsureBackend()
        {
            var backend = GetBackend();
            if (backend == null)
                throw new InvalidOperationException("AudioKit backend is not configured. Call AudioKit.SetBackend from an engine adapter first.");

            return backend;
        }

        private static AudioPlayOptions NormalizeOptions(AudioPlayOptions options)
        {
            // 所有默认值和非法值修正都集中在 AudioKit，后端只接收已经规范化的跨引擎语义。
            if (string.IsNullOrEmpty(options.Bus))
                options.Bus = AudioBus.Sfx;
            else
                options.Bus = NormalizeBus(options.Bus);

            if (options.Volume <= 0f)
                options.Volume = 1f;
            else
                options.Volume = Clamp01(options.Volume);

            if (options.Pitch <= 0f)
                options.Pitch = 1f;

            if (options.FadeInDuration < 0f)
                options.FadeInDuration = 0f;

            if (options.FadeOutDuration < 0f)
                options.FadeOutDuration = 0f;

            if (options.MinDistance <= 0f)
                options.MinDistance = 1f;

            if (options.MaxDistance <= 0f)
                options.MaxDistance = 500f;

            if (options.MaxDistance < options.MinDistance)
                options.MaxDistance = options.MinDistance;

            if (!IsKnownRolloffMode(options.RolloffMode))
                options.RolloffMode = AudioRolloffMode.Logarithmic;

            if (options.FollowTarget != null)
            {
                options.Is3D = true;
                options.Position = options.FollowTarget.Position;
            }

            return options;
        }

        private static string ResolvePath(int audioId)
        {
            Func<int, string> resolver;
            lock (sLock)
                resolver = sPathResolver;

            if (resolver != null)
                return resolver(audioId);

            return "Audio/" + audioId;
        }

        private static string ToBus(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music:
                    return AudioBus.Music;
                case AudioChannel.Sfx:
                    return AudioBus.Sfx;
                case AudioChannel.Voice:
                    return AudioBus.Voice;
                case AudioChannel.Ambience:
                    return AudioBus.Ambience;
                case AudioChannel.UI:
                    return AudioBus.UI;
                default:
                    return AudioBus.Sfx;
            }
        }

        private static string ToBus(int channelId)
        {
            switch (channelId)
            {
                case 0:
                    return AudioBus.Music;
                case 1:
                    return AudioBus.Sfx;
                case 2:
                    return AudioBus.Voice;
                case 3:
                    return AudioBus.Ambience;
                case 4:
                    return AudioBus.UI;
                default:
                    return "Channel_" + channelId;
            }
        }

        private static string NormalizeBus(string bus)
        {
            return string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;

            return value;
        }

        private static void SyncBackendState(IAudioBackend backend)
        {
            if (backend == null)
                return;

            backend.SetBusVolume(AudioBus.Master, GetEffectiveMasterVolume());
            backend.SetBusVolume(AudioBus.Music, GetEffectiveBusVolume(AudioBus.Music));
            backend.SetBusVolume(AudioBus.Sfx, GetEffectiveBusVolume(AudioBus.Sfx));
            backend.SetBusVolume(AudioBus.Voice, GetEffectiveBusVolume(AudioBus.Voice));
            backend.SetBusVolume(AudioBus.Ambience, GetEffectiveBusVolume(AudioBus.Ambience));
            backend.SetBusVolume(AudioBus.UI, GetEffectiveBusVolume(AudioBus.UI));

            string[] customBuses;
            lock (sLock)
            {
                customBuses = new string[sBusVolumes.Count];
                sBusVolumes.Keys.CopyTo(customBuses, 0);
            }

            for (var i = 0; i < customBuses.Length; i++)
                backend.SetBusVolume(customBuses[i], GetEffectiveBusVolume(customBuses[i]));
        }

        private static void SetStoredBusVolume(string bus, float volume)
        {
            lock (sLock)
                sBusVolumes[bus] = volume;
        }

        private static float GetStoredBusVolume(string bus)
        {
            lock (sLock)
            {
                float volume;
                return sBusVolumes.TryGetValue(bus, out volume) ? volume : 1f;
            }
        }

        private static bool IsBusMuted(string bus)
        {
            lock (sLock)
                return sMutedBuses.Contains(bus);
        }

        private static bool IsDebugMuted(string bus)
        {
            return string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase) ? IsMuted() : IsBusMuted(bus);
        }

        private static float GetStoredDebugVolume(string bus)
        {
            return string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase) ? GetGlobalVolume() : GetStoredBusVolume(bus);
        }

        private static float GetEffectiveDebugVolume(string bus)
        {
            return string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase) ? GetEffectiveMasterVolume() : GetEffectiveBusVolume(bus);
        }

        private static float GetEffectiveBusVolume(string bus)
        {
            return IsBusMuted(bus) ? 0f : GetStoredBusVolume(bus);
        }

        private static float GetEffectiveMasterVolume()
        {
            lock (sLock)
                return sGlobalMuted ? 0f : sGlobalVolume;
        }

        private static AudioVoiceDebugInfo FindVoice(List<AudioVoiceDebugInfo> active, int voiceId)
        {
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].VoiceId == voiceId)
                    return active[i];
            }

            return null;
        }

        private static bool IsKnownRolloffMode(AudioRolloffMode mode)
        {
            return mode == AudioRolloffMode.Logarithmic
                || mode == AudioRolloffMode.Linear
                || mode == AudioRolloffMode.Custom;
        }

        private static void AddBusName(List<string> busNames, string bus)
        {
            if (string.IsNullOrEmpty(bus))
                return;

            for (var i = 0; i < busNames.Count; i++)
            {
                if (string.Equals(busNames[i], bus, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            busNames.Add(bus);
        }

        private static int CompareBusNames(string left, string right)
        {
            var leftOrder = GetDefaultBusOrder(left);
            var rightOrder = GetDefaultBusOrder(right);
            if (leftOrder != rightOrder)
                return leftOrder.CompareTo(rightOrder);

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetDefaultBusOrder(string bus)
        {
            if (string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(bus, AudioBus.Music, StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(bus, AudioBus.Sfx, StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(bus, AudioBus.Voice, StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(bus, AudioBus.Ambience, StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(bus, AudioBus.UI, StringComparison.OrdinalIgnoreCase)) return 5;
            return 100;
        }

        private static bool IsDefaultBus(string bus)
        {
            return GetDefaultBusOrder(bus) < 100;
        }

        private static int CountActiveVoices(List<AudioVoiceDebugInfo> active, string bus)
        {
            var count = 0;
            for (var i = 0; i < active.Count; i++)
            {
                if (string.Equals(active[i].Bus, bus, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }

        private static bool StopWithFadeCore(IAudioBackend backend, AudioVoiceDebugInfo beforeStop, int voiceId, float fadeDuration)
        {
            var normalizedDuration = fadeDuration < 0f ? 0f : fadeDuration;
            var stopped = backend.StopWithFade(voiceId, normalizedDuration);
            if (!stopped)
                return false;

            var info = beforeStop ?? new AudioVoiceDebugInfo { VoiceId = voiceId, BackendName = backend.BackendName };
            Record(normalizedDuration > 0f ? "play_stop_requested" : "play_stopped", info);
            return true;
        }
    }
}
