#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 使用初始化参数中的唯一加密方案创建并执行 YooAsset package 构建。
    /// V2/V3 API 差异只保留在本 Editor Integration 边界。
    /// </summary>
    internal static class YooAssetPackageBuilder
    {
        private const string PACKAGE_VERSION_FORMAT = "yyyy-MM-dd-HHmmss";

        /// <summary>
        /// 使用当前活动平台和时间版本执行 package 构建。
        /// </summary>
        /// <param name="packageName">YooAsset package 名称。</param>
        /// <param name="pipelineName">当前主版本支持的构建管线名称。</param>
        /// <param name="options">提供构建加密参数的初始化配置快照。</param>
        /// <returns>YooAsset 原生构建结果。</returns>
        internal static BuildResult Build(
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options)
        {
            BuildParameters parameters = CreateBuildParameters(
                packageName,
                pipelineName,
                options,
                EditorUserBuildSettings.activeBuildTarget,
                DateTime.Now.ToString(PACKAGE_VERSION_FORMAT));
#if YOKIFRAME_YOOASSET_3
            return RunV3Build(parameters);
#else
            return RunV2Build(parameters);
#endif
        }

        /// <summary>
        /// 创建可测试的版本化 YooAsset 构建参数，并直接安装匹配的加密器实例。
        /// </summary>
        /// <param name="packageName">YooAsset package 名称。</param>
        /// <param name="pipelineName">构建管线名称。</param>
        /// <param name="options">初始化和加密配置快照。</param>
        /// <param name="buildTarget">Unity 构建目标。</param>
        /// <param name="packageVersion">本次 package 版本。</param>
        /// <returns>匹配 YooAsset 主版本和构建管线的参数对象。</returns>
        internal static BuildParameters CreateBuildParameters(
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options,
            BuildTarget buildTarget,
            string packageVersion)
        {
            ValidateRequest(packageName, pipelineName, options, buildTarget, packageVersion);
#if YOKIFRAME_YOOASSET_3
            return CreateV3BuildParameters(
                packageName, pipelineName, options, buildTarget, packageVersion);
#else
            return CreateV2BuildParameters(
                packageName, pipelineName, options, buildTarget, packageVersion);
#endif
        }

        /// <summary>校验构建请求的稳定标识和必要参数。</summary>
        private static void ValidateRequest(
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options,
            BuildTarget buildTarget,
            string packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                throw new ArgumentException("YooAsset package name cannot be empty.", nameof(packageName));
            if (string.IsNullOrWhiteSpace(pipelineName))
                throw new ArgumentException("YooAsset build pipeline cannot be empty.", nameof(pipelineName));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (buildTarget == BuildTarget.NoTarget)
                throw new ArgumentException("Unity build target is not selected.", nameof(buildTarget));
            if (string.IsNullOrWhiteSpace(packageVersion))
                throw new ArgumentException("YooAsset package version cannot be empty.", nameof(packageVersion));
        }

#if YOKIFRAME_YOOASSET_3
        /// <summary>按 YooAsset V3 管线创建具体参数类型。</summary>
        private static BuildParameters CreateV3BuildParameters(
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options,
            BuildTarget buildTarget,
            string packageVersion)
        {
            BuildParameters parameters;
            if (pipelineName == nameof(EBuildPipeline.ScriptableBuildPipeline))
            {
                parameters = new ScriptableBuildParameters
                {
                    CompressOption = YooAssetBuildSettingsAdapter.GetCompressOption(
                        packageName, pipelineName),
                    BuiltinShadersBundleName = GetV3BuiltinShadersBundleName(packageName)
                };
            }
            else if (pipelineName == nameof(EBuildPipeline.ArchiveFileBuildPipeline))
                parameters = new ArchiveFileBuildParameters();
            else if (pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline))
                parameters = new RawFileBuildParameters();
            else
                throw new NotSupportedException("Unsupported YooAsset V3 pipeline: " + pipelineName);

            ConfigureV3Common(parameters, packageName, pipelineName, options, buildTarget, packageVersion);
            return parameters;
        }

        /// <summary>填充 YooAsset V3 各管线共享的构建参数和参数化加密器。</summary>
        private static void ConfigureV3Common(
            BuildParameters parameters,
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options,
            BuildTarget buildTarget,
            string packageVersion)
        {
            parameters.BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
            parameters.BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot();
            parameters.BuildPipeline = pipelineName;
            parameters.BuildBundleType = GetV3BundleType(pipelineName);
            parameters.BuildTarget = buildTarget;
            parameters.PackageName = packageName;
            parameters.PackageVersion = packageVersion;
            parameters.EnableSharePackRule = pipelineName != nameof(EBuildPipeline.RawFileBuildPipeline);
            parameters.VerifyBuildingResult = true;
            parameters.FileNameStyle = BundleBuilderSetting.GetPackageFileNameStyle(
                packageName, pipelineName);
            parameters.BundledCopyOption = (EBundledCopyOption)YooAssetBuildSettingsAdapter.GetCopyOption(
                packageName, pipelineName);
            parameters.BundledCopyParams = YooAssetBuildSettingsAdapter.GetCopyParams(
                packageName, pipelineName);
            parameters.ClearBuildCacheFiles = YooAssetBuildSettingsAdapter.GetClearBuildCache(
                packageName, pipelineName);
            parameters.UseAssetDependencyDB = YooAssetBuildSettingsAdapter.GetUseAssetDependencyDatabase(
                packageName, pipelineName);
            parameters.BundleEncryptor = YooAssetEncryptionServices.CreateBundleEncryptor(options);
        }

        /// <summary>把 YooAsset V3 管线名称映射为对应 bundle 类型。</summary>
        private static int GetV3BundleType(string pipelineName)
        {
            if (pipelineName == nameof(EBuildPipeline.ArchiveFileBuildPipeline))
                return (int)EBundleType.ArchiveBundle;
            if (pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline))
                return (int)EBundleType.RawBundle;
            return (int)EBundleType.AssetBundle;
        }

        /// <summary>执行与 YooAsset V3 参数类型匹配的构建管线。</summary>
        private static BuildResult RunV3Build(BuildParameters parameters)
        {
            if (parameters is ScriptableBuildParameters)
                return new ScriptableBuildPipeline().Run(parameters, true);
            if (parameters is ArchiveFileBuildParameters)
                return new ArchiveFileBuildPipeline().Run(parameters, true);
            if (parameters is RawFileBuildParameters)
                return new RawFileBuildPipeline().Run(parameters, true);
            throw new NotSupportedException("Unsupported YooAsset V3 build parameters.");
        }

        /// <summary>获取 YooAsset V3 默认着色器 bundle 名称。</summary>
        private static string GetV3BuiltinShadersBundleName(string packageName)
        {
            bool uniqueBundleName = BundleCollectorSettingData.Setting.UniqueBundleName;
            var result = DefaultBundlePackRule.CreateShadersPackRuleResult();
            return result.GetBundleName(packageName, uniqueBundleName);
        }
#else
        /// <summary>按 YooAsset V2 管线创建具体参数类型。</summary>
        private static BuildParameters CreateV2BuildParameters(
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options,
            BuildTarget buildTarget,
            string packageVersion)
        {
            BuildParameters parameters;
            if (pipelineName == nameof(EBuildPipeline.ScriptableBuildPipeline))
            {
                parameters = new ScriptableBuildParameters
                {
                    CompressOption = YooAssetBuildSettingsAdapter.GetCompressOption(
                        packageName, pipelineName),
                    BuiltinShadersBundleName = GetV2BuiltinShadersBundleName(packageName)
                };
            }
            else if (pipelineName == nameof(EBuildPipeline.BuiltinBuildPipeline))
            {
                parameters = new BuiltinBuildParameters
                {
                    CompressOption = YooAssetBuildSettingsAdapter.GetCompressOption(
                        packageName, pipelineName)
                };
            }
            else if (pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline))
                parameters = new RawFileBuildParameters();
            else
                throw new NotSupportedException("Unsupported YooAsset V2 pipeline: " + pipelineName);

            ConfigureV2Common(parameters, packageName, pipelineName, options, buildTarget, packageVersion);
            return parameters;
        }

        /// <summary>填充 YooAsset V2 各管线共享的构建参数和参数化加密服务。</summary>
        private static void ConfigureV2Common(
            BuildParameters parameters,
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options,
            BuildTarget buildTarget,
            string packageVersion)
        {
            parameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            parameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            parameters.BuildPipeline = pipelineName;
            parameters.BuildBundleType = pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline)
                ? (int)EBuildBundleType.RawBundle
                : (int)EBuildBundleType.AssetBundle;
            parameters.BuildTarget = buildTarget;
            parameters.PackageName = packageName;
            parameters.PackageVersion = packageVersion;
            parameters.EnableSharePackRule = pipelineName != nameof(EBuildPipeline.RawFileBuildPipeline);
            parameters.VerifyBuildingResult = true;
            parameters.FileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(
                packageName, pipelineName);
            parameters.BuildinFileCopyOption = (EBuildinFileCopyOption)YooAssetBuildSettingsAdapter.GetCopyOption(
                packageName, pipelineName);
            parameters.BuildinFileCopyParams = YooAssetBuildSettingsAdapter.GetCopyParams(
                packageName, pipelineName);
            parameters.ClearBuildCacheFiles = YooAssetBuildSettingsAdapter.GetClearBuildCache(
                packageName, pipelineName);
            parameters.UseAssetDependencyDB = YooAssetBuildSettingsAdapter.GetUseAssetDependencyDatabase(
                packageName, pipelineName);
            parameters.EncryptionServices = YooAssetEncryptionServices.CreateEncryptionServices(options);
        }

        /// <summary>执行与 YooAsset V2 参数类型匹配的构建管线。</summary>
        private static BuildResult RunV2Build(BuildParameters parameters)
        {
            if (parameters is ScriptableBuildParameters)
                return new ScriptableBuildPipeline().Run(parameters, true);
            if (parameters is BuiltinBuildParameters)
                return new BuiltinBuildPipeline().Run(parameters, true);
            if (parameters is RawFileBuildParameters)
                return new RawFileBuildPipeline().Run(parameters, true);
            throw new NotSupportedException("Unsupported YooAsset V2 build parameters.");
        }

        /// <summary>获取 YooAsset V2 默认着色器 bundle 名称。</summary>
        private static string GetV2BuiltinShadersBundleName(string packageName)
        {
            bool uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var result = DefaultPackRule.CreateShadersPackRuleResult();
            return result.GetBundleName(packageName, uniqueBundleName);
        }
#endif
    }
}
#endif
