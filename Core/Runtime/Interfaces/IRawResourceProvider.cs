using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 原始资源提供者抽象接口，用于读取文本、bytes 等非实例化资源。
    /// </summary>
    public interface IRawResourceProvider
    {
        /// <summary>
        /// 同步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
        byte[] LoadRaw(string path);

        /// <summary>
        /// 同步加载原始文本资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
        string LoadRawText(string path);
#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
        UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default);

        /// <summary>
        /// 异步加载原始文本资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
        UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default);
#else
        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
        Task<byte[]> LoadRawAsync(string path, CancellationToken token = default);

        /// <summary>
        /// 异步加载原始文本资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
        Task<string> LoadRawTextAsync(string path, CancellationToken token = default);
#endif
        /// <summary>
        /// 获取原始文件在宿主文件系统中的可访问路径。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>文件系统路径；宿主不支持时返回 null。</returns>
        string GetRawFilePath(string path);
    }
}
