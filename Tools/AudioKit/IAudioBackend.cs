using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 音频后端接口 - 策略模式核心，支持 Unity 原生和 FMOD 等扩展
    /// </summary>
    public interface IAudioBackend : IDisposable
    {
        /// <summary>
        /// 初始化后端
        /// </summary>
        void Initialize(AudioKitConfig config);

        /// <summary>
        /// 同步播放音频
        /// </summary>
        /// <param name="audioId">音频 ID</param>
        /// <param name="path">资源路径</param>
        /// <param name="config">播放配置</param>
        /// <returns>音频句柄，失败返回 null</returns>
        IAudioHandle Play(int audioId, string path, AudioPlayConfig config);

        /// <summary>
        /// 异步播放音频
        /// </summary>
        /// <param name="audioId">音频 ID</param>
        /// <param name="path">资源路径</param>
        /// <param name="config">播放配置</param>
        /// <param name="onComplete">完成回调</param>
        void PlayAsync(int audioId, string path, AudioPlayConfig config, Action<IAudioHandle> onComplete);

        /// <summary>
        /// 同步预加载音频
        /// </summary>
        void Preload(int audioId, string path);

        /// <summary>
        /// 异步预加载音频
        /// </summary>
        void PreloadAsync(int audioId, string path, Action onComplete);

        /// <summary>
        /// 卸载音频
        /// </summary>
        void Unload(int audioId);

        /// <summary>
        /// 卸载所有音频
        /// </summary>
        void UnloadAll();

        /// <summary>
        /// 停止所有音频
        /// </summary>
        void StopAll();

        /// <summary>
        /// 暂停所有音频
        /// </summary>
        void PauseAll();

        /// <summary>
        /// 恢复所有音频
        /// </summary>
        void ResumeAll();

        /// <summary>
        /// 设置全局音量
        /// </summary>
        void SetGlobalVolume(float volume);

        /// <summary>
        /// 更新（驱动淡入淡出、3D 跟随等）
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// 获取指定通道的所有正在播放的音频句柄
        /// </summary>
        void GetPlayingHandles(AudioChannel channel, System.Collections.Generic.List<IAudioHandle> result);

        /// <summary>
        /// 获取所有正在播放的音频句柄
        /// </summary>
        void GetAllPlayingHandles(System.Collections.Generic.List<IAudioHandle> result);

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// [UniTask] 异步播放音频
        /// </summary>
        UniTask<IAudioHandle> PlayUniTaskAsync(int audioId, string path, AudioPlayConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// [UniTask] 异步预加载音频
        /// </summary>
        UniTask PreloadUniTaskAsync(int audioId, string path, CancellationToken cancellationToken = default);
#endif
    }
}
