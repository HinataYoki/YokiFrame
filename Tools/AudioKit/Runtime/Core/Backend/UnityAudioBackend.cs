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
    /// Unity 原生音频后端实现，使用 ResKit 加载资源。
    /// </summary>
    public sealed partial class UnityAudioBackend : IAudioBackend
    {
        private AudioKitConfig mConfig;
        private readonly AudioClipCache mClipCache = new();
        private readonly List<UnityAudioHandle> mPlayingHandles = new();
        private readonly List<UnityAudioHandle> mHandlesToRemove = new();
        private readonly Stack<PlayAsyncLoadRequest> mPlayAsyncRequestPool = new();
        private readonly Stack<PreloadAsyncLoadRequest> mPreloadAsyncRequestPool = new();
        private GameObject mAudioRoot;
        private float mGlobalVolume = 1f;
        private int mHandleMutationDepth;
        private bool mIsDisposed;

        /// <summary>
        /// AudioSource GameObject 对象池，避免频繁 new/Destroy。
        /// </summary>
        private readonly Stack<AudioSource> mSourcePool = new();

        private readonly Dictionary<int, float> mChannelVolumes = new();
        private readonly Dictionary<int, bool> mChannelMuted = new();

        public void Initialize(AudioKitConfig config)
        {
            mConfig = config ?? AudioKitConfig.Default;
            mGlobalVolume = mConfig.GlobalVolume;

            mChannelVolumes[(int)AudioChannel.Bgm] = mConfig.BgmVolume;
            mChannelVolumes[(int)AudioChannel.Sfx] = mConfig.SfxVolume;
            mChannelVolumes[(int)AudioChannel.Voice] = mConfig.VoiceVolume;
            mChannelVolumes[(int)AudioChannel.Ambient] = mConfig.AmbientVolume;
            mChannelVolumes[(int)AudioChannel.UI] = mConfig.UIVolume;

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
            var request = AllocatePreloadAsyncRequest();
            request.Start(this, loader, path, onComplete);
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

        public void GetPlayingHandles(AudioChannel channel, List<IAudioHandle> result)
        {
            GetPlayingHandles((int)channel, result);
        }

        public void GetPlayingHandles(int channelId, List<IAudioHandle> result)
        {
            RemoveInactiveHandles();
            result.Clear();
            for (var i = 0; i < mPlayingHandles.Count; i++)
            {
                var handle = mPlayingHandles[i];
                if (handle.ChannelId == channelId)
                {
                    result.Add(handle);
                }
            }
        }

        public void GetAllPlayingHandles(List<IAudioHandle> result)
        {
            RemoveInactiveHandles();
            result.Clear();
            for (var i = 0; i < mPlayingHandles.Count; i++)
            {
                var handle = mPlayingHandles[i];
                result.Add(handle);
            }
        }

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

        internal int SourcePoolCount => mSourcePool.Count;
        internal bool IsMutatingHandles => mHandleMutationDepth > 0;

        private void RecycleHandle(UnityAudioHandle handle)
        {
            var path = handle.Path;
            var channelId = handle.ChannelId;
            RecycleSource(handle.Source);
            AudioMonitorService.ReportStop(path, channelId);
            SafePoolKit<UnityAudioHandle>.Instance.Recycle(handle);
        }

        private void BeginHandleMutation()
        {
            mHandleMutationDepth++;
        }

        private void EndHandleMutation()
        {
            if (mHandleMutationDepth > 0)
            {
                mHandleMutationDepth--;
            }
        }

        private PlayAsyncLoadRequest AllocatePlayAsyncRequest()
        {
            return mPlayAsyncRequestPool.Count > 0 ? mPlayAsyncRequestPool.Pop() : new PlayAsyncLoadRequest();
        }

        private void RecyclePlayAsyncRequest(PlayAsyncLoadRequest request)
        {
            request.Reset();
            mPlayAsyncRequestPool.Push(request);
        }

        private PreloadAsyncLoadRequest AllocatePreloadAsyncRequest()
        {
            return mPreloadAsyncRequestPool.Count > 0 ? mPreloadAsyncRequestPool.Pop() : new PreloadAsyncLoadRequest();
        }

        private void RecyclePreloadAsyncRequest(PreloadAsyncLoadRequest request)
        {
            request.Reset();
            mPreloadAsyncRequestPool.Push(request);
        }

        private sealed class PlayAsyncLoadRequest
        {
            private readonly Action<AudioClip> mCachedCallback;
            private UnityAudioBackend mBackend;
            private IAudioLoader mLoader;
            private string mPath;
            private AudioPlayConfig mConfig;
            private Action<IAudioHandle> mOnComplete;

            public PlayAsyncLoadRequest()
            {
                mCachedCallback = OnLoaded;
            }

            public void Start(UnityAudioBackend backend, IAudioLoader loader, string path, AudioPlayConfig config, Action<IAudioHandle> onComplete)
            {
                mBackend = backend;
                mLoader = loader;
                mPath = path;
                mConfig = config;
                mOnComplete = onComplete;
                loader.LoadAsync(path, mCachedCallback);
            }

            public void Reset()
            {
                mBackend = null;
                mLoader = null;
                mPath = null;
                mConfig = default;
                mOnComplete = null;
            }

            private void OnLoaded(AudioClip loadedClip)
            {
                var backend = mBackend;
                var loader = mLoader;
                var onComplete = mOnComplete;
                var path = mPath;
                var config = mConfig;

                try
                {
                    if (backend.mIsDisposed)
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

                    if (backend.mClipCache.Contains(path))
                    {
                        loader.UnloadAndRecycle();
                        backend.mClipCache.TryGet(path, out loadedClip);
                    }
                    else
                    {
                        backend.mClipCache.Add(path, loadedClip, loader);
                    }

                    var audioHandle = backend.PlayInternal(path, loadedClip, config);
                    onComplete?.Invoke(audioHandle);
                }
                finally
                {
                    backend.RecyclePlayAsyncRequest(this);
                }
            }
        }

        private sealed class PreloadAsyncLoadRequest
        {
            private readonly Action<AudioClip> mCachedCallback;
            private UnityAudioBackend mBackend;
            private IAudioLoader mLoader;
            private string mPath;
            private Action mOnComplete;

            public PreloadAsyncLoadRequest()
            {
                mCachedCallback = OnLoaded;
            }

            public void Start(UnityAudioBackend backend, IAudioLoader loader, string path, Action onComplete)
            {
                mBackend = backend;
                mLoader = loader;
                mPath = path;
                mOnComplete = onComplete;
                loader.LoadAsync(path, mCachedCallback);
            }

            public void Reset()
            {
                mBackend = null;
                mLoader = null;
                mPath = null;
                mOnComplete = null;
            }

            private void OnLoaded(AudioClip clip)
            {
                var backend = mBackend;
                var loader = mLoader;
                var path = mPath;
                var onComplete = mOnComplete;

                try
                {
                    if (!backend.mIsDisposed && clip != null)
                    {
                        if (backend.mClipCache.Contains(path))
                        {
                            loader.UnloadAndRecycle();
                        }
                        else
                        {
                            backend.mClipCache.Add(path, clip, loader);
                        }
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
                }
                finally
                {
                    backend.RecyclePreloadAsyncRequest(this);
                }
            }
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
