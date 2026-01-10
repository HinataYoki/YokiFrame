using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit - 通道控制
    /// </summary>
    public static partial class AudioKit
    {
        #region 通道控制

        /// <summary>
        /// 设置通道音量（内置通道）
        /// </summary>
        public static void SetChannelVolume(AudioChannel channel, float volume)
        {
            SetChannelVolume((int)channel, volume);
        }

        /// <summary>
        /// 设置通道音量（支持自定义通道 ID，5+ 为用户自定义）
        /// </summary>
        public static void SetChannelVolume(int channelId, float volume)
        {
            sChannelVolumes[channelId] = Mathf.Clamp01(volume);

            // 更新后端
            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.SetChannelVolume(channelId, sChannelVolumes[channelId]);
            }
        }

        /// <summary>
        /// 获取通道音量（内置通道）
        /// </summary>
        public static float GetChannelVolume(AudioChannel channel)
        {
            return GetChannelVolume((int)channel);
        }

        /// <summary>
        /// 获取通道音量（支持自定义通道 ID）
        /// </summary>
        public static float GetChannelVolume(int channelId)
        {
            if (sChannelMuted.TryGetValue(channelId, out var muted) && muted) return 0f;
            return sChannelVolumes.TryGetValue(channelId, out var volume) ? volume : 1f;
        }

        /// <summary>
        /// 静音/取消静音通道（内置通道）
        /// </summary>
        public static void MuteChannel(AudioChannel channel, bool mute)
        {
            MuteChannel((int)channel, mute);
        }

        /// <summary>
        /// 静音/取消静音通道（支持自定义通道 ID）
        /// </summary>
        public static void MuteChannel(int channelId, bool mute)
        {
            sChannelMuted[channelId] = mute;

            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.SetChannelMuted(channelId, mute);
            }
        }

        /// <summary>
        /// 停止指定通道的所有音频（内置通道）
        /// </summary>
        public static void StopChannel(AudioChannel channel)
        {
            StopChannel((int)channel);
        }

        /// <summary>
        /// 停止指定通道的所有音频（支持自定义通道 ID）
        /// </summary>
        public static void StopChannel(int channelId)
        {
            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.StopChannel(channelId);
            }
            else if (sBackend != null)
            {
                // 通用实现：获取通道音频并停止
                sBackend.GetPlayingHandles(channelId, sCachedHandleList);
                foreach (var handle in sCachedHandleList)
                {
                    handle.Stop();
                }
            }
        }

        #endregion

        #region 全局控制

        /// <summary>
        /// 设置全局音量
        /// </summary>
        public static void SetGlobalVolume(float volume)
        {
            sGlobalVolume = Mathf.Clamp01(volume);
            sBackend?.SetGlobalVolume(GetEffectiveGlobalVolume());
        }

        /// <summary>
        /// 获取全局音量
        /// </summary>
        public static float GetGlobalVolume() => sGlobalVolume;

        /// <summary>
        /// 暂停所有音频
        /// </summary>
        public static void PauseAll()
        {
            sBackend?.PauseAll();
        }

        /// <summary>
        /// 恢复所有音频
        /// </summary>
        public static void ResumeAll()
        {
            sBackend?.ResumeAll();
        }

        /// <summary>
        /// 停止所有音频
        /// </summary>
        public static void StopAll()
        {
            sBackend?.StopAll();
        }

        /// <summary>
        /// 全局静音/取消静音
        /// </summary>
        public static void MuteAll(bool mute)
        {
            sGlobalMuted = mute;
            sBackend?.SetGlobalVolume(GetEffectiveGlobalVolume());
        }

        /// <summary>
        /// 获取全局静音状态
        /// </summary>
        public static bool IsMuted() => sGlobalMuted;

        #endregion
    }
}
