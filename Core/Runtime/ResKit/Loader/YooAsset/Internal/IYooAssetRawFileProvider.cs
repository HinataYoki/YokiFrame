#if YOKIFRAME_YOOASSET_SUPPORT
using System;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载能力抽象。
    /// V2（专用 RawFileHandle API）与 V3（RawFileObject 作为资源加载）差异巨大，
    /// 通过此接口完全隔离。
    /// </summary>
    internal interface IYooAssetRawFileProvider
    {
        string LoadText(string path);
        byte[] LoadData(string path);
        string GetFilePath(string path);
        void LoadTextAsync(string path, Action<string> onComplete);
        void LoadDataAsync(string path, Action<byte[]> onComplete);
        void ReleaseHandle();
    }
}
#endif
