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
    /// Unity 原生音频后端 - 播放和通道控制
    /// </summary>
    public sealed partial class UnityAudioBackend
    {
        #region 播放方法

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

                // 防止并发加载同一路径导致 loader 泄漏
                if (mClipCache.Contains(path))
                {
                    loader.UnloadAndRecycle();
                    mClipCache.TryGet(path, out loadedClip);
                }
                else
                {
                    mClipCache.Add(path, loadedClip, loader);
                }
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
            ConfigureAudioSource(source, clip, config);

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

            // 报告播放事件
            AudioMonitorService.ReportPlay(path, channelId, config.Volume, config.Pitch, clip.length);

            return handle;
        }

        private void ConfigureAudioSource(AudioSource source, AudioClip clip, AudioPlayConfig config)
        {
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
                    source.transform.position = config.FollowTarget.position;
                }
                else
                {
                    source.transform.position = config.Position;
                }
            }
            else
            {
                source.spatialBlend = 0f;
            }
        }

        #endregion

        #region 全局控制

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

        #endregion

        #region 通道控制

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

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 支持

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

            // 防止并发加载同一路径导致 loader 泄漏
            if (mClipCache.Contains(path))
            {
                loader.UnloadAndRecycle();
                mClipCache.TryGet(path, out clip);
            }
            else
            {
                mClipCache.Add(path, clip, loader);
            }

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
                // 防止并发加载同一路径导致 loader 泄漏
                if (mClipCache.Contains(path))
                {
                    loader.UnloadAndRecycle();
                }
                else
                {
                    mClipCache.Add(path, clip, loader);
                }
            }
            else
            {
                KitLogger.Error($"[AudioKit] 预加载失败: {path}");
                loader.UnloadAndRecycle();
            }
        }

        #endregion
#endif
    }
}
