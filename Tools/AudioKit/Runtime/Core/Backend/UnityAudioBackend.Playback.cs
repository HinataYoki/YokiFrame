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

            if (mClipCache.TryGet(path, out var clip))
            {
                var handle = PlayInternal(path, clip, config);
                onComplete?.Invoke(handle);
                return;
            }

            var loader = AudioKit.GetLoaderPool().AllocateLoader();
            var request = AllocatePlayAsyncRequest();
            request.Start(this, loader, path, config, onComplete);
        }

        private IAudioHandle PlayInternal(string path, AudioClip clip, AudioPlayConfig config)
        {
            RemoveInactiveHandles();

            if (mPlayingHandles.Count >= mConfig.MaxConcurrentSounds)
            {
                KitLogger.Warning($"[AudioKit] 超出最大并发数 {mConfig.MaxConcurrentSounds}，跳过播放");
                return null;
            }

            var source = AllocateSource();
            ConfigureAudioSource(source, clip, config);

            var handle = SafePoolKit<UnityAudioHandle>.Instance.Allocate();
            var channelId = config.ChannelId;
            var channelVolume = GetChannelVolume(channelId);
            var effectiveVolume = config.Volume * channelVolume * mGlobalVolume;

            handle.Initialize(path, source, channelId, config.Volume, config.ManualLifecycle, config.FollowTarget);

            if (config.FadeInDuration > 0f)
            {
                handle.SetFadeIn(config.FadeInDuration, effectiveVolume);
            }
            else
            {
                source.volume = effectiveVolume;
            }

            source.Play();
            mPlayingHandles.Add(handle);

            var maxConcurrent = mConfig.GetChannelMaxConcurrent(channelId);
            if (maxConcurrent > 0)
            {
                var currentCount = 0;
                foreach (var h in mPlayingHandles)
                {
                    if (h.ChannelId == channelId)
                    {
                        currentCount++;
                    }
                }

                if (currentCount > maxConcurrent)
                {
                    UnityAudioHandle oldestHandle = null;
                    for (var i = 0; i < mPlayingHandles.Count; i++)
                    {
                        var h = mPlayingHandles[i];
                        if (h.ChannelId == channelId && h != handle)
                        {
                            oldestHandle = h;
                            break;
                        }
                    }

                    if (oldestHandle != null)
                    {
                        BeginHandleMutation();
                        try
                        {
                            if (mPlayingHandles.Remove(oldestHandle))
                            {
                                if (oldestHandle.IsValid)
                                {
                                    oldestHandle.Stop();
                                }
                                RecycleHandle(oldestHandle);
                            }
                        }
                        finally
                        {
                            EndHandleMutation();
                        }
                    }
                }
            }

            AudioMonitorService.ReportPlay(path, channelId, config.Volume, config.Pitch, clip.length);
            return handle;
        }

        private void ConfigureAudioSource(AudioSource source, AudioClip clip, AudioPlayConfig config)
        {
            source.clip = clip;
            source.loop = config.Loop;
            source.pitch = Mathf.Clamp(config.Pitch, 0.01f, 3f);
            source.playOnAwake = false;

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
            BeginHandleMutation();
            try
            {
                for (var i = mPlayingHandles.Count - 1; i >= 0; i--)
                {
                    var handle = mPlayingHandles[i];
                    mPlayingHandles.RemoveAt(i);
                    if (handle.IsValid)
                    {
                        handle.Stop();
                    }
                    RecycleHandle(handle);
                }
            }
            finally
            {
                EndHandleMutation();
            }
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

        internal void SetChannelVolume(AudioChannel channel, float volume)
        {
            SetChannelVolume((int)channel, volume);
        }

        internal void SetChannelVolume(int channelId, float volume)
        {
            mChannelVolumes[channelId] = Mathf.Clamp01(volume);
            UpdateChannelHandleVolumes(channelId);
        }

        internal float GetChannelVolume(AudioChannel channel)
        {
            return GetChannelVolume((int)channel);
        }

        internal float GetChannelVolume(int channelId)
        {
            if (mChannelMuted.TryGetValue(channelId, out var muted) && muted) return 0f;
            return mChannelVolumes.TryGetValue(channelId, out var volume) ? volume : 1f;
        }

        internal void SetChannelMuted(AudioChannel channel, bool muted)
        {
            SetChannelMuted((int)channel, muted);
        }

        internal void SetChannelMuted(int channelId, bool muted)
        {
            mChannelMuted[channelId] = muted;
            UpdateChannelHandleVolumes(channelId);
        }

        internal void StopChannel(AudioChannel channel)
        {
            StopChannel((int)channel);
        }

        internal void StopChannel(int channelId)
        {
            BeginHandleMutation();
            try
            {
                for (var i = mPlayingHandles.Count - 1; i >= 0; i--)
                {
                    var handle = mPlayingHandles[i];
                    if (handle.ChannelId != channelId)
                    {
                        continue;
                    }

                    mPlayingHandles.RemoveAt(i);
                    if (handle.IsValid)
                    {
                        handle.Stop();
                    }
                    RecycleHandle(handle);
                }
            }
            finally
            {
                EndHandleMutation();
            }
        }

        private void RemoveInactiveHandles()
        {
            if (IsMutatingHandles)
            {
                return;
            }

            mHandlesToRemove.Clear();

            for (var i = mPlayingHandles.Count - 1; i >= 0; i--)
            {
                var handle = mPlayingHandles[i];
                if (handle.IsManualLifecycle)
                {
                    continue;
                }

                if (!handle.IsPlaying && !handle.IsPaused && !handle.IsFading)
                {
                    mHandlesToRemove.Add(handle);
                }
            }

            if (mHandlesToRemove.Count == 0)
            {
                return;
            }

            BeginHandleMutation();
            try
            {
                for (var i = 0; i < mHandlesToRemove.Count; i++)
                {
                    var handle = mHandlesToRemove[i];
                    if (!mPlayingHandles.Remove(handle))
                    {
                        continue;
                    }

                    RecycleHandle(handle);
                }
            }
            finally
            {
                EndHandleMutation();
            }
        }

        private void UpdateAllHandleVolumes()
        {
            for (var i = 0; i < mPlayingHandles.Count; i++)
            {
                var handle = mPlayingHandles[i];
                var channelVolume = GetChannelVolume(handle.ChannelId);
                handle.UpdateEffectiveVolume(channelVolume, mGlobalVolume);
            }
        }

        private void UpdateChannelHandleVolumes(int channelId)
        {
            var channelVolume = GetChannelVolume(channelId);
            for (var i = 0; i < mPlayingHandles.Count; i++)
            {
                var handle = mPlayingHandles[i];
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

            if (mClipCache.TryGet(path, out var clip))
            {
                return PlayInternal(path, clip, config);
            }

            var loader = AudioKit.GetLoaderPool().AllocateLoader();
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
                return null;
            }

            if (clip == null)
            {
                KitLogger.Error($"[AudioKit] 音频加载失败: {path}");
                loader.UnloadAndRecycle();
                return null;
            }

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
