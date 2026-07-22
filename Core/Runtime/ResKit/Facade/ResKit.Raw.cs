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
        /// <summary>通过当前 Provider 的可选 raw capability 同步读取二进制内容。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <returns>二进制内容；未找到时由 Provider 返回 null。</returns>
        public static byte[] LoadRaw(string path)
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRaw(path);
        }

        /// <summary>作为 <see cref="LoadRaw"/> 的语义化别名同步读取二进制内容。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <returns>二进制内容；未找到时由 Provider 返回 null。</returns>
        public static byte[] LoadRawBytes(string path) => LoadRaw(path);

        /// <summary>通过当前 Provider 的可选 raw capability 同步读取文本。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <returns>文本内容；未找到时由 Provider 返回 null。</returns>
        public static string LoadRawText(string path)
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRawText(path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 raw 二进制内容；取消令牌直接约束本次 Provider 调用。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <param name="token">约束本次 raw 读取的取消令牌。</param>
        /// <returns>二进制内容；未找到时由 Provider 返回 null。</returns>
        public static UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        /// <summary>异步读取 raw 二进制内容；取消令牌直接约束本次 Provider 调用。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <param name="token">约束本次 raw 读取的取消令牌。</param>
        /// <returns>二进制内容；未找到时由 Provider 返回 null。</returns>
        public static Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRawAsync(path, token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>作为 <see cref="LoadRawAsync"/> 的语义化别名异步读取二进制内容。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <param name="token">约束本次 raw 读取的取消令牌。</param>
        /// <returns>二进制内容；未找到时由 Provider 返回 null。</returns>
        public static UniTask<byte[]> LoadRawBytesAsync(string path, CancellationToken token = default)
#else
        /// <summary>作为 <see cref="LoadRawAsync"/> 的语义化别名异步读取二进制内容。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <param name="token">约束本次 raw 读取的取消令牌。</param>
        /// <returns>二进制内容；未找到时由 Provider 返回 null。</returns>
        public static Task<byte[]> LoadRawBytesAsync(string path, CancellationToken token = default)
#endif
        {
            return LoadRawAsync(path, token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 raw 文本内容；取消令牌直接约束本次 Provider 调用。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <param name="token">约束本次 raw 读取的取消令牌。</param>
        /// <returns>文本内容；未找到时由 Provider 返回 null。</returns>
        public static UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        /// <summary>异步读取 raw 文本内容；取消令牌直接约束本次 Provider 调用。</summary>
        /// <param name="path">Provider 可识别的 raw 资源路径。</param>
        /// <param name="token">约束本次 raw 读取的取消令牌。</param>
        /// <returns>文本内容；未找到时由 Provider 返回 null。</returns>
        public static Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
            EnsurePath(path);
            return EnsureRawProvider().LoadRawTextAsync(path, token);
        }

        /// <summary>获取当前 Provider 的 raw capability，不支持时抛出明确异常。</summary>
        private static IRawResourceProvider EnsureRawProvider()
        {
            IResourceProvider provider;
            lock (sLock)
            {
                provider = EnsureProviderLocked();
            }

            if (provider is IRawResourceProvider rawProvider)
            {
                return rawProvider;
            }

            Type providerType = provider.GetType();
            string providerName = providerType.FullName ?? providerType.Name;
            throw new NotSupportedException(
                "ResKit provider '" + providerName + "' does not support raw resources.");
        }

        /// <summary>校验调用路径非空，所有普通与 raw API 共用同一参数约束。</summary>
        private static void EnsurePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Resource path cannot be null or empty.", nameof(path));
            }
        }
    }
}
