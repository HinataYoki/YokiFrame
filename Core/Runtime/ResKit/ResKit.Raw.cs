using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>
        /// 同步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>资源二进制内容。</returns>
        public static byte[] LoadRaw(string path)
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRaw(path);
        }

        /// <summary>
        /// 同步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>资源二进制内容。</returns>
        public static byte[] LoadRawBytes(string path) => LoadRaw(path);

        /// <summary>
        /// 同步加载原始文本资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>资源文本内容。</returns>
        public static string LoadRawText(string path)
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRawText(path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容。</returns>
        public static UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容。</returns>
        public static Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRawAsync(path, token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容。</returns>
        public static UniTask<byte[]> LoadRawBytesAsync(string path, CancellationToken token = default)
#else
        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容。</returns>
        public static Task<byte[]> LoadRawBytesAsync(string path, CancellationToken token = default)
#endif
        {
            return LoadRawAsync(path, token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载原始文本资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源文本内容。</returns>
        public static UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        /// <summary>
        /// 异步加载原始文本资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源文本内容。</returns>
        public static Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRawTextAsync(path, token);
        }

        /// <summary>
        /// 获取原始文件在宿主中的真实路径。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>宿主可访问的原始文件路径。</returns>
        public static string GetRawFilePath(string path)
        {
            EnsurePath(path);
            return EnsureRawProvider().GetRawFilePath(path);
        }
        private static IRawResourceProvider EnsureRawProvider()
        {
            var provider = EnsureProvider();
            var rawProvider = provider as IRawResourceProvider;
            if (rawProvider == null)
                throw new NotSupportedException("ResKit provider '" + provider.ProviderName + "' does not support raw resources.");

            return rawProvider;
        }

        private static void EnsurePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));
        }
    }
}
