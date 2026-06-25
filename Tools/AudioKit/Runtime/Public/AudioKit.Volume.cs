using System;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 音量与静音控制 API。
    /// </summary>
    public static partial class AudioKit
    {
        /// <summary>
        /// 设置指定总线音量。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <param name="volume">目标音量，范围会被限制到 0..1。</param>
        public static void SetVolume(string bus, float volume)
        {
            SetBusVolume(bus, volume);
        }

        /// <summary>
        /// 设置指定总线音量。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <param name="volume">目标音量，范围会被限制到 0..1。</param>
        public static void SetBusVolume(string bus, float volume)
        {
            var backend = EnsureBackend();
            var normalizedBus = NormalizeBus(bus);
            var clampedVolume = Clamp01(volume);
            SetStoredBusVolume(normalizedBus, clampedVolume);
            backend.SetBusVolume(normalizedBus, GetEffectiveBusVolume(normalizedBus));
            RecordVolumeChanged(backend.BackendName, normalizedBus, clampedVolume);
        }

        /// <summary>
        /// 获取指定总线的后端音量。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <returns>当前后端总线音量。</returns>
        public static float GetVolume(string bus)
        {
            return EnsureBackend().GetBusVolume(NormalizeBus(bus));
        }

        /// <summary>
        /// 获取指定总线的逻辑音量，静音时返回 0。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <returns>当前逻辑总线音量。</returns>
        public static float GetBusVolume(string bus)
        {
            return GetChannelVolume(bus);
        }

        /// <summary>
        /// 设置指定内置通道音量。
        /// </summary>
        /// <param name="channel">音频通道。</param>
        /// <param name="volume">目标音量，范围会被限制到 0..1。</param>
        public static void SetChannelVolume(AudioChannel channel, float volume)
        {
            SetBusVolume(ToBus(channel), volume);
        }

        /// <summary>
        /// 使用旧版整数通道设置音量。
        /// </summary>
        /// <param name="channelId">旧版通道编号，0-4 对应内置通道，5+ 对应自定义通道。</param>
        /// <param name="volume">目标音量，范围会被限制到 0..1。</param>
        public static void SetChannelVolume(int channelId, float volume)
        {
            SetBusVolume(ToBus(channelId), volume);
        }

        /// <summary>
        /// 获取指定内置通道音量。
        /// </summary>
        /// <param name="channel">音频通道。</param>
        /// <returns>当前逻辑通道音量。</returns>
        public static float GetChannelVolume(AudioChannel channel)
        {
            return GetChannelVolume(ToBus(channel));
        }

        /// <summary>
        /// 使用旧版整数通道获取逻辑音量。
        /// </summary>
        /// <param name="channelId">旧版通道编号，0-4 对应内置通道，5+ 对应自定义通道。</param>
        /// <returns>当前逻辑通道音量。</returns>
        public static float GetChannelVolume(int channelId)
        {
            return GetChannelVolume(ToBus(channelId));
        }

        /// <summary>
        /// 获取指定总线的逻辑音量，静音时返回 0。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <returns>当前逻辑总线音量。</returns>
        public static float GetChannelVolume(string bus)
        {
            var normalizedBus = NormalizeBus(bus);
            return IsBusMuted(normalizedBus) ? 0f : GetStoredBusVolume(normalizedBus);
        }

        /// <summary>
        /// 设置指定内置通道静音状态。
        /// </summary>
        /// <param name="channel">音频通道。</param>
        /// <param name="mute">是否静音。</param>
        public static void MuteChannel(AudioChannel channel, bool mute)
        {
            MuteBus(ToBus(channel), mute);
        }

        /// <summary>
        /// 使用旧版整数通道设置静音状态。
        /// </summary>
        /// <param name="channelId">旧版通道编号，0-4 对应内置通道，5+ 对应自定义通道。</param>
        /// <param name="mute">是否静音。</param>
        public static void MuteChannel(int channelId, bool mute)
        {
            MuteBus(ToBus(channelId), mute);
        }

        /// <summary>
        /// 设置指定总线静音状态。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <param name="mute">是否静音。</param>
        public static void MuteChannel(string bus, bool mute)
        {
            MuteBus(bus, mute);
        }

        /// <summary>
        /// 设置指定总线静音状态。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <param name="mute">是否静音。</param>
        public static void MuteBus(string bus, bool mute)
        {
            var backend = EnsureBackend();
            var normalizedBus = NormalizeBus(bus);
            lock (sLock)
            {
                if (mute)
                    sMutedBuses.Add(normalizedBus);
                else
                    sMutedBuses.Remove(normalizedBus);
            }

            backend.SetBusVolume(normalizedBus, GetEffectiveBusVolume(normalizedBus));
        }

        /// <summary>
        /// 设置全局主音量。
        /// </summary>
        /// <param name="volume">目标音量，范围会被限制到 0..1。</param>
        public static void SetGlobalVolume(float volume)
        {
            var clamped = Clamp01(volume);
            lock (sLock)
                sGlobalVolume = clamped;

            var backend = GetBackend();
            if (backend != null)
                backend.SetBusVolume(AudioBus.Master, GetEffectiveMasterVolume());
        }

        /// <summary>
        /// 获取全局主音量。
        /// </summary>
        /// <returns>当前全局主音量。</returns>
        public static float GetGlobalVolume()
        {
            lock (sLock)
                return sGlobalVolume;
        }

        /// <summary>
        /// 设置全局静音状态。
        /// </summary>
        /// <param name="mute">是否静音。</param>
        public static void MuteAll(bool mute)
        {
            lock (sLock)
                sGlobalMuted = mute;

            var backend = GetBackend();
            if (backend != null)
                backend.SetBusVolume(AudioBus.Master, GetEffectiveMasterVolume());
        }

        /// <summary>
        /// 获取全局静音状态。
        /// </summary>
        /// <returns>全局静音时返回 true。</returns>
        public static bool IsMuted()
        {
            lock (sLock)
                return sGlobalMuted;
        }
    }
}
