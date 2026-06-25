using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 的引擎后端接口。Unity/Godot 的具体播放实现放在 Adapter 层。
    /// </summary>
    public interface IAudioBackend
    {
        /// <summary>
        /// 后端名称，用于诊断和命令桥状态输出。
        /// </summary>
        string BackendName { get; }

        /// <summary>
        /// 播放指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="options">已规范化的播放选项。</param>
        /// <returns>播放 voice 诊断信息；播放失败时返回空。</returns>
        AudioVoiceDebugInfo Play(string path, AudioPlayOptions options);

        /// <summary>
        /// 异步播放指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="options">已规范化的播放选项。</param>
        /// <param name="onComplete">播放启动完成回调。</param>
        void PlayAsync(string path, AudioPlayOptions options, Action<AudioVoiceDebugInfo> onComplete);

        /// <summary>
        /// 停止指定播放 voice。
        /// </summary>
        /// <param name="voiceId">播放 voice 标识。</param>
        /// <returns>成功停止时返回 true。</returns>
        bool Stop(int voiceId);

        /// <summary>
        /// 使用淡出停止指定播放 voice。
        /// </summary>
        /// <param name="voiceId">播放 voice 标识。</param>
        /// <param name="fadeDuration">淡出时长，单位秒。</param>
        /// <returns>成功发起停止时返回 true。</returns>
        bool StopWithFade(int voiceId, float fadeDuration);

        /// <summary>
        /// 停止全部播放。
        /// </summary>
        void StopAll();

        /// <summary>
        /// 停止指定总线上的全部播放。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        void StopBus(string bus);

        /// <summary>
        /// 暂停全部播放。
        /// </summary>
        void PauseAll();

        /// <summary>
        /// 恢复全部播放。
        /// </summary>
        void ResumeAll();

        /// <summary>
        /// 预加载指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        void Preload(string path);

        /// <summary>
        /// 异步预加载指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="onComplete">预加载完成回调。</param>
        void PreloadAsync(string path, Action onComplete);

        /// <summary>
        /// 卸载指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        void Unload(string path);

        /// <summary>
        /// 卸载后端持有的全部音频资源。
        /// </summary>
        void UnloadAll();

        /// <summary>
        /// 设置指定总线音量。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <param name="volume">音量值。</param>
        void SetBusVolume(string bus, float volume);

        /// <summary>
        /// 获取指定总线音量。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        /// <returns>当前后端总线音量。</returns>
        float GetBusVolume(string bus);

        /// <summary>
        /// 推进后端运行时更新。
        /// </summary>
        /// <param name="deltaTime">宿主帧间隔，单位秒。</param>
        void Update(float deltaTime);

        /// <summary>
        /// 获取当前活跃 voice 列表。
        /// </summary>
        /// <param name="result">接收结果的列表；实现应先清空或按调用方约定写入。</param>
        void GetActiveVoices(List<AudioVoiceDebugInfo> result);
    }
}
