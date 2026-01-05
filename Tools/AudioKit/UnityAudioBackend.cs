using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity 原生音频后端实现
    /// </summary>
    public sealed class UnityAudioBackend : IAudioBackend
    {
        private AudioKitConfig mConfig;
        private readonly AudioClipCache mClipCache = new();
        private readonly List<UnityAudioHandle> mPlayingHandles = new();
        private readonly List<UnityAudioHandle> mHandlesToRemove = new();
        private GameObject mAudioRoot;
        private float mGlobalVolume = 1f;
        private bool mIsDisposed;

        // 通道音量缓存
        private readonly float[] mChannelVolumes = new float[5];
        private readonly bool[] mChannelMuted = new bool[5];

        public void Initialize(AudioKitConfig config)
        {
            mConfig = config ?? AudioKitConfig.Default;
            mGlobalVolume = mConfig.GlobalVolume;

            // 初始化通道音量
            mChannelVolumes[(int)AudioChannel.Bgm] = mConfig.BgmVolume;
            mChannelVolumes[(int)AudioChannel.Sfx] = mConfig.SfxVolume;
            mChannelVolumes[(int)AudioChannel.Voice] = mConfig.VoiceVolume;
            mChannelVolumes[(int)AudioChannel.Ambient] = mConfig.AmbientVolume;
            mChannelVolumes[(int)AudioChannel.UI] = mConfig.UIVolume;

            // 创建音频根对象
            if (mAudioRoot == null)
            {
                mAudioRoot = new GameObject("[AudioKit]");
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DontDestroyOnLoad(mAudioRoot);
                }
#else
                UnityEngine.Object.DontDestroyOnLoad(mAudioRoot);
#endif
            }

            // 初始化句柄对象池
            SafePoolKit<UnityAudioHandle>.Instance.Init(mConfig.PoolInitialSize, mConfig.PoolMaxSize);
        }

        public IAudioHandle Play(int audioId, string path, AudioPlayConfig config)
        {
            if (mIsDisposed) return null;

            // 获取或加载音频剪辑
            if (!mClipCache.TryGet(audioId, out var clip))
            {
                clip = Resources.Load<AudioClip>(path);
                if (clip == null)
                {
                    KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                    return null;
                }
                mClipCache.Add(audioId, clip);
            }

            return PlayInternal(audioId, clip, config);
        }

        public void PlayAsync(int audioId, string path, AudioPlayConfig config, Action<IAudioHandle> onComplete)
        {
            if (mIsDisposed)
            {
                onComplete?.Invoke(null);
                return;
            }

            // 检查缓存
            if (mClipCache.TryGet(audioId, out var clip))
            {
                var handle = PlayInternal(audioId, clip, config);
                onComplete?.Invoke(handle);
                return;
            }

            // 异步加载
            var request = Resources.LoadAsync<AudioClip>(path);
            request.completed += _ =>
            {
                if (mIsDisposed)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                clip = request.asset as AudioClip;
                if (clip == null)
                {
                    KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                    onComplete?.Invoke(null);
                    return;
                }

                mClipCache.Add(audioId, clip);
                var handle = PlayInternal(audioId, clip, config);
                onComplete?.Invoke(handle);
            };
        }

        private IAudioHandle PlayInternal(int audioId, AudioClip clip, AudioPlayConfig config)
        {
            // 检查是否超出最大并发数
            if (mPlayingHandles.Count >= mConfig.MaxConcurrentSounds)
            {
                KitLogger.Warning($"[AudioKit] 超出最大并发数 {mConfig.MaxConcurrentSounds}，跳过播放");
                return null;
            }

            // 创建 AudioSource
            var sourceGo = new GameObject($"Audio_{audioId}");
            sourceGo.transform.SetParent(mAudioRoot.transform);
            var source = sourceGo.AddComponent<AudioSource>();

            // 配置 AudioSource
            source.clip = clip;
            source.loop = config.Loop;
            source.pitch = Mathf.Clamp(config.Pitch, 0.01f, 3f);
            source.playOnAwake = false;

            // 3D 音效配置
            if (config.Is3D)
            {
                source.spatialBlend = 1f;
                source.minDistance = config.MinDistance;
                source.maxDistance = config.MaxDistance;
                source.rolloffMode = config.RolloffMode;

                if (config.FollowTarget != null)
                {
                    sourceGo.transform.position = config.FollowTarget.position;
                }
                else
                {
                    sourceGo.transform.position = config.Position;
                }
            }
            else
            {
                source.spatialBlend = 0f;
            }

            // 获取句柄
            var handle = SafePoolKit<UnityAudioHandle>.Instance.Allocate();
            var channelIndex = (int)config.Channel;
            var channelVolume = GetChannelVolume(channelIndex);
            var effectiveVolume = config.Volume * channelVolume * mGlobalVolume;

            handle.Initialize(audioId, source, config.Channel, config.Volume, config.FollowTarget);

            // 淡入处理
            if (config.FadeInDuration > 0f)
            {
                handle.SetFadeIn(config.FadeInDuration, effectiveVolume);
            }
            else
            {
                source.volume = effectiveVolume;
            }

            // 开始播放
            source.Play();
            mPlayingHandles.Add(handle);

            return handle;
        }

        public void Preload(int audioId, string path)
        {
            if (mIsDisposed) return;
            if (mClipCache.Contains(audioId)) return;

            var clip = Resources.Load<AudioClip>(path);
            if (clip != null)
            {
                mClipCache.Add(audioId, clip);
            }
            else
            {
                KitLogger.Error($"[AudioKit] 预加载失败: {path}");
            }
        }

        public void PreloadAsync(int audioId, string path, Action onComplete)
        {
            if (mIsDisposed)
            {
                onComplete?.Invoke();
                return;
            }

            if (mClipCache.Contains(audioId))
            {
                onComplete?.Invoke();
                return;
            }

            var request = Resources.LoadAsync<AudioClip>(path);
            request.completed += _ =>
            {
                if (!mIsDisposed)
                {
                    var clip = request.asset as AudioClip;
                    if (clip != null)
                    {
                        mClipCache.Add(audioId, clip);
                    }
                    else
                    {
                        KitLogger.Error($"[AudioKit] 预加载失败: {path}");
                    }
                }
                onComplete?.Invoke();
            };
        }

        public void Unload(int audioId)
        {
            mClipCache.Remove(audioId);
        }

        public void UnloadAll()
        {
            mClipCache.Clear();
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
            result.Clear();
            foreach (var handle in mPlayingHandles)
            {
                if (handle.Channel == channel)
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
        /// 设置通道音量
        /// </summary>
        internal void SetChannelVolume(AudioChannel channel, float volume)
        {
            var index = (int)channel;
            if (index >= 0 && index < mChannelVolumes.Length)
            {
                mChannelVolumes[index] = Mathf.Clamp01(volume);
                UpdateChannelHandleVolumes(channel);
            }
        }

        /// <summary>
        /// 获取通道音量
        /// </summary>
        internal float GetChannelVolume(AudioChannel channel)
        {
            return GetChannelVolume((int)channel);
        }

        private float GetChannelVolume(int channelIndex)
        {
            if (channelIndex >= 0 && channelIndex < mChannelVolumes.Length)
            {
                if (mChannelMuted[channelIndex]) return 0f;
                return mChannelVolumes[channelIndex];
            }
            return 1f;
        }

        /// <summary>
        /// 设置通道静音
        /// </summary>
        internal void SetChannelMuted(AudioChannel channel, bool muted)
        {
            var index = (int)channel;
            if (index >= 0 && index < mChannelMuted.Length)
            {
                mChannelMuted[index] = muted;
                UpdateChannelHandleVolumes(channel);
            }
        }

        /// <summary>
        /// 停止指定通道的所有音频
        /// </summary>
        internal void StopChannel(AudioChannel channel)
        {
            mHandlesToRemove.Clear();
            foreach (var handle in mPlayingHandles)
            {
                if (handle.Channel == channel)
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
                var channelVolume = GetChannelVolume(handle.Channel);
                handle.UpdateEffectiveVolume(channelVolume, mGlobalVolume);
            }
        }

        private void UpdateChannelHandleVolumes(AudioChannel channel)
        {
            var channelVolume = GetChannelVolume(channel);
            foreach (var handle in mPlayingHandles)
            {
                if (handle.Channel == channel)
                {
                    handle.UpdateEffectiveVolume(channelVolume, mGlobalVolume);
                }
            }
        }

        private void RecycleHandle(UnityAudioHandle handle)
        {
            if (handle.Source != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(handle.Source.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(handle.Source.gameObject);
                }
#else
                UnityEngine.Object.Destroy(handle.Source.gameObject);
#endif
            }
            SafePoolKit<UnityAudioHandle>.Instance.Recycle(handle);
        }

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            StopAll();
            UnloadAll();

            if (mAudioRoot != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(mAudioRoot);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(mAudioRoot);
                }
#else
                UnityEngine.Object.Destroy(mAudioRoot);
#endif
                mAudioRoot = null;
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<IAudioHandle> PlayUniTaskAsync(int audioId, string path, AudioPlayConfig config, CancellationToken cancellationToken = default)
        {
            if (mIsDisposed) return null;

            // 检查缓存
            if (mClipCache.TryGet(audioId, out var clip))
            {
                return PlayInternal(audioId, clip, config);
            }

            // 异步加载
            var request = Resources.LoadAsync<AudioClip>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);

            if (mIsDisposed || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            clip = request.asset as AudioClip;
            if (clip == null)
            {
                KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                return null;
            }

            mClipCache.Add(audioId, clip);
            return PlayInternal(audioId, clip, config);
        }

        public async UniTask PreloadUniTaskAsync(int audioId, string path, CancellationToken cancellationToken = default)
        {
            if (mIsDisposed) return;
            if (mClipCache.Contains(audioId)) return;

            var request = Resources.LoadAsync<AudioClip>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);

            if (mIsDisposed || cancellationToken.IsCancellationRequested) return;

            var clip = request.asset as AudioClip;
            if (clip != null)
            {
                mClipCache.Add(audioId, clip);
            }
            else
            {
                KitLogger.Error($"[AudioKit] 预加载失败: {path}");
            }
        }
#endif
    }
}
