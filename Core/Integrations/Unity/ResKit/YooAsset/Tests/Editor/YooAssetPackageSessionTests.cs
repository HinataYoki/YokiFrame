#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Unity.Tests
{
    /// <summary>守护 YooAsset 包会话可以重复接入，且热加包不再替换 ResKit Provider。</summary>
    public sealed class YooAssetPackageSessionTests
    {
        private const string INITIALIZER_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/Initialization/YooAssetInitializer.cs";
        private const string SESSION_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/Initialization/YooAssetInitializer.Session.cs";
        private const string NETWORK_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/Initialization/YooAssetInitializer.Network.cs";
        private const string OBSERVER_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/Initialization/YooAssetDownloaderObserver.cs";

        /// <summary>验证批量初始化会逐包接入，已有 Provider 只追加，不再整表替换。</summary>
        [Test]
        public void BatchInitializationAttachesPackagesWithoutReplacingProvider()
        {
            string source = ReadSource(INITIALIZER_PATH);
            string sessionSource = ReadSource(SESSION_PATH);

            StringAssert.Contains("InitializePackageAsync(packageNames[index], options, token)", source);
            StringAssert.Contains("provider.AddPackage(package)", sessionSource);
            StringAssert.Contains("if (provider == null)", sessionSource);
            StringAssert.DoesNotContain("if (IsInitialized)\r\n                return;", source);
            StringAssert.DoesNotContain("if (IsInitialized)\n                return;", source);
            StringAssert.DoesNotContain("new YooAssetResourceProvider(packages, editorSimulateMode)", source);
            StringAssert.DoesNotContain("new YooAssetResourceProvider(packages, editorSimulateMode)", sessionSource);
        }

        /// <summary>验证运行期更新、预下载、下载和销毁都有独立入口，且预下载不激活清单。</summary>
        [Test]
        public void PackageSessionsExposeUpdatePrefetchAndDestroy()
        {
            string source = ReadSource(SESSION_PATH);
            string networkSource = ReadSource(NETWORK_PATH);

            StringAssert.Contains("UpdatePackageAsync(", source);
            StringAssert.Contains("PrefetchPackageAsync", source);
            StringAssert.Contains("DestroyPackageAsync", source);
            StringAssert.Contains("RemovePackage(packageName)", source);
            StringAssert.Contains("PreDownloadContentAsync", networkSource);
            StringAssert.Contains("PrefetchManifestAsync", networkSource);
        }

        /// <summary>验证下载进度只在真正有文件时转发，空下载器不会触发回调。</summary>
        [Test]
        public void DownloadObserverSkipsEmptyDownloader()
        {
            string source = ReadSource(OBSERVER_PATH);

            StringAssert.Contains("downloader.TotalDownloadCount <= 0", source);
            StringAssert.Contains("DownloadUpdateCallback", source);
            StringAssert.Contains("DownloadProgressChanged", source);
            StringAssert.Contains("DownloadErrorCallback", source);
            StringAssert.Contains("DownloadFileBeginCallback", source);
        }

        /// <summary>将 Assets 相对路径解析为当前 Unity 工程中的源码路径。</summary>
        private static string ReadSource(string relativePath)
        {
            string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
            return File.ReadAllText(sourcePath);
        }
    }
}
#endif
