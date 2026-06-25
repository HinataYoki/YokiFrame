#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    internal interface IYooAssetRawFileBackend
    {
        byte[] LoadRaw(string path);
        string LoadRawText(string path);
        string GetRawFilePath(string path);
#if YOKIFRAME_UNITASK_SUPPORT
        UniTask<byte[]> LoadRawAsync(string path, CancellationToken token);
        UniTask<string> LoadRawTextAsync(string path, CancellationToken token);
#else
        Task<byte[]> LoadRawAsync(string path, CancellationToken token);
        Task<string> LoadRawTextAsync(string path, CancellationToken token);
#endif
    }
}
#endif
#endif
