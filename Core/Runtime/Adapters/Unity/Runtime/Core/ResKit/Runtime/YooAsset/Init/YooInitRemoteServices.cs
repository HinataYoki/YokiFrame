#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using YooAsset;

namespace YokiFrame.Unity
{
    internal sealed class YooInitRemoteServices : IRemoteServices
    {
        private readonly string mDefaultHostServer;
        private readonly string mFallbackHostServer;

        public YooInitRemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            mDefaultHostServer = TrimEndSlash(defaultHostServer);
            mFallbackHostServer = string.IsNullOrEmpty(fallbackHostServer) ? mDefaultHostServer : TrimEndSlash(fallbackHostServer);
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return mDefaultHostServer + "/" + fileName;
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return mFallbackHostServer + "/" + fileName;
        }

        private static string TrimEndSlash(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.TrimEnd('/');
        }
    }
}
#endif
#endif
