#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>根据初始化参数生成当前 YooAsset 主版本的主备资源地址。</summary>
#if YOKIFRAME_YOOASSET_3
    internal sealed class YooAssetRemoteServices : IRemoteService
#else
    internal sealed class YooAssetRemoteServices : IRemoteServices
#endif
    {
        private readonly string mDefaultHostServer;
        private readonly string mFallbackHostServer;

        /// <summary>创建规范化末尾斜杠的主备远端服务。</summary>
        internal YooAssetRemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            mDefaultHostServer = TrimEndSlash(defaultHostServer);
            mFallbackHostServer = string.IsNullOrWhiteSpace(fallbackHostServer)
                ? mDefaultHostServer
                : TrimEndSlash(fallbackHostServer);
        }

#if YOKIFRAME_YOOASSET_3
        /// <summary>按优先级生成主备资源服务器文件地址，重复地址只保留一次。</summary>
        IReadOnlyList<string> IRemoteService.GetRemoteUrls(string fileName)
        {
            var urls = new List<string>(2) { mDefaultHostServer + "/" + fileName };
            if (mFallbackHostServer != mDefaultHostServer)
            {
                urls.Add(mFallbackHostServer + "/" + fileName);
            }

            return urls;
        }
#else
        /// <summary>生成主资源服务器文件地址。</summary>
        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return mDefaultHostServer + "/" + fileName;
        }

        /// <summary>生成备用资源服务器文件地址。</summary>
        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return mFallbackHostServer + "/" + fileName;
        }
#endif

        /// <summary>移除地址末尾斜杠，避免拼接时产生重复分隔符。</summary>
        private static string TrimEndSlash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().TrimEnd('/');
        }
    }
}
#endif
