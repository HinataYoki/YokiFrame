#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame.Godot
{
    public sealed partial class GodotAudioKitBackend
    {
        /// <summary>同步加载并缓存指定 AudioStream。</summary>
        public bool Preload(string path)
        {
            EnsureUsable();
            return ResolveStream(path) != null;
        }

        /// <summary>异步加载并缓存指定 AudioStream。</summary>
        public async Task<bool> PreloadAsync(string path, CancellationToken token)
        {
            EnsureUsable();
            return await ResolveStreamAsync(path, token).ConfigureAwait(false) != null;
        }

        /// <summary>释放指定路径缓存持有的一次资源租约。</summary>
        public void Unload(string path)
        {
            CachedStream cached;
            lock (mStreamLock)
            {
                if (!mStreams.TryGetValue(path, out cached)) return;
                mStreams.Remove(path);
            }

            ReleaseCachedStream(cached);
        }

        /// <summary>释放全部缓存 AudioStream 的资源租约。</summary>
        public void UnloadAll()
        {
            List<CachedStream> streams;
            lock (mStreamLock)
            {
                streams = new List<CachedStream>(mStreams.Values);
                mStreams.Clear();
            }

            for (var index = 0; index < streams.Count; index++) ReleaseCachedStream(streams[index]);
        }

        /// <summary>返回缓存流，缺失时通过当前 AudioKit Loader 同步加载。</summary>
        private AudioStream ResolveStream(string path)
        {
            lock (mStreamLock)
            {
                if (mStreams.TryGetValue(path, out CachedStream cached) && IsValid(cached.Stream)) return cached.Stream;
            }

            IAudioResourceLoader loader = AudioKit.GetResourceLoader();
            AudioStream stream = loader.Load<AudioStream>(path);
            if (!IsValid(stream)) return null;

            CachedStream duplicate = null;
            try
            {
                lock (mStreamLock)
                {
                    EnsureUsable();
                    if (mStreams.TryGetValue(path, out CachedStream existing) && IsValid(existing.Stream))
                    {
                        duplicate = existing;
                    }
                    else
                    {
                        mStreams[path] = new CachedStream { Stream = stream, Loader = loader };
                    }
                }
            }
            catch
            {
                ReleaseAsset(loader, stream);
                throw;
            }

            if (duplicate != null)
            {
                ReleaseAsset(loader, stream);
                return duplicate.Stream;
            }

            return stream;
        }

        /// <summary>异步加载流，并保证缓存只在 Godot 主线程写入。</summary>
        private async Task<AudioStream> ResolveStreamAsync(string path, CancellationToken token)
        {
            lock (mStreamLock)
            {
                if (mStreams.TryGetValue(path, out CachedStream cached) && IsValid(cached.Stream)) return cached.Stream;
            }

            IAudioResourceLoader loader = AudioKit.GetResourceLoader();
            AudioStream stream = null;
            bool leaseSettled = false;
            try
            {
                stream = await loader.LoadAsync<AudioStream>(path, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!IsValid(stream)) return null;
                return await InvokeOnGodotThreadAsync(() =>
                {
                    AudioStream resolved = CacheLoadedStream(path, stream, loader);
                    leaseSettled = true;
                    return resolved;
                }, token).ConfigureAwait(false);
            }
            finally
            {
                if (IsValid(stream) && !leaseSettled) ReleaseAsset(loader, stream);
            }
        }

        /// <summary>提交异步结果；竞争路径已存在时释放重复租约。</summary>
        private AudioStream CacheLoadedStream(string path, AudioStream stream, IAudioResourceLoader loader)
        {
            EnsureUsable();
            CachedStream duplicate = null;
            lock (mStreamLock)
            {
                EnsureUsable();
                if (mStreams.TryGetValue(path, out CachedStream existing) && IsValid(existing.Stream))
                {
                    duplicate = existing;
                }
                else
                {
                    mStreams[path] = new CachedStream { Stream = stream, Loader = loader };
                }
            }

            if (duplicate != null)
            {
                ReleaseAsset(loader, stream);
                return duplicate.Stream;
            }

            return stream;
        }

        /// <summary>把缓存流交还实际创建该租约的 Loader。</summary>
        private static void ReleaseCachedStream(CachedStream cached)
        {
            if (cached == null || !IsValid(cached.Stream) || cached.Loader == null) return;
            ReleaseAsset(cached.Loader, cached.Stream);
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
                TryLogCleanupFailure("Godot audio resource release failed", exception);
            }
        }
    }
}
#endif
