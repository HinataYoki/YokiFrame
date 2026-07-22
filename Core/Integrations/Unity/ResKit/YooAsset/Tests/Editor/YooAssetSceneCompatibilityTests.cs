#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YooAsset;

namespace YokiFrame.Unity.Tests
{
    /// <summary>验证 YooAsset V2/V3 场景激活和远端文件系统的兼容映射。</summary>
    public sealed class YooAssetSceneCompatibilityTests
    {
        private const string SCENE_PROVIDER_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.Scene.cs";
        private const string SCENE_OPERATION_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetSceneLoadOperation.cs";
        private const string INITIALIZER_OPERATIONS_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/Initialization/YooAssetInitializer.Operations.cs";

        /// <summary>验证场景加载参数和恢复 API 始终随当前 YooAsset 主版本切换。</summary>
        [Test]
        public void SceneActivationUsesCurrentYooAssetVersionSemantics()
        {
            string providerSource = ReadSource(SCENE_PROVIDER_PATH);
            string operationSource = ReadSource(SCENE_OPERATION_PATH);

#if YOKIFRAME_YOOASSET_3
            StringAssert.Contains("bool allowSceneActivation = !shouldSuspend;", providerSource);
            StringAssert.Contains("allowSceneActivation);", providerSource);
            StringAssert.Contains("mHandle.AllowSceneActivation()", operationSource);
#else
            StringAssert.Contains("bool suspendLoad = shouldSuspend;", providerSource);
            StringAssert.Contains("suspendLoad);", providerSource);
            StringAssert.Contains("mHandle.UnSuspend()", operationSource);
#endif
        }

        /// <summary>验证挂起状态只会在预加载阈值实际到达后对 SceneKit 可见。</summary>
        [Test]
        public void SceneSuspensionStateRequiresReachedThreshold()
        {
            string operationSource = ReadSource(SCENE_OPERATION_PATH);

            StringAssert.Contains("mSuspensionReached", operationSource);
            StringAssert.Contains("!mSceneActivationAllowed", operationSource);
            StringAssert.Contains("Progress >= mSuspendAtProgress", operationSource);
        }

        /// <summary>验证 V3 Host/Web 未注册自定义回调时仍配置默认文件系统。</summary>
        [Test]
        public void V3HostAndWebModesKeepDefaultFileSystemFallbacks()
        {
#if YOKIFRAME_YOOASSET_3
            string source = ReadSource(INITIALIZER_OPERATIONS_PATH);

            StringAssert.Contains("CreateDefaultSandboxFileSystemParameters(remoteService)", source);
            StringAssert.Contains("WebNetworkFileSystemParameters = webNetworkFileSystem", source);
            StringAssert.Contains("return HostInitializationHandler(package, options);", source);
            StringAssert.Contains("return WebInitializationHandler(package, options);", source);
#else
            Assert.Pass("YooAsset V2 使用其原生默认 Host/Web 文件系统路径。");
#endif
        }

        /// <summary>验证 V3 远端服务保留主备 URL 优先级并去除空备用地址产生的重复项。</summary>
        [Test]
        public void RemoteServiceProducesOrderedDistinctUrls()
        {
            object service = CreateRemoteService(
                "https://main.example.com/",
                "https://fallback.example.com/");
            Assert.NotNull(service);

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

        /// <summary>通过公开初始化器所在程序集创建内部远端服务，以验证真实程序集边界内的实现。</summary>
        private static object CreateRemoteService(string defaultHostServer, string fallbackHostServer)
        {
            Type serviceType = typeof(YooAssetInitializer).Assembly.GetType(
                "YokiFrame.Unity.YooAssetRemoteServices", true);
            return Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { defaultHostServer, fallbackHostServer },
                null);
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
