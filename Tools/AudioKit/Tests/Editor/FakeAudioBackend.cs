using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame.Tests
{
    /// <summary>提供不依赖宿主 SDK 的 AudioKit 后端测试替身。</summary>
    internal class FakeAudioBackend : IAudioBackend
    {
        private readonly Dictionary<string, float> mBusVolumes = new();
        private readonly List<AudioVoiceSnapshot> mVoices = new();
        private int mNextVoiceId;

        /// <summary>获取稳定测试后端名称。</summary>
        public string BackendName => "FakeAudio";

        /// <summary>获取测试后端声明的完整基础能力。</summary>
        public AudioBackendCapabilities Capabilities => AudioBackendCapabilities.All;

        /// <summary>获取播放调用次数。</summary>
        public int PlayCount { get; private set; }

        /// <summary>获取后端更新调用次数。</summary>
        public int UpdateCount { get; private set; }

        /// <summary>获取最近一次后端时间步长。</summary>
        public float LastDeltaTime { get; private set; }

        /// <summary>获取最近一次规范化播放参数。</summary>
        public AudioPlayOptions LastOptions { get; private set; }

        /// <summary>获取后端是否已释放。</summary>
        public bool Disposed { get; private set; }

        /// <summary>创建一个递增 voice id 并记录参数。</summary>
        public virtual int Play(string path, AudioPlayOptions options)
        {
            PlayCount++;
            LastOptions = options;
            int voiceId = ++mNextVoiceId;
            mVoices.Add(new AudioVoiceSnapshot
            {
                VoiceId = voiceId,
                Path = path,
                Bus = options.Bus,
                BackendName = BackendName,
                Loop = options.Loop,
                IsPlaying = true,
                Volume = options.Volume,
                Pitch = options.Pitch,
                Is3D = options.Is3D,
                Position = options.Position,
                MinDistance = options.MinDistance,
                MaxDistance = options.MaxDistance,
                RolloffMode = options.RolloffMode
            });
            return voiceId;
        }

        /// <summary>异步播放测试直接返回同步创建的 voice id。</summary>
        public virtual Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Play(path, options));
        }

        /// <summary>正 id 均视为可停止。</summary>
        public virtual bool Stop(int voiceId) => mVoices.RemoveAll(voice => voice.VoiceId == voiceId) > 0;

        /// <summary>淡出停止沿用普通停止结果。</summary>
        public bool StopWithFade(int voiceId, float fadeDuration) => Stop(voiceId);

        /// <summary>测试后端无需维护全部 voice。</summary>
        public virtual void StopAll() => mVoices.Clear();

        /// <summary>测试后端无需维护总线 voice。</summary>
        public void StopBus(string bus) => mVoices.RemoveAll(
            voice => string.Equals(voice.Bus, bus, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>测试后端无需维护暂停状态。</summary>
        public void PauseAll() { }

        /// <summary>测试后端无需维护恢复状态。</summary>
        public void ResumeAll() { }

        /// <summary>非空路径视为预加载成功。</summary>
        public bool Preload(string path) => !string.IsNullOrEmpty(path);

        /// <summary>异步预加载测试直接返回同步结果。</summary>
        public Task<bool> PreloadAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Preload(path));
        }

        /// <summary>测试后端无需维护单资源缓存。</summary>
        public void Unload(string path) { }

        /// <summary>测试后端无需维护全资源缓存。</summary>
        public virtual void UnloadAll() { }

        /// <summary>保存指定逻辑总线音量。</summary>
        public virtual void SetBusVolume(string bus, float volume) => mBusVolumes[bus] = volume;

        /// <summary>读取已保存音量，未知总线返回一。</summary>
        public float GetBusVolume(string bus) => mBusVolumes.TryGetValue(bus, out float volume) ? volume : 1f;

        /// <summary>判断门面是否已经向测试后端同步指定总线。</summary>
        public bool HasBus(string bus) => mBusVolumes.ContainsKey(bus);

        /// <summary>记录统一帧派发器传入的时间步长。</summary>
        public void Update(float deltaTime)
        {
            UpdateCount++;
            LastDeltaTime = deltaTime;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>复制测试播放产生的 active voice，供诊断聚合测试读取。</summary>
        public void GetActiveVoices(List<AudioVoiceSnapshot> result)
        {
            result.Clear();
            result.AddRange(mVoices);
        }
#endif

        /// <summary>标记后端已经由 AudioKit 释放。</summary>
        public virtual void Dispose() => Disposed = true;
    }
}
