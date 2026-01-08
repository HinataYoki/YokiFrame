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
    /// Unity 原生音频后端实现（使用 ResKit 加载资源）
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

        public IAudioHandle Play(string path, AudioPlayConfig config)
        {
            if (mIsDisposed) return null;
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 播放路径为空");
                return null;
            }

            // 获取或加载音频剪辑
            if (!mClipCache.TryGet(path, out var clip))
            {
                var loader = AudioKit.GetLoaderPool().AllocateLoader();
                clip = loader.Load(path);
                if (clip == null)
                {
                    KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                    loader.UnloadAndRecycle();
                    return null;
                }
                mClipCache.Add(path, clip, loader);
            }

            return PlayInternal(path, clip, config);
        }


        public void PlayAsync(string path, AudioPlayConfig config, Action<IAudioHandle> onComplete)
        {
            if (mIsDisposed)
            {
                onComplete?.Invoke(null);
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 播放路径为空");
                onComplete?.Invoke(null);
                return;
            }

            // 检查缓存
            if (mClipCache.TryGet(path, out var clip))
            {
                var handle = PlayInternal(path, clip, config);
                onComplete?.Invoke(handle);
                return;
            }

            // 使用加载池异步加载
            var loader = AudioKit.GetLoaderPool().AllocateLoader();
            loader.LoadAsync(path, loadedClip =>
            {
                if (mIsDisposed)
                {
                    loader.UnloadAndRecycle();
                    onComplete?.Invoke(null);
                    return;
                }

                if (loadedClip == null)
                {
                    KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                    loader.UnloadAndRecycle();
                    onComplete?.Invoke(null);
                    return;
                }

                mClipCache.Add(path, loadedClip, loader);
                var audioHandle = PlayInternal(path, loadedClip, config);
                onComplete?.Invoke(audioHandle);
            });
        }

        private IAudioHandle PlayInternal(string path, AudioClip clip, AudioPlayConfig config)
        {
            // 检查是否超出最大并发数
            if (mPlayingHandles.Count >= mConfig.MaxConcurrentSounds)
            {
                KitLogger.Warning($"[AudioKit] 超出最大并发数 {mConfig.MaxConcurrentSounds}，跳过播放");
                return null;
            }

            // 创建 AudioSource
            var sourceGo = new GameObject($"Audio_{path.GetHashCode():X8}");
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
            var channelId = config.ChannelId;
            var channelVolume = GetChannelVolume(channelId);
            var effectiveVolume = config.Volume * channelVolume * mGlobalVolume;

            handle.Initialize(path, source, channelId, config.Volume, config.FollowTarget);

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

            // 报告播放事件（通过统一监控服务）
            AudioMonitorService.ReportPlay(path, channelId, config.Volume, config.Pitch, clip.length);

            return handle;
        }

        public void Preload(string path)
        {
            if (mIsDisposed) return;
            if (string.IsNullOrEmpty(path)) return;
            if (mClipCache.Contains(path)) return;

            var loader = AudioKit.GetLoaderPool().AllocateLoader();
            var clip = loader.Load(path);
            if (clip != null)
            {
                mClipCache.Add(path, clip, loader);
            }
            else
            {
                KitLogger.Error($"[AudioKit] 预加载失败: {path}");
                loader.UnloadAndRecycle();
            }
        }

        public void PreloadAsync(string path, Action onComplete)
        {
            if (mIsDisposed)
            {
                onComplete?.Invoke();
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                onComplete?.Invoke();
                return;
            }

            if (mClipCache.Contains(path))
            {
                onComplete?.Invoke();
                return;
            }

            var loader = AudioKit.GetLoaderPool().AllocateLoader();
            loader.LoadAsync(path, clip =>
            {
                if (!mIsDisposed && clip != null)
                {
                    mClipCache.Add(path, clip, loader);
                }
                else
                {
                    if (clip == null)
                    {
                        KitLogger.Error($"[AudioKit] 预加载失败: {path}");
                    }
                    loader.UnloadAndRecycle();
                }
                onComplete?.Invoke();
            });
        }

        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (mClipCache.TryGetEntry(path, out var entry))
            {
                entry.AudioLoader?.UnloadAndRecycle();
                mClipCache.Remove(path);
            }
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
        public async UniTask<IAudioHandle> PlayUniTaskAsync(string path, AudioPlayConfig config, CancellationToken cancellationToken = default)
        {
            if (mIsDisposed) return null;

            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 播放路径为空");
                return null;
            }

            // 检查缓存
            if (mClipCache.TryGet(path, out var clip))
            {
                return PlayInternal(path, clip, config);
            }

            // 使用加载池 UniTask 异步加载
            var loader = AudioKit.GetLoaderPool().AllocateLoader();
            if (loader is IAudioLoaderUniTask uniTaskLoader)
            {
                clip = await uniTaskLoader.LoadUniTaskAsync(path, cancellationToken);
            }
            else
            {
                // 回退到同步加载
                clip = loader.Load(path);
            }

            if (mIsDisposed || cancellationToken.IsCancellationRequested)
            {
                loader.UnloadAndRecycle();
                return null;
            }

            if (clip == null)
            {
                KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                loader.UnloadAndRecycle();
                return null;
            }

            mClipCache.Add(path, clip, loader);
            return PlayInternal(path, clip, config);
        }

        public async UniTask PreloadUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            if (mIsDisposed) return;
            if (string.IsNullOrEmpty(path)) return;
            if (mClipCache.Contains(path)) return;

            var loader = AudioKit.GetLoaderPool().AllocateLoader();
            AudioClip clip;
            if (loader is IAudioLoaderUniTask uniTaskLoader)
            {
                clip = await uniTaskLoader.LoadUniTaskAsync(path, cancellationToken);
            }
            else
            {
                clip = loader.Load(path);
            }

            if (mIsDisposed || cancellationToken.IsCancellationRequested)
            {
                loader.UnloadAndRecycle();
                return;
            }

            if (clip != null)
            {
                mClipCache.Add(path, clip, loader);
            }
            else
            {
                KitLogger.Error($"[AudioKit] 预加载失败: {path}");
                loader.UnloadAndRecycle();
            }
        }
#endif
    }
}
