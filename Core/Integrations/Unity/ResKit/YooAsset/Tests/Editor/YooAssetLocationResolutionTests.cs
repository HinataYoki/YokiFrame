#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Unity.Tests
{
    /// <summary>守护 YooAsset 多包路径解析保持显式语法，且加载入口实际消费解析结果。</summary>
    public sealed class YooAssetLocationResolutionTests
    {
        private const string LOCATION_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetLocation.cs";
        private const string PROVIDER_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.cs";
        private const string RESOLUTION_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.Resolution.cs";

        /// <summary>验证普通路径保持自动探测，不把冒号误判成包名前缀。</summary>
        [Test]
        public void PlainPathKeepsAutomaticResolution()
        {
            YooAssetLocation.Split("Prefabs/Enemy", out string packageName, out string location);

            Assert.IsNull(packageName);
            Assert.AreEqual("Prefabs/Enemy", location);
        }

        /// <summary>验证显式语法只切出包名，剩余部分原样交给 YooAsset。</summary>
        [Test]
        public void ExplicitPrefixSeparatesPackageAndLocation()
        {
            YooAssetLocation.Split(
                "package:DLC/Prefabs/Enemy",
                out string packageName,
                out string location);

            Assert.AreEqual("DLC", packageName);
            Assert.AreEqual("Prefabs/Enemy", location);
        }

        /// <summary>验证残缺显式路径在探测前失败，避免把非法前缀送进任意 package。</summary>
        [Test]
        public void IncompleteExplicitPrefixIsRejected()
        {
            Assert.Throws<ArgumentException>(
                () => YooAssetLocation.Split("package:DLC", out _, out _));
            Assert.Throws<ArgumentException>(
                () => YooAssetLocation.Split("package:DLC/", out _, out _));
        }

        /// <summary>验证资源、raw 和场景入口都经过同一 package 解析，而不是绑定构造时的单个包。</summary>
        [Test]
        public void LoadEntriesResolvePackageBeforeCallingYooAsset()
        {
            string providerSource = ReadSource(PROVIDER_PATH);
            string resolutionSource = ReadSource(RESOLUTION_PATH);
            string sceneSource = ReadSource(
                "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.Scene.cs");

            StringAssert.Contains("ResolvePackage(path, out string location)", providerSource);
#if YOKIFRAME_YOOASSET_3
            StringAssert.Contains("IsLocationValid(candidate, location)", resolutionSource);
            StringAssert.Contains("return package.IsLocationValid(location);", resolutionSource);
#else
            StringAssert.Contains("IsLocationValid(candidate, location)", resolutionSource);
            StringAssert.Contains("return package.CheckLocationValid(location);", resolutionSource);
#endif
            StringAssert.Contains("RequirePackage(packages, packageName)", resolutionSource);
            StringAssert.Contains("ResolvePackage(request.SceneName, out string location)", sceneSource);
            StringAssert.Contains("public void AddPackage", resolutionSource);
            StringAssert.Contains("public bool RemovePackage", resolutionSource);
            StringAssert.DoesNotContain("private readonly ResourcePackage[] mPackages", providerSource);
            StringAssert.DoesNotContain("mPackage.Load", providerSource);
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
