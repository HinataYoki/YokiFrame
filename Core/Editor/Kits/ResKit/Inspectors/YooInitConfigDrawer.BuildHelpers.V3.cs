#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 3.x 构建辅助方法
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        /// <summary>3.x 构建管线选项列表</summary>
        private static readonly List<string> sBuildPipelineChoicesV3 = new()
        {
            nameof(EBuildPipeline.ScriptableBuildPipeline),
            nameof(EBuildPipeline.ArchiveFileBuildPipeline),
            nameof(EBuildPipeline.RawFileBuildPipeline)
        };

        #region 获取资源包名称

        /// <summary>
        /// 获取所有资源包名称
        /// </summary>
        private static List<string> GetBuildPackageNamesV3()
        {
            var result = new List<string>();
            try
            {
                foreach (var package in BundleCollectorSettingData.Setting.Packages)
                    result.Add(package.PackageName);
            }
            catch { }
            return result;
        }

        #endregion

        #region 获取加密服务类名

        /// <summary>
        /// 获取所有 IBundleEncryptor 实现类名（3.x 使用 IBundleEncryptor）
        /// </summary>
        private static List<string> GetEncryptionServiceClassNamesV3()
        {
            var result = new List<string> { typeof(EncryptionNone).FullName };
            try
            {
                var types = TypeCache.GetTypesDerivedFrom<IBundleEncryptor>();
                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!result.Contains(type.FullName))
                        result.Add(type.FullName);
                }
            }
            catch { }
            return result;
        }

        #endregion

        #region 执行构建

        /// <summary>
        /// 执行资源包构建（3.x API）
        /// </summary>
        private static void ExecuteBuildV3(string packageName, string pipelineName)
        {
            try
            {
                var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                var compressOption = BundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
                var copyOption = BundleBuilderSetting.GetPackageBundledCopyOption(packageName, pipelineName);
                var copyParams = BundleBuilderSetting.GetPackageBundledCopyParams(packageName, pipelineName);
                var encryptClassName = BundleBuilderSetting.GetPackageBundleEncryptorClassName(packageName, pipelineName);
                var clearCache = BundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
                var useDepDB = BundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

                IBundleEncryptor encryptor = CreateEncryptionServiceV3(encryptClassName);

                // 3.x: 手动构建默认路径（AssetBundleBuilderHelper 已移除）
                string projectRoot = Application.dataPath.Replace("/Assets", "");
                string buildOutputRoot = $"{projectRoot}/Bundles";
                string bundledFileRoot = Application.streamingAssetsPath;

                var buildResult = pipelineName switch
                {
                    nameof(EBuildPipeline.ScriptableBuildPipeline) => ExecuteScriptableBuildV3(
                        packageName, pipelineName, buildTarget, compressOption,
                        copyOption, copyParams, encryptor, clearCache, useDepDB,
                        buildOutputRoot, bundledFileRoot),
                    nameof(EBuildPipeline.ArchiveFileBuildPipeline) => ExecuteArchiveFileBuildV3(
                        packageName, pipelineName, buildTarget, compressOption,
                        copyOption, copyParams, encryptor, clearCache, useDepDB,
                        buildOutputRoot, bundledFileRoot),
                    nameof(EBuildPipeline.RawFileBuildPipeline) => ExecuteRawFileBuildV3(
                        packageName, pipelineName, buildTarget,
                        copyOption, copyParams, encryptor, clearCache, useDepDB,
                        buildOutputRoot, bundledFileRoot),
                    _ => (false, $"不支持的构建管线: {pipelineName}", "")
                };

                ShowBuildResultV3(buildResult.Item1, buildResult.Item2, buildResult.Item3);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("构建异常", $"构建过程中发生异常:\n{e.Message}", "确定");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// 创建加密服务实例（3.x 使用 IBundleEncryptor）
        /// </summary>
        private static IBundleEncryptor CreateEncryptionServiceV3(string encryptClassName)
        {
            if (!string.IsNullOrEmpty(encryptClassName))
            {
                var encryptType = Type.GetType(encryptClassName);
                if (encryptType != null)
                    return Activator.CreateInstance(encryptType) as IBundleEncryptor ?? new EncryptionNone();
            }
            return new EncryptionNone();
        }

        /// <summary>
        /// 执行 ScriptableBuildPipeline 构建（3.x）
        /// </summary>
        private static (bool success, string error, string outputDir) ExecuteScriptableBuildV3(
            string packageName, string pipelineName, BuildTarget buildTarget,
            ECompressOption compressOption, EBundledCopyOption copyOption, string copyParams,
            IBundleEncryptor encryptor, bool clearCache, bool useDepDB,
            string buildOutputRoot, string bundledFileRoot)
        {
            var buildParameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = buildOutputRoot,
                BundledFileRoot = bundledFileRoot,
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = buildTarget,
                PackageName = packageName,
                PackageVersion = GetBuildPackageVersionV3(),
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = copyOption,
                BundledCopyParams = copyParams,
                BundleEncryptor = encryptor,
                ClearBuildCacheFiles = clearCache,
                UseAssetDependencyDB = useDepDB,
                CompressOption = compressOption
            };

            var pipeline = new ScriptableBuildPipeline();
            var buildResult = pipeline.Run(buildParameters, true);
            return (buildResult.Success, buildResult.ErrorInfo, buildParameters.GetPackageOutputDirectory());
        }

        /// <summary>
        /// 执行 ArchiveFileBuildPipeline 构建（3.x，替代 2.x BuiltinBuildPipeline）
        /// </summary>
        private static (bool success, string error, string outputDir) ExecuteArchiveFileBuildV3(
            string packageName, string pipelineName, BuildTarget buildTarget,
            ECompressOption compressOption, EBundledCopyOption copyOption, string copyParams,
            IBundleEncryptor encryptor, bool clearCache, bool useDepDB,
            string buildOutputRoot, string bundledFileRoot)
        {
            var buildParameters = new ArchiveFileBuildParameters
            {
                BuildOutputRoot = buildOutputRoot,
                BundledFileRoot = bundledFileRoot,
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = buildTarget,
                PackageName = packageName,
                PackageVersion = GetBuildPackageVersionV3(),
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = copyOption,
                BundledCopyParams = copyParams,
                BundleEncryptor = encryptor,
                ClearBuildCacheFiles = clearCache,
                UseAssetDependencyDB = useDepDB
            };

            var pipeline = new ArchiveFileBuildPipeline();
            var buildResult = pipeline.Run(buildParameters, true);
            return (buildResult.Success, buildResult.ErrorInfo, buildParameters.GetPackageOutputDirectory());
        }

        /// <summary>
        /// 执行 RawFileBuildPipeline 构建（3.x）
        /// </summary>
        private static (bool success, string error, string outputDir) ExecuteRawFileBuildV3(
            string packageName, string pipelineName, BuildTarget buildTarget,
            EBundledCopyOption copyOption, string copyParams,
            IBundleEncryptor encryptor, bool clearCache, bool useDepDB,
            string buildOutputRoot, string bundledFileRoot)
        {
            var buildParameters = new RawFileBuildParameters
            {
                BuildOutputRoot = buildOutputRoot,
                BundledFileRoot = bundledFileRoot,
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBundleType.RawBundle,
                BuildTarget = buildTarget,
                PackageName = packageName,
                PackageVersion = GetBuildPackageVersionV3(),
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = copyOption,
                BundledCopyParams = copyParams,
                BundleEncryptor = encryptor,
                ClearBuildCacheFiles = clearCache,
                UseAssetDependencyDB = useDepDB
            };

            var pipeline = new RawFileBuildPipeline();
            var buildResult = pipeline.Run(buildParameters, true);
            return (buildResult.Success, buildResult.ErrorInfo, buildParameters.GetPackageOutputDirectory());
        }

        /// <summary>
        /// 显示构建结果
        /// </summary>
        private static void ShowBuildResultV3(bool success, string errorInfo, string outputDir)
        {
            if (success)
                EditorUtility.DisplayDialog("构建成功", $"资源包构建完成！\n\n输出目录: {outputDir}", "确定");
            else
                EditorUtility.DisplayDialog("构建失败", $"构建失败: {errorInfo}", "确定");
        }

        /// <summary>
        /// 获取构建版本号
        /// </summary>
        private static string GetBuildPackageVersionV3() => DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

        #endregion
    }
}
#endif
