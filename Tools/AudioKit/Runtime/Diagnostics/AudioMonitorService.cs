using System;

namespace YokiFrame
{
    /// <summary>
    /// 音频监控服务 - 统一的播放事件报告入口
    /// 所有音频后端（Unity/FMOD/Wwise 等）通过此服务报告播放事件
    /// </summary>
    public static class AudioMonitorService
    {
#if UNITY_EDITOR
        /// <summary>
        /// 音频播放事件（仅编辑器）
        /// 参数：path, channelId, volume, pitch, duration
        /// </summary>
        public static event Action<string, int, float, float, float> OnAudioPlayed;

        /// <summary>
        /// 音频停止事件（仅编辑器）
        /// 参数：path, channelId
        /// </summary>
        public static event Action<string, int> OnAudioStopped;
#endif

        /// <summary>
        /// 报告音频播放（所有后端实现时调用此方法）
        /// </summary>
        /// <param name="path">音频路径</param>
        /// <param name="channelId">通道 ID</param>
        /// <param name="volume">音量</param>
        /// <param name="pitch">音调</param>
        /// <param name="duration">时长</param>
        public static void ReportPlay(string path, int channelId, float volume, float pitch, float duration)
        {
#if UNITY_EDITOR
            OnAudioPlayed?.Invoke(path, channelId, volume, pitch, duration);
#endif
        }

        /// <summary>
        /// 报告音频停止（所有后端实现时调用此方法）
        /// </summary>
        /// <param name="path">音频路径</param>
        /// <param name="channelId">通道 ID</param>
        public static void ReportStop(string path, int channelId)
        {
#if UNITY_EDITOR
            OnAudioStopped?.Invoke(path, channelId);
#endif
        }
    }
}
