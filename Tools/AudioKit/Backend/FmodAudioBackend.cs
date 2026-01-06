#if YOKIFRAME_FMOD_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

namespace YokiFrame
{
    /// <summary>
    /// FMOD 音频后端实现 - 使用 FMOD Studio 事件系统
    /// </summary>
    public sealed class FmodAudioBackend : IAudioBackend
    {
        private AudioKitConfig mConfig;
        private readonly List<FmodAudioHandle> mPlayingHandles = new();
        private readonly List<FmodAudioHandle> mHandlesToRemove = new();
        private readonly Dictionary<string, EventDescription> mCachedDescriptions = new();
        private float mGlobalVolume = 1f;
        private bool mIsDisposed;

        /// <summary>
        /// 通道音量缓存（支持动态扩展）
        /// </summary>
        private readonly Dictionary<int, float> mChannelVolumes = new();

        /// <summary>
        /// 通道静音状态缓存（支持动态扩展）
        /// </summary>
        private readonly Dictionary<int, bool> mChannelMuted = new();

        public void Initialize(AudioKitConfig config)
        {
            mConfig = config ?? AudioKitConfig.Default;
            mGlobalVolume = mConfig.GlobalVolume;

            // 初始化内置通道音量
            mChannelVolumes[(int)AudioChannel.Bgm] = mConfig.BgmVolume;
            mChannelVolumes[(int)AudioChannel.Sfx] = mConfig.SfxVolume;
            mChannelVolumes[(int)AudioChannel.Voice] = mConfig.VoiceVolume;
            mChannelVolumes[(int)AudioChannel.Ambient] = mConfig.AmbientVolume;
            mChannelVolumes[(int)AudioChannel.UI] = mConfig.UIVolume;

            // 初始化句柄对象池
            SafePoolKit<FmodAudioHandle>.Instance.Init(mConfig.PoolInitialSize, mConfig.PoolMaxSize);
        }

        public IAudioHandle Play(string path, AudioPlayConfig config)
        {
            if (mIsDisposed) return null;
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit/FMOD] 播放路径为空");
                return null;
            }

            // 获取或缓存 EventDescription
            if (!TryGetEventDescription(path, out var description))
            {
                KitLogger.Error($"[AudioKit/FMOD] 事件加载失败: {path}");
                return null;
            }

            return PlayInternal(path, description, config);
        }

        public void PlayAsync(string path, AudioPlayConfig config, Action<IAudioHandle> onComplete)
        {
            // FMOD 的事件加载是同步的，直接调用同步方法
            var handle = Play(path, config);
            onComplete?.Invoke(handle);
        }

        private bool TryGetEventDescription(string path, out EventDescription description)
        {
            // 检查缓存
            if (mCachedDescriptions.TryGetValue(path, out description) && description.isValid())
            {
                return true;
            }

            // 从 FMOD 获取事件描述
            try
            {
                description = RuntimeManager.GetEventDescription(path);
                if (description.isValid())
                {
                    mCachedDescriptions[path] = description;
                    return true;
                }
            }
            catch (EventNotFoundException)
            {
                KitLogger.Error($"[AudioKit/FMOD] 事件未找到: {path}");
            }

            description = default;
            return false;
        }

        private IAudioHandle PlayInternal(string path, EventDescription description, AudioPlayConfig config)
        {
            // 检查是否超出最大并发数
            if (mPlayingHandles.Count >= mConfig.MaxConcurrentSounds)
            {
                KitLogger.Warning($"[AudioKit/FMOD] 超出最大并发数 {mConfig.MaxConcurrentSounds}，跳过播放");
                return null;
            }

            // 创建 FMOD 事件实例
            description.createInstance(out var instance);
            if (!instance.isValid())
            {
                KitLogger.Error($"[AudioKit/FMOD] 创建事件实例失败: {path}");
                return null;
            }

            // 3D 音效配置
            if (config.Is3D)
            {
                FMOD.ATTRIBUTES_3D attributes;
                if (config.FollowTarget != null)
                {
                    attributes = RuntimeUtils.To3DAttributes(config.FollowTarget);
                }
                else
                {
                    attributes = RuntimeUtils.To3DAttributes(config.Position);
                }
                instance.set3DAttributes(attributes);
            }

            // 获取句柄
            var handle = SafePoolKit<FmodAudioHandle>.Instance.Allocate();
            var channelId = config.ChannelId;
            var channelVolume = GetChannelVolume(channelId);
            var effectiveVolume = config.Volume * channelVolume * mGlobalVolume;

            handle.Initialize(path, instance, description, channelId, config.Volume, config.FollowTarget);

            // 设置音调
            if (Math.Abs(config.Pitch - 1f) > 0.001f)
            {
                instance.setPitch(Mathf.Clamp(config.Pitch, 0.01f, 3f));
            }

            // 淡入处理
            if (config.FadeInDuration > 0f)
            {
                handle.SetFadeIn(config.FadeInDuration, effectiveVolume);
            }
            else
            {
                instance.setVolume(effectiveVolume);
            }

            // 开始播放
            instance.start();
            mPlayingHandles.Add(handle);

            // 报告播放事件
            description.getLength(out var lengthMs);
            AudioMonitorService.ReportPlay(path, channelId, config.Volume, config.Pitch, lengthMs / 1000f);

            return handle;
        }

        public void Preload(string path)
        {
            if (mIsDisposed) return;
            if (string.IsNullOrEmpty(path)) return;

            // 预加载事件描述并加载采样数据
            if (TryGetEventDescription(path, out var description))
            {
                description.loadSampleData();
            }
        }

        public void PreloadAsync(string path, Action onComplete)
        {
            // FMOD 的预加载是同步的
            Preload(path);
            onComplete?.Invoke();
        }

        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (mCachedDescriptions.TryGetValue(path, out var description) && description.isValid())
            {
                description.unloadSampleData();
                mCachedDescriptions.Remove(path);
            }
        }

        public void UnloadAll()
        {
            foreach (var kvp in mCachedDescriptions)
            {
                if (kvp.Value.isValid())
                {
                    kvp.Value.unloadSampleData();
                }
            }
            mCachedDescriptions.Clear();
        }

        public void StopAll()
        {
            foreach (var handle in mPlayingHandles)
            {
                handle.Stop();
                RecycleHandle(handle);
            }
            mPlayingHandles.Clear();
        }

        public void PauseAll()
        {
            foreach (var handle in mPlayingHandles)
            {
                handle.Pause();
            }
        }

        public void ResumeAll()
        {
            foreach (var handle in mPlayingHandles)
            {
                handle.Resume();
            }
        }

        public void SetGlobalVolume(float volume)
        {
            mGlobalVolume = Mathf.Clamp01(volume);
            UpdateAllHandleVolumes();
        }

        public void Update(float deltaTime)
        {
            if (mIsDisposed) return;

            mHandlesToRemove.Clear();

            foreach (var handle in mPlayingHandles)
            {
                if (handle.UpdateFade(deltaTime))
                {
                    mHandlesToRemove.Add(handle);
                }
            }

            // 移除已完成的句柄
            foreach (var handle in mHandlesToRemove)
            {
                mPlayingHandles.Remove(handle);
                RecycleHandle(handle);
            }
        }

        public void GetPlayingHandles(AudioChannel channel, List<IAudioHandle> result)
        {
            GetPlayingHandles((int)channel, result);
        }

        public void GetPlayingHandles(int channelId, List<IAudioHandle> result)
        {
            result.Clear();
            foreach (var handle in mPlayingHandles)
            {
                if (handle.ChannelId == channelId)
                {
                    result.Add(handle);
                }
            }
        }

        public void GetAllPlayingHandles(List<IAudioHandle> result)
        {
            result.Clear();
            foreach (var handle in mPlayingHandles)
            {
                result.Add(handle);
            }
        }

        /// <summary>
        /// 设置通道音量（内置通道）
        /// </summary>
        internal void SetChannelVolume(AudioChannel channel, float volume)
        {
            SetChannelVolume((int)channel, volume);
        }

        /// <summary>
        /// 设置通道音量（支持自定义通道 ID）
        /// </summary>
        internal void SetChannelVolume(int channelId, float volume)
        {
            mChannelVolumes[channelId] = Mathf.Clamp01(volume);
            UpdateChannelHandleVolumes(channelId);
        }

        /// <summary>
        /// 获取通道音量（内置通道）
        /// </summary>
        internal float GetChannelVolume(AudioChannel channel)
        {
            return GetChannelVolume((int)channel);
        }

        /// <summary>
        /// 获取通道音量（支持自定义通道 ID）
        /// </summary>
        internal float GetChannelVolume(int channelId)
        {
            if (mChannelMuted.TryGetValue(channelId, out var muted) && muted) return 0f;
            return mChannelVolumes.TryGetValue(channelId, out var volume) ? volume : 1f;
        }

        /// <summary>
        /// 设置通道静音（内置通道）
        /// </summary>
        internal void SetChannelMuted(AudioChannel channel, bool muted)
        {
            SetChannelMuted((int)channel, muted);
        }

        /// <summary>
        /// 设置通道静音（支持自定义通道 ID）
        /// </summary>
        internal void SetChannelMuted(int channelId, bool muted)
        {
            mChannelMuted[channelId] = muted;
            UpdateChannelHandleVolumes(channelId);
        }

        /// <summary>
        /// 停止指定通道的所有音频（内置通道）
        /// </summary>
        internal void StopChannel(AudioChannel channel)
        {
            StopChannel((int)channel);
        }

        /// <summary>
        /// 停止指定通道的所有音频（支持自定义通道 ID）
        /// </summary>
        internal void StopChannel(int channelId)
        {
            mHandlesToRemove.Clear();
            foreach (var handle in mPlayingHandles)
            {
                if (handle.ChannelId == channelId)
                {
                    handle.Stop();
                    mHandlesToRemove.Add(handle);
                }
            }

            foreach (var handle in mHandlesToRemove)
            {
                mPlayingHandles.Remove(handle);
                RecycleHandle(handle);
            }
        }

        private void UpdateAllHandleVolumes()
        {
            foreach (var handle in mPlayingHandles)
            {
                var channelVolume = GetChannelVolume(handle.ChannelId);
                handle.UpdateEffectiveVolume(channelVolume, mGlobalVolume);
            }
        }

        private void UpdateChannelHandleVolumes(int channelId)
        {
            var channelVolume = GetChannelVolume(channelId);
            foreach (var handle in mPlayingHandles)
            {
                if (handle.ChannelId == channelId)
                {
                    handle.UpdateEffectiveVolume(channelVolume, mGlobalVolume);
                }
            }
        }

        private void RecycleHandle(FmodAudioHandle handle)
        {
            SafePoolKit<FmodAudioHandle>.Instance.Recycle(handle);
        }

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            StopAll();
            UnloadAll();
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<IAudioHandle> PlayUniTaskAsync(string path, AudioPlayConfig config, CancellationToken cancellationToken = default)
        {
            if (mIsDisposed) return UniTask.FromResult<IAudioHandle>(null);

            // FMOD 事件加载是同步的，直接返回
            var handle = Play(path, config);
            return UniTask.FromResult(handle);
        }

        public UniTask PreloadUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            if (mIsDisposed) return UniTask.CompletedTask;

            Preload(path);
            return UniTask.CompletedTask;
        }
#endif
    }
}
#endif
