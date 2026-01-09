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
    public sealed partial class UnityAudioBackend : IAudioBackend
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
    }
}
