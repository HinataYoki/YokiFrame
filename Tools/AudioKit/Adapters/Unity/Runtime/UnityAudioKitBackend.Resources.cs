#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        /// <summary>同步加载并缓存指定 AudioClip。</summary>
        public bool Preload(string path)
        {
            EnsureUsable();
            return ResolveClip(path) != null;
        }

        /// <summary>异步加载并在 Unity 主线程提交缓存。</summary>
        public async Task<bool> PreloadAsync(string path, CancellationToken token)
        {
            EnsureUsable();
            AudioClip clip = await ResolveClipAsync(path, token).ConfigureAwait(false);
            return clip != null;
        }

        /// <summary>释放指定路径缓存持有的一次资源租约。</summary>
        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            CachedClip cached;
            lock (mClipLock)
            {
                if (!mClips.TryGetValue(path, out cached)) return;
                mClips.Remove(path);
            }

            ReleaseCachedClip(cached);
        }

        /// <summary>释放全部缓存 AudioClip 的资源租约。</summary>
        public void UnloadAll()
        {
            List<CachedClip> clips;
            lock (mClipLock)
            {
                clips = new List<CachedClip>(mClips.Values);
                mClips.Clear();
            }

            for (var index = 0; index < clips.Count; index++) ReleaseCachedClip(clips[index]);
        }

        /// <summary>返回缓存 AudioClip，缺失时通过当前 AudioKit Loader 同步加载。</summary>
        private AudioClip ResolveClip(string path)
        {
            lock (mClipLock)
            {
                if (mClips.TryGetValue(path, out CachedClip cached) && cached.Clip != null) return cached.Clip;
            }

            IAudioResourceLoader loader = AudioKit.GetResourceLoader();
            AudioClip clip = loader.Load<AudioClip>(path);
            if (clip == null) return null;

            CachedClip duplicate = null;
            try
            {
                lock (mClipLock)
                {
                    EnsureUsable();
                    if (mClips.TryGetValue(path, out CachedClip existing) && existing.Clip != null)
                    {
                        duplicate = existing;
                    }
                    else
                    {
                        mClips[path] = new CachedClip { Clip = clip, Loader = loader };
                    }
                }
            }
            catch
            {
                ReleaseAsset(loader, clip);
                throw;
            }

            if (duplicate != null)
            {
                ReleaseAsset(loader, clip);
                return duplicate.Clip;
            }

            return clip;
        }

        /// <summary>异步加载 AudioClip，并保证缓存字典只在 Unity 主线程写入。</summary>
        private async Task<AudioClip> ResolveClipAsync(string path, CancellationToken token)
        {
            lock (mClipLock)
            {
                if (mClips.TryGetValue(path, out CachedClip cached) && cached.Clip != null) return cached.Clip;
            }

            IAudioResourceLoader loader = AudioKit.GetResourceLoader();
            AudioClip clip = null;
            bool leaseSettled = false;
            try
            {
                clip = await loader.LoadAsync<AudioClip>(path, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (clip == null) return null;
                return await InvokeOnUnityThreadAsync(() =>
                {
                    AudioClip resolved = CacheLoadedClip(path, clip, loader);
                    leaseSettled = true;
                    return resolved;
                }, token).ConfigureAwait(false);
            }
            finally
            {
                if (clip != null && !leaseSettled) ReleaseAsset(loader, clip);
            }
        }

        /// <summary>提交异步加载结果；竞争路径已存在时释放重复租约。</summary>
        private AudioClip CacheLoadedClip(string path, AudioClip clip, IAudioResourceLoader loader)
        {
            EnsureUsable();
            CachedClip duplicate = null;
            lock (mClipLock)
            {
                EnsureUsable();
                if (mClips.TryGetValue(path, out CachedClip existing) && existing.Clip != null)
                {
                    duplicate = existing;
                }
                else
                {
                    mClips[path] = new CachedClip { Clip = clip, Loader = loader };
                }
            }

            if (duplicate != null)
            {
                ReleaseAsset(loader, clip);
                return duplicate.Clip;
            }

            return clip;
        }

        /// <summary>把缓存资源交还实际创建该租约的 Loader。</summary>
        private static void ReleaseCachedClip(CachedClip cached)
        {
            if (cached == null || cached.Clip == null || cached.Loader == null) return;
            ReleaseAsset(cached.Loader, cached.Clip);
        }

        /// <summary>安全归还一次资源租约，释放器异常不能阻断其它缓存回收。</summary>
        private static void ReleaseAsset(IAudioResourceLoader loader, object asset)
        {
            if (loader == null || asset == null) return;
            try
            {
                loader.Release(asset);
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Unity audio resource release failed", exception);
            }
        }
    }
}
#endif
