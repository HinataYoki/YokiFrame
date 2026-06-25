#if GODOT
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// IResourceProvider 的 Godot 实现，基于 GD.Load。
    /// </summary>
    public sealed class GodotResourceProvider : IResourceProvider, IRawResourceProvider
    {
        public string ProviderName => "Godot.ResourceLoader";

        public T Load<T>(string path) where T : class
        {
            var resource = ResourceLoader.Load(path);
            if (resource == null)
                return null;

            if (typeof(T) == typeof(IEngineObject))
            {
                if (resource is PackedScene packedScene)
                    return new GodotEngineObject(packedScene) as T;

                return null;
            }

            return resource as T;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
            if (token.IsCancellationRequested)
#if YOKIFRAME_UNITASK_SUPPORT
                return UniTask.FromCanceled<T>(token);
#else
                return Task.FromCanceled<T>(token);
#endif

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(Load<T>(path));
#else
            return Task.FromResult(Load<T>(path));
#endif
        }

        public IEngineObject Instantiate(string path)
        {
            var packedScene = GD.Load<PackedScene>(path);
            if (packedScene == null)
                return null;

            var instance = packedScene.Instantiate<Node>();
            return new GodotEngineObject(instance);
        }

        public byte[] LoadRaw(string path)
        {
            if (!FileAccess.FileExists(path))
                return null;

            return FileAccess.GetFileAsBytes(path);
        }

        public string LoadRawText(string path)
        {
            if (!FileAccess.FileExists(path))
                return null;

            return FileAccess.GetFileAsString(path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        public Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
            if (token.IsCancellationRequested)
#if YOKIFRAME_UNITASK_SUPPORT
                return UniTask.FromCanceled<byte[]>(token);
#else
                return Task.FromCanceled<byte[]>(token);
#endif

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(LoadRaw(path));
#else
            return Task.FromResult(LoadRaw(path));
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        public Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
            if (token.IsCancellationRequested)
#if YOKIFRAME_UNITASK_SUPPORT
                return UniTask.FromCanceled<string>(token);
#else
                return Task.FromCanceled<string>(token);
#endif

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(LoadRawText(path));
#else
            return Task.FromResult(LoadRawText(path));
#endif
        }

        public string GetRawFilePath(string path)
        {
            if (!FileAccess.FileExists(path))
                return null;

            return ProjectSettings.GlobalizePath(path);
        }

        public void Release(object asset)
        {
            if (asset is GodotEngineObject godotObject)
                godotObject.Destroy();
        }
    }
}
#endif
