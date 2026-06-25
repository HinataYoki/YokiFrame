#if !GODOT
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// IResourceProvider 的 Unity 实现，基于 Resources.Load
    /// 未来可替换为 YooAsset / Addressables 实现
    /// </summary>
    public sealed class UnityResourceProvider : IResourceProvider, IRawResourceProvider
    {
        /// <summary>
        /// 获取资源提供者名称。
        /// </summary>
        public string ProviderName => "Unity.Resources";

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 获取当前异步资源加载后端名称。
        /// </summary>
        public static string AsyncBackendName => "UniTask";
#else
        /// <summary>
        /// 获取当前异步资源加载后端名称。
        /// </summary>
        public static string AsyncBackendName => "TaskCompletionSource";
#endif

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；不存在或类型不匹配时返回 null。</returns>
        public T Load<T>(string path) where T : class
        {
            var obj = Resources.Load(path);
            if (obj == default)
                return null;

            if (typeof(T) == typeof(GameObject))
                return obj as T;

            if (typeof(T) == typeof(IEngineObject))
            {
                var go = obj as GameObject;
                if (go != default)
                    return new UnityEngineObject(go) as T;
            }

            // 对于其他类型（Texture2D, Sprite, AudioClip 等），直接返回
            if (obj is T typedObj)
                return typedObj;

            return null;
        }

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；不存在或类型不匹配时返回 null。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var obj = await LoadObjectAsync(path, token);
#else
            var obj = await LoadObjectAsync(path, token).ConfigureAwait(false);
#endif
            if (obj == default)
                return null;

            return ConvertResource<T>(obj);
        }

        /// <summary>
        /// 实例化资源路径对应的预制体。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>实例化后的引擎对象；加载失败时返回 null。</returns>
        public IEngineObject Instantiate(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == default)
                return null;

            var instance = UnityEngine.Object.Instantiate(prefab);
            return new UnityEngineObject(instance);
        }

        /// <summary>
        /// 同步加载原始二进制资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
        public byte[] LoadRaw(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            return asset != null ? asset.bytes : null;
        }

        /// <summary>
        /// 同步加载原始文本资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
        public string LoadRawText(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            return asset != null ? asset.text : null;
        }

        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var asset = await LoadTextAssetAsync(path, token);
#else
            var asset = await LoadTextAssetAsync(path, token).ConfigureAwait(false);
#endif
            return asset != null ? asset.bytes : null;
        }

        /// <summary>
        /// 异步加载原始文本资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var asset = await LoadTextAssetAsync(path, token);
#else
            var asset = await LoadTextAssetAsync(path, token).ConfigureAwait(false);
#endif
            return asset != null ? asset.text : null;
        }

        /// <summary>
        /// 获取原始文件路径。Resources 后端不暴露真实文件路径。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>始终返回 null。</returns>
        public string GetRawFilePath(string path)
        {
            return null;
        }

        /// <summary>
        /// 释放资源引用。Resources 系统不支持手动释放，此方法保留统一接口形状。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        public void Release(object asset)
        {
            // Resources 系统由引擎管理生命周期；YooAsset 适配器会在此释放 AssetHandle。
            if (asset is UnityEngineObject uobj && uobj.GameObject != default)
            {
                // 仅对实例化对象执行 Destroy
            }
        }

        private static T ConvertResource<T>(UnityEngine.Object obj) where T : class
        {
            if (typeof(T) == typeof(GameObject))
                return obj as T;

            if (typeof(T) == typeof(IEngineObject))
            {
                var go = obj as GameObject;
                if (go != default)
                    return new UnityEngineObject(go) as T;
            }

            return obj as T;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask<TextAsset> LoadTextAssetAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync<TextAsset>(path);
            await request.ToUniTask(cancellationToken: token);
            return request.asset as TextAsset;
        }

        private static async UniTask<UnityEngine.Object> LoadObjectAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync(path);
            await request.ToUniTask(cancellationToken: token);
            return request.asset;
        }
#else
        private static async Task<TextAsset> LoadTextAssetAsync(string path, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync<TextAsset>(path);
            var tcs = new TaskCompletionSource<TextAsset>();
            var registration = token.Register(() =>
            {
                tcs.TrySetCanceled(token);
            });

            try
            {
                request.completed += _ =>
                {
                    if (token.IsCancellationRequested)
                        tcs.TrySetCanceled(token);
                    else
                        tcs.TrySetResult(request.asset as TextAsset);
                };

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                registration.Dispose();
            }
        }

        private static async Task<UnityEngine.Object> LoadObjectAsync(string path, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync(path);
            var tcs = new TaskCompletionSource<ResourceRequest>();
            var registration = token.Register(() =>
            {
                tcs.TrySetCanceled(token);
            });

            try
            {
                request.completed += _ =>
                {
                    if (token.IsCancellationRequested)
                        tcs.TrySetCanceled(token);
                    else
                        tcs.TrySetResult(request);
                };

                await tcs.Task.ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                return request.asset;
            }
            finally
            {
                registration.Dispose();
            }
        }
#endif
    }
}
#endif
