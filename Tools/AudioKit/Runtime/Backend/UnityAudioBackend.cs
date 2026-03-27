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
        /// AudioSource GameObject 对象池，避免频繁 new/Destroy
        /// </summary>
        private readonly Stack<AudioSource> mSourcePool = new();

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

        /// <summary>
        /// 从池中分配 AudioSource，池空时创建新 GameObject
        /// </summary>
        internal AudioSource AllocateSource()
        {
            while (mSourcePool.Count > 0)
            {
                var pooled = mSourcePool.Pop();
                if (pooled != default)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var go = new GameObject("[AudioSource]");
            go.transform.SetParent(mAudioRoot.transform);
            return go.AddComponent<AudioSource>();
        }

        /// <summary>
        /// 回收 AudioSource 到池中（清理状态 + 隐藏）
        /// </summary>
        internal void RecycleSource(AudioSource source)
        {
            if (source == default) return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.pitch = 1f;
            source.volume = 1f;
            source.spatialBlend = 0f;
            source.playOnAwake = false;

            if (mSourcePool.Count < mConfig.PoolMaxSize)
            {
                source.gameObject.SetActive(false);
                mSourcePool.Push(source);
            }
            else
            {
                DestroyGameObject(source.gameObject);
            }
        }

        /// <summary>
        /// 当前 AudioSource 池中缓存的数量
        /// </summary>
        internal int SourcePoolCount => mSourcePool.Count;

        private void RecycleHandle(UnityAudioHandle handle)
        {
            RecycleSource(handle.Source);
            SafePoolKit<UnityAudioHandle>.Instance.Recycle(handle);
        }

        private static void DestroyGameObject(GameObject go)
        {
            if (go == default) return;
#if UNITY_EDITOR
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
#else
            UnityEngine.Object.Destroy(go);
#endif
        }

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            StopAll();
            UnloadAll();

            while (mSourcePool.Count > 0)
            {
                var source = mSourcePool.Pop();
                if (source != default)
                    DestroyGameObject(source.gameObject);
            }

            if (mAudioRoot != default)
            {
                DestroyGameObject(mAudioRoot);
                mAudioRoot = null;
            }
        }
    }
}
