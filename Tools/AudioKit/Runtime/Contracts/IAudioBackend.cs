using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>定义 AudioKit 与具体宿主或第三方音频系统之间的运行时边界。</summary>
    public interface IAudioBackend : IDisposable
    {
        /// <summary>获取诊断展示名称。</summary>
        string BackendName { get; }

        /// <summary>获取后端真实支持的可选语义。</summary>
        AudioBackendCapabilities Capabilities { get; }

        /// <summary>同步播放并返回后端局部 voice id；失败返回零。</summary>
        int Play(string path, AudioPlayOptions options);

        /// <summary>异步播放并返回后端局部 voice id；失败返回零。</summary>
        Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token);

        /// <summary>立即停止指定后端局部 voice。</summary>
        bool Stop(int voiceId);

        /// <summary>按指定秒数淡出并停止 voice。</summary>
        bool StopWithFade(int voiceId, float fadeDuration);

        /// <summary>停止全部 voice。</summary>
        void StopAll();

        /// <summary>停止指定逻辑总线的全部 voice。</summary>
        void StopBus(string bus);

        /// <summary>暂停全部 voice。</summary>
        void PauseAll();

        /// <summary>恢复全部 voice。</summary>
        void ResumeAll();

        /// <summary>同步预加载指定资源并报告是否成功。</summary>
        bool Preload(string path);

        /// <summary>异步预加载指定资源并报告是否成功。</summary>
        Task<bool> PreloadAsync(string path, CancellationToken token);

        /// <summary>卸载指定缓存资源。</summary>
        void Unload(string path);

        /// <summary>卸载全部缓存资源。</summary>
        void UnloadAll();

        /// <summary>设置后端逻辑总线的有效音量。</summary>
        void SetBusVolume(string bus, float volume);

        /// <summary>获取后端逻辑总线的有效音量。</summary>
        float GetBusVolume(string bus);

        /// <summary>使用缩放时间推进淡变、跟随和自然结束回收。</summary>
        void Update(float deltaTime);

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>按需复制当前 active voice 诊断状态；方法必须先清空结果。</summary>
        void GetActiveVoices(List<AudioVoiceSnapshot> result);
#endif
    }

    /// <summary>供宿主 Adapter 把异步启动失败后的 voice 清理封送回宿主线程；不属于公开后端契约。</summary>
    internal interface IAudioBackendAsyncCleanup
    {
        /// <summary>在后端要求的线程上尽力停止指定局部 voice。</summary>
        Task StopVoiceAsync(int voiceId);
    }
}
