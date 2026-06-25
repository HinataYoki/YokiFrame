using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 默认资源加载器，通过 ResKit 统一资源入口加载并持有句柄。
    /// </summary>
    public sealed class ResKitAudioResourceLoader : IAudioResourceLoader
    {
        internal static readonly ResKitAudioResourceLoader Shared = new ResKitAudioResourceLoader();

        private readonly object mLock = new object();
        private readonly Dictionary<object, Stack<IDisposable>> mHandles = new Dictionary<object, Stack<IDisposable>>();

        public string LoaderName => "ResKit";

        public T Load<T>(string path) where T : class
        {
            if (ResKit.GetProvider() == null)
                return null;

            var handle = ResKit.LoadAsset<T>(path);
            if (handle == null || handle.Asset == null)
                return null;

            TrackHandle(handle.Asset, handle);
            return handle.Asset;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
            if (ResKit.GetProvider() == null)
                return null;

#if YOKIFRAME_UNITASK_SUPPORT
            var handle = await ResKit.LoadAssetAsync<T>(path, token);
#else
            var handle = await ResKit.LoadAssetAsync<T>(path, token).ConfigureAwait(false);
#endif
            if (handle == null || handle.Asset == null)
                return null;

            TrackHandle(handle.Asset, handle);
            return handle.Asset;
        }

        public void Release(object asset)
        {
            if (asset == null)
                return;

            IDisposable handle = null;
            lock (mLock)
            {
                Stack<IDisposable> handles;
                if (mHandles.TryGetValue(asset, out handles) && handles.Count > 0)
                {
                    handle = handles.Pop();
                    if (handles.Count == 0)
                        mHandles.Remove(asset);
                }
            }

            if (handle != null)
                handle.Dispose();
        }

        private void TrackHandle(object asset, IDisposable handle)
        {
            lock (mLock)
            {
                Stack<IDisposable> handles;
                if (!mHandles.TryGetValue(asset, out handles))
                {
                    handles = new Stack<IDisposable>();
                    mHandles.Add(asset, handles);
                }

                handles.Push(handle);
            }
        }
    }
}
