#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
#if YOKIFRAME_YOOASSET_3
using System.Collections.Generic;
#endif
using NUnit.Framework;
using YooAsset;

namespace YokiFrame.Unity.Tests
{
    /// <summary>验证 YooAsset 远端服务按主备优先级返回地址。</summary>
    public sealed class YooAssetSceneCompatibilityTests
    {
        /// <summary>验证主地址和备用地址按该顺序返回。</summary>
        [Test]
        public void YooAssetRemoteServices_主备地址_按主备顺序返回()
        {
            YooAssetRemoteServices service = new(
                "https://main.example.com/",
                "https://fallback.example.com/");

#if YOKIFRAME_YOOASSET_3
            IReadOnlyList<string> urls = ((IRemoteService)service).GetRemoteUrls("bundle.bin");
            CollectionAssert.AreEqual(
                new[]
                {
                    "https://main.example.com/bundle.bin",
                    "https://fallback.example.com/bundle.bin"
                },
                urls);
#else
            IRemoteServices remoteServices = (IRemoteServices)service;
            Assert.AreEqual(
                "https://main.example.com/bundle.bin",
                remoteServices.GetRemoteMainURL("bundle.bin"));
            Assert.AreEqual(
                "https://fallback.example.com/bundle.bin",
                remoteServices.GetRemoteFallbackURL("bundle.bin"));
#endif
        }
    }
}
#endif
