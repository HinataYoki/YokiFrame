#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.Unity.Tests
{
    /// <summary>验证 YooAsset 初始化参数的默认值、策略校验和加密映射。</summary>
    public sealed class YooAssetInitializationOptionsTests
    {
        /// <summary>空 package 列表必须回退到稳定的默认 package 名称。</summary>
        [Test]
        public void PrimaryPackageName_空列表_回退默认包名()
        {
            YooAssetInitializationOptions options = new()
            {
                PackageNames = new List<string>()
            };

            Assert.That(
                options.PrimaryPackageName,
                Is.EqualTo(YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME));
        }

        /// <summary>跳过纯空白项后，使用下一个有效名称并去掉首尾空白。</summary>
        [Test]
        public void PrimaryPackageName_首项只有空白_使用下一个有效名称()
        {
            YooAssetInitializationOptions options = new()
            {
                PackageNames = new List<string> { "   ", "  RuntimePackage  " }
            };

            Assert.That(options.PrimaryPackageName, Is.EqualTo("RuntimePackage"));
        }

        /// <summary>非正数清单超时回退默认值，正数保持调用方设置。</summary>
        [Test]
        public void GetManifestTimeoutSeconds_非正数与正数_回退默认或保留()
        {
            YooAssetInitializationOptions options = new()
            {
                ManifestTimeoutSeconds = 0
            };
            Assert.That(
                options.GetManifestTimeoutSeconds(),
                Is.EqualTo(YooAssetInitializationOptions.DEFAULT_MANIFEST_TIMEOUT_SECONDS));

            options.ManifestTimeoutSeconds = 15;
            Assert.That(options.GetManifestTimeoutSeconds(), Is.EqualTo(15));
        }

        /// <summary>默认联网策略保持仅清单，越界下载参数被钳制到安全范围。</summary>
        [Test]
        public void 构造_默认策略与越界下载参数_清单优先并钳制()
        {
            YooAssetInitializationOptions options = new()
            {
                DownloadMaximumConcurrency = 100,
                DownloadRetryCount = -1,
                DownloadMaxRequestPerFrame = 0,
                DownloadNoProgressTimeoutSeconds = 0
            };

            Assert.That(
                options.InitializationStrategy,
                Is.EqualTo(YooAssetInitializationStrategy.ManifestOnly));
            Assert.That(options.AppendTimestampToVersionRequest, Is.True);
            Assert.That(options.GetDownloadMaximumConcurrency(), Is.EqualTo(32));
            Assert.That(options.GetDownloadRetryCount(), Is.EqualTo(0));
            Assert.That(options.GetDownloadMaxRequestPerFrame(), Is.EqualTo(1));
            Assert.That(
                options.GetDownloadNoProgressTimeoutSeconds(),
                Is.EqualTo(YooAssetInitializationOptions.DEFAULT_DOWNLOAD_NO_PROGRESS_TIMEOUT_SECONDS));
        }

        /// <summary>编辑器模拟模式应接受仅用于联网运行模式的回退策略。</summary>
        [Test]
        public void ValidateStrategy_编辑器模拟且远端后离线_不抛出()
        {
            YooAssetInitializationOptions options = new()
            {
                EditorPlayMode = EPlayMode.EditorSimulateMode,
                InitializationStrategy = YooAssetInitializationStrategy.RemoteThenOffline
            };

            Assert.DoesNotThrow(() => YooAssetInitializer.ValidateStrategy(options));
        }

        /// <summary>Web 模式不允许复用本地 package 回退。</summary>
        [Test]
        public void ValidateStrategy_Web且缓存回退_抛出无效操作()
        {
            YooAssetInitializationOptions options = new()
            {
                EditorPlayMode = EPlayMode.WebPlayMode,
                InitializationStrategy = YooAssetInitializationStrategy.RemoteThenCached
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => YooAssetInitializer.ValidateStrategy(options));
            Assert.AreEqual(
                "WebPlayMode does not support local package fallback strategies.",
                exception.Message);
        }

        /// <summary>内置加密方案必须映射为当前主版本的成对实现类型。</summary>
        [TestCase(YooAssetEncryptionMode.XorStream)]
        [TestCase(YooAssetEncryptionMode.FileOffset)]
        [TestCase(YooAssetEncryptionMode.Aes)]
        public void 加密工厂_内置方案_返回成对实现(YooAssetEncryptionMode mode)
        {
            YooAssetInitializationOptions options = new() { EncryptionMode = mode };

#if YOKIFRAME_YOOASSET_3
            Assert.IsInstanceOf(
                EncryptionType(mode),
                YooAssetEncryptionServices.CreateBundleEncryptor(options));
            Assert.IsInstanceOf(
                DecryptionType(mode),
                YooAssetEncryptionServices.CreateBundleDecryptor(options));
#else
            Assert.IsInstanceOf(
                EncryptionType(mode),
                YooAssetEncryptionServices.CreateEncryptionServices(options));
            Assert.IsInstanceOf(
                DecryptionType(mode),
                YooAssetEncryptionServices.CreateDecryptionServices(options));
#endif
        }

        /// <summary>构建参数必须直接持有当前方案对应的加密器实例。</summary>
        [TestCase(YooAssetEncryptionMode.XorStream)]
        [TestCase(YooAssetEncryptionMode.FileOffset)]
        [TestCase(YooAssetEncryptionMode.Aes)]
        public void CreateBuildParameters_内置方案_安装对应加密器(YooAssetEncryptionMode mode)
        {
            YooAssetInitializationOptions options = new() { EncryptionMode = mode };
            BuildParameters parameters = YooAssetPackageBuilder.CreateBuildParameters(
                YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME,
                nameof(EBuildPipeline.RawFileBuildPipeline),
                options,
                BuildTarget.StandaloneWindows64,
                "test-version");

#if YOKIFRAME_YOOASSET_3
            Assert.IsInstanceOf(EncryptionType(mode), parameters.BundleEncryptor);
#else
            Assert.IsInstanceOf(EncryptionType(mode), parameters.EncryptionServices);
#endif
        }

        /// <summary>结构守卫：常用加解密实现保持公开，供项目代码直接复用。</summary>
        [Test]
        public void AssemblyBoundary_常用加解密类型_保持公开()
        {
#if YOKIFRAME_YOOASSET_3
            Assert.That(typeof(YooAssetXorBundleEncryptor).IsPublic, Is.True);
            Assert.That(typeof(YooAssetXorStreamDecryptor).IsPublic, Is.True);
            Assert.That(typeof(YooAssetFileOffsetBundleEncryptor).IsPublic, Is.True);
            Assert.That(typeof(YooAssetFileOffsetDecryptor).IsPublic, Is.True);
            Assert.That(typeof(YooAssetAesBundleEncryptor).IsPublic, Is.True);
            Assert.That(typeof(YooAssetAesDecryptor).IsPublic, Is.True);
#else
            Assert.That(typeof(YooAssetXorStreamEncryptionService).IsPublic, Is.True);
            Assert.That(typeof(YooAssetXorStreamDecryptionService).IsPublic, Is.True);
            Assert.That(typeof(YooAssetFileOffsetEncryptionService).IsPublic, Is.True);
            Assert.That(typeof(YooAssetFileOffsetDecryptionService).IsPublic, Is.True);
            Assert.That(typeof(YooAssetAesEncryptionService).IsPublic, Is.True);
            Assert.That(typeof(YooAssetAesDecryptionService).IsPublic, Is.True);
#endif
        }

        /// <summary>返回当前主版本中指定内置方案的构建加密类型。</summary>
        /// <param name="mode">内置加密方案。</param>
        /// <returns>对应该方案的加密实现类型。</returns>
        private static Type EncryptionType(YooAssetEncryptionMode mode)
        {
            switch (mode)
            {
                case YooAssetEncryptionMode.XorStream:
#if YOKIFRAME_YOOASSET_3
                    return typeof(YooAssetXorBundleEncryptor);
#else
                    return typeof(YooAssetXorStreamEncryptionService);
#endif
                case YooAssetEncryptionMode.FileOffset:
#if YOKIFRAME_YOOASSET_3
                    return typeof(YooAssetFileOffsetBundleEncryptor);
#else
                    return typeof(YooAssetFileOffsetEncryptionService);
#endif
                case YooAssetEncryptionMode.Aes:
#if YOKIFRAME_YOOASSET_3
                    return typeof(YooAssetAesBundleEncryptor);
#else
                    return typeof(YooAssetAesEncryptionService);
#endif
                default:
                    Assert.Fail("未覆盖的加密方案：" + mode);
                    return null;
            }
        }

        /// <summary>返回当前主版本中指定内置方案的运行时解密类型。</summary>
        /// <param name="mode">内置加密方案。</param>
        /// <returns>对应该方案的解密实现类型。</returns>
        private static Type DecryptionType(YooAssetEncryptionMode mode)
        {
            switch (mode)
            {
                case YooAssetEncryptionMode.XorStream:
#if YOKIFRAME_YOOASSET_3
                    return typeof(YooAssetXorStreamDecryptor);
#else
                    return typeof(YooAssetXorStreamDecryptionService);
#endif
                case YooAssetEncryptionMode.FileOffset:
#if YOKIFRAME_YOOASSET_3
                    return typeof(YooAssetFileOffsetDecryptor);
#else
                    return typeof(YooAssetFileOffsetDecryptionService);
#endif
                case YooAssetEncryptionMode.Aes:
#if YOKIFRAME_YOOASSET_3
                    return typeof(YooAssetAesDecryptor);
#else
                    return typeof(YooAssetAesDecryptionService);
#endif
                default:
                    Assert.Fail("未覆盖的加密方案：" + mode);
                    return null;
            }
        }
    }
}
#endif
