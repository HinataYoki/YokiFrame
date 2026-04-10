using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频句柄接口 - 控制正在播放的单个音频实例
    /// </summary>
    public interface IAudioHandle
    {
        /// <summary>
        /// 句柄是否有效（未被回收）
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 是否启用手动生命周期管理（禁用自动回收）
        /// </summary>
        bool IsManualLifecycle { get; }

        /// <summary>
        /// 音频资源路径
        /// </summary>
        string Path { get; }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 是否已暂停
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 音量 (0-1)
        /// </summary>
        float Volume { get; set; }

        /// <summary>
        /// 音调 (0.01-3)
        /// </summary>
        float Pitch { get; set; }

        /// <summary>
        /// 当前播放时间（秒）
        /// </summary>
        float Time { get; set; }

        /// <summary>
        /// 音频总时长（秒）
        /// </summary>
        float Duration { get; }

        /// <summary>
        /// 音频通道（内置通道的便捷属性）
        /// </summary>
        AudioChannel Channel { get; }

        /// <summary>
        /// 音频通道 ID（支持自定义通道，5+ 为用户自定义）
        /// </summary>
        int ChannelId { get; }

        /// <summary>
        /// 暂停播放
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复播放
        /// </summary>
        void Resume();

        /// <summary>
        /// 停止播放
        /// </summary>
        void Stop();

        /// <summary>
        /// 淡出后停止
        /// </summary>
        /// <param name="fadeDuration">淡出时长（秒）</param>
        void StopWithFade(float fadeDuration);

        /// <summary>
        /// 设置手动生命周期管理（禁用自动回收）
        /// </summary>
        /// <param name="manual">true: 用户手动管理，播放完成不自动回收；false: 自动回收</param>
        void SetManualLifecycle(bool manual);

        /// <summary>
        /// 手动释放句柄（仅在手动生命周期模式下使用）
        /// </summary>
        void Release();

        /// <summary>
        /// 设置 3D 音效位置
        /// </summary>
        void SetPosition(Vector3 position);
    }
}
