#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.Threading;
using YooAsset;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    internal sealed class YooAssetV3RawFileBackend : IYooAssetRawFileBackend
    {
        private const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        private readonly ResourcePackage mPackage;
        private readonly string mPackageName;
        private readonly bool mEditorSimulateMode;

        public YooAssetV3RawFileBackend(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        internal YooAssetV3RawFileBackend(ResourcePackage package, bool editorSimulateMode)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
            mEditorSimulateMode = editorSimulateMode;
        }

        public YooAssetV3RawFileBackend(string packageName)
        {
            mPackageName = string.IsNullOrEmpty(packageName) ? DEFAULT_PACKAGE_NAME : packageName;
        }

        internal YooAssetV3RawFileBackend(string packageName, bool editorSimulateMode)
        {
            mPackageName = string.IsNullOrEmpty(packageName) ? DEFAULT_PACKAGE_NAME : packageName;
            mEditorSimulateMode = editorSimulateMode;
        }

        public byte[] LoadRaw(string path)
        {
            var handle = mEditorSimulateMode
                ? ResolvePackage().LoadAssetSync<TextAsset>(path)
                : ResolvePackage().LoadAssetSync<RawFileObject>(path);
            try
            {
                if (mEditorSimulateMode)
                {
                    var textAsset = handle.GetAssetObject<TextAsset>();
                    return textAsset != null ? textAsset.bytes : null;
                }

                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != null ? rawObject.GetBytes() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        public string LoadRawText(string path)
        {
            var handle = mEditorSimulateMode
                ? ResolvePackage().LoadAssetSync<TextAsset>(path)
                : ResolvePackage().LoadAssetSync<RawFileObject>(path);
            try
            {
                if (mEditorSimulateMode)
                {
                    var textAsset = handle.GetAssetObject<TextAsset>();
                    return textAsset != null ? textAsset.text : null;
                }

                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != null ? rawObject.GetText() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        public string GetRawFilePath(string path)
        {
            var handle = mEditorSimulateMode
                ? ResolvePackage().LoadAssetSync<TextAsset>(path)
                : ResolvePackage().LoadAssetSync<RawFileObject>(path);
            try
            {
                return handle.GetAssetInfo().AssetPath;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token)
#else
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token)
#endif
        {
            var handle = mEditorSimulateMode
                ? ResolvePackage().LoadAssetAsync<TextAsset>(path)
                : ResolvePackage().LoadAssetAsync<RawFileObject>(path);
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
                await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
                if (mEditorSimulateMode)
                {
                    var textAsset = handle.GetAssetObject<TextAsset>();
                    return textAsset != null ? textAsset.bytes : null;
                }

                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != null ? rawObject.GetBytes() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token)
#else
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token)
#endif
        {
            var handle = mEditorSimulateMode
                ? ResolvePackage().LoadAssetAsync<TextAsset>(path)
                : ResolvePackage().LoadAssetAsync<RawFileObject>(path);
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
                await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
                if (mEditorSimulateMode)
                {
                    var textAsset = handle.GetAssetObject<TextAsset>();
                    return textAsset != null ? textAsset.text : null;
                }

                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != null ? rawObject.GetText() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        private ResourcePackage ResolvePackage()
        {
            if (mPackage != null)
                return mPackage;

            return YooAssets.GetPackage(mPackageName);
        }
    }
}
#endif
#endif
