#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.Unity.Tests
{
    /// <summary>验证 YooAsset 初始化参数的默认值和规范化行为。</summary>
    public sealed class YooAssetInitializationOptionsTests
    {
        /// <summary>空 package 列表必须回退到稳定的默认 package 名称。</summary>
        [Test]
        public void EmptyPackageListUsesDefaultPackageName()
        {
            YooAssetInitializationOptions options = new()
            {
                PackageNames = new List<string>()
            };

            Assert.That(
                options.PrimaryPackageName,
                Is.EqualTo(YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME));
        }

        /// <summary>首个有效 package 名称应去除首尾空白并作为默认包。</summary>
        [Test]
        public void PrimaryPackageNameTrimsFirstValidName()
        {
            YooAssetInitializationOptions options = new()
            {
                PackageNames = new List<string> { "   ", "  RuntimePackage  " }
            };

            Assert.That(options.PrimaryPackageName, Is.EqualTo("RuntimePackage"));
        }

        /// <summary>非正数超时应回退到默认值，正数保持调用方设置。</summary>
        [Test]
        public void ManifestTimeoutUsesPositiveConfiguredValue()
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

        /// <summary>内置加密方案必须能够映射为当前 YooAsset 主版本的成对服务。</summary>
        [TestCase(YooAssetEncryptionMode.XorStream)]
        [TestCase(YooAssetEncryptionMode.FileOffset)]
        [TestCase(YooAssetEncryptionMode.Aes)]
        public void EncryptionModeCreatesMatchingServices(YooAssetEncryptionMode mode)
        {
            YooAssetInitializationOptions options = new() { EncryptionMode = mode };

#if YOKIFRAME_YOOASSET_3
            IBundleEncryptor encryption = YooAssetEncryptionServices.CreateBundleEncryptor(options);
            IBundleDecryptor decryption = YooAssetEncryptionServices.CreateBundleDecryptor(options);
#else
            IEncryptionServices encryption = YooAssetEncryptionServices.CreateEncryptionServices(options);
            IDecryptionServices decryption = YooAssetEncryptionServices.CreateDecryptionServices(options);
#endif

            Assert.That(encryption, Is.Not.Null);
            Assert.That(decryption, Is.Not.Null);
        }

        /// <summary>构建参数必须直接持有当前方案创建的参数化加密器实例。</summary>
        [TestCase(YooAssetEncryptionMode.XorStream)]
        [TestCase(YooAssetEncryptionMode.FileOffset)]
        [TestCase(YooAssetEncryptionMode.Aes)]
        public void BuildParametersUseSelectedEncryptionMode(YooAssetEncryptionMode mode)
        {
            YooAssetInitializationOptions options = new() { EncryptionMode = mode };
            BuildParameters parameters = YooAssetPackageBuilder.CreateBuildParameters(
                YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME,
                nameof(EBuildPipeline.RawFileBuildPipeline),
                options,
                BuildTarget.StandaloneWindows64,
                "test-version");

#if YOKIFRAME_YOOASSET_3
            Assert.That(parameters.BundleEncryptor, Is.Not.Null);
#else
            Assert.That(parameters.EncryptionServices, Is.Not.Null);
#endif
        }

        /// <summary>常用加密和解密实现必须作为公开 Integration API 提供给项目代码复用。</summary>
        [Test]
        public void CommonEncryptionImplementationsArePublic()
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
    }
}
#endif
