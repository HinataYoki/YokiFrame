using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>定义无需进入 ResKit 对象缓存的 raw bytes 和文本能力。</summary>
    public interface IRawResourceProvider
    {
        /// <summary>同步读取 raw bytes；未找到时返回 null。</summary>
        byte[] LoadRaw(string path);

        /// <summary>同步读取 UTF-8 或 Provider 约定编码的 raw 文本；未找到时返回 null。</summary>
        string LoadRawText(string path);

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 raw bytes；取消令牌只约束本次读取。</summary>
        UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default);

        /// <summary>异步读取 raw 文本；取消令牌只约束本次读取。</summary>
        UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default);
#else
        /// <summary>异步读取 raw bytes；取消令牌只约束本次读取。</summary>
        Task<byte[]> LoadRawAsync(string path, CancellationToken token = default);

        /// <summary>异步读取 raw 文本；取消令牌只约束本次读取。</summary>
        Task<string> LoadRawTextAsync(string path, CancellationToken token = default);
#endif

    }
}
