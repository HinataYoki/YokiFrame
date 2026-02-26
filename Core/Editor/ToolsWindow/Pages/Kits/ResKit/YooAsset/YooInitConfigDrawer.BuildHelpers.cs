#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 构建辅助方法（UIToolkit 和 IMGUI 共享）
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 打开收集器

        /// <summary>
        /// 打开 YokiFrame 工具窗口并切换到 YooAsset 资源收集标签页
        /// </summary>
        private static void OpenYooAssetCollector()
        {
            YokiToolsWindow.OpenAndSelectPage<ResKitToolPage>(page =>
            {
                // 切换到 YooAsset 收集器标签页
                page.SwitchToYooAssetCollector();
            });
        }

        #endregion

        #region 构建辅助方法

        /// <summary>
        /// 获取所有资源包名称
        /// </summary>
        private static List<string> GetBuildPackageNames()
        {
            var result = new List<string>();
            try
            {
                foreach (var package in AssetBundleCollectorSettingData.Setting.Packages)
                    result.Add(package.PackageName);
            }
            catch { }
            return result;
        }

        /// <summary>
        /// 获取所有加密服务类名
        /// </summary>
        private static List<string> GetEncryptionServiceClassNames()
        {
            var result = new List<string> { typeof(EncryptionNone).FullName };
            try
            {
                var types = TypeCache.GetTypesDerivedFrom<IEncryptionServices>();
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

        /// <summary>
        /// 执行资源包构建
        /// </summary>
        private static void ExecuteBuild(string packageName, string pipelineName)
        {
            try
            {
                var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
                var copyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
                var copyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
                var encryptClassName = AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipelineName);
                var clearCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
                var useDepDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

                IEncryptionServices encryptionServices = CreateEncryptionService(encryptClassName);

                string buildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
                string buildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();

                var buildResult = pipelineName switch
                {
                    nameof(EBuildPipeline.ScriptableBuildPipeline) => ExecuteScriptableBuild(packageName, pipelineName, buildTarget, compressOption, copyOption, copyParams, encryptionServices, clearCache, useDepDB, buildOutputRoot, buildinFileRoot),
                    nameof(EBuildPipeline.BuiltinBuildPipeline) => ExecuteBuiltinBuild(packageName, pipelineName, buildTarget, compressOption, copyOption, copyParams, encryptionServices, clearCache, useDepDB, buildOutputRoot, buildinFileRoot),
                    nameof(EBuildPipeline.RawFileBuildPipeline) => ExecuteRawFileBuild(packageName, pipelineName, buildTarget, copyOption, copyParams, encryptionServices, clearCache, useDepDB, buildOutputRoot, buildinFileRoot),
                    _ => (false, $"不支持的构建管线: {pipelineName}", "")
                };

                ShowBuildResult(buildResult.Item1, buildResult.Item2, buildResult.Item3);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("构建异常", $"构建过程中发生异常:\n{e.Message}", "确定");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// 创建加密服务实例
        /// </summary>
        private static IEncryptionServices CreateEncryptionService(string encryptClassName)
        {
            if (!string.IsNullOrEmpty(encryptClassName))
            {
                var encryptType = Type.GetType(encryptClassName);
                if (encryptType != null)
                    return Activator.CreateInstance(encryptType) as IEncryptionServices ?? new EncryptionNone();
            }
            return new EncryptionNone();
        }

        /// <summary>
        /// 执行 ScriptableBuildPipeline 构建
        /// </summary>
        private static (bool success, string error, string outputDir) ExecuteScriptableBuild(
            string packageName, string pipelineName, BuildTarget buildTarget,
            ECompressOption compressOption, EBuildinFileCopyOption copyOption, string copyParams,
            IEncryptionServices encryptionServices, bool clearCache, bool useDepDB,
            string buildOutputRoot, string buildinFileRoot)
        {
            var buildParameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = buildOutputRoot,
                BuildinFileRoot = buildinFileRoot,
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                BuildTarget = buildTarget,
                PackageName = packageName,
                PackageVersion = GetBuildPackageVersion(),
                CompressOption = compressOption,
                FileNameStyle = EFileNameStyle.HashName,
                BuildinFileCopyOption = copyOption,
                BuildinFileCopyParams = copyParams,
                EncryptionServices = encryptionServices,
                ClearBuildCacheFiles = clearCache,
                UseAssetDependencyDB = useDepDB
            };

            var pipeline = new ScriptableBuildPipeline();
            var buildResult = pipeline.Run(buildParameters, true);
            return (buildResult.Success, buildResult.ErrorInfo, buildParameters.GetPackageOutputDirectory());
        }

        /// <summary>
        /// 执行 BuiltinBuildPipeline 构建
        /// </summary>
        private static (bool success, string error, string outputDir) ExecuteBuiltinBuild(
            string packageName, string pipelineName, BuildTarget buildTarget,
            ECompressOption compressOption, EBuildinFileCopyOption copyOption, string copyParams,
            IEncryptionServices encryptionServices, bool clearCache, bool useDepDB,
            string buildOutputRoot, string buildinFileRoot)
        {
            var buildParameters = new BuiltinBuildParameters
            {
                BuildOutputRoot = buildOutputRoot,
                BuildinFileRoot = buildinFileRoot,
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                BuildTarget = buildTarget,
                PackageName = packageName,
                PackageVersion = GetBuildPackageVersion(),
                CompressOption = compressOption,
                FileNameStyle = EFileNameStyle.HashName,
                BuildinFileCopyOption = copyOption,
                BuildinFileCopyParams = copyParams,
                EncryptionServices = encryptionServices,
                ClearBuildCacheFiles = clearCache,
                UseAssetDependencyDB = useDepDB
            };

            var pipeline = new BuiltinBuildPipeline();
            var buildResult = pipeline.Run(buildParameters, true);
            return (buildResult.Success, buildResult.ErrorInfo, buildParameters.GetPackageOutputDirectory());
        }

        /// <summary>
        /// 执行 RawFileBuildPipeline 构建
        /// </summary>
        private static (bool success, string error, string outputDir) ExecuteRawFileBuild(
            string packageName, string pipelineName, BuildTarget buildTarget,
            EBuildinFileCopyOption copyOption, string copyParams,
            IEncryptionServices encryptionServices, bool clearCache, bool useDepDB,
            string buildOutputRoot, string buildinFileRoot)
        {
            var buildParameters = new RawFileBuildParameters
            {
                BuildOutputRoot = buildOutputRoot,
                BuildinFileRoot = buildinFileRoot,
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.RawBundle,
                BuildTarget = buildTarget,
                PackageName = packageName,
                PackageVersion = GetBuildPackageVersion(),
                FileNameStyle = EFileNameStyle.HashName,
                BuildinFileCopyOption = copyOption,
                BuildinFileCopyParams = copyParams,
                EncryptionServices = encryptionServices,
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
        private static void ShowBuildResult(bool success, string errorInfo, string outputDir)
        {
            if (success)
                EditorUtility.DisplayDialog("构建成功", $"资源包构建完成！\n\n输出目录: {outputDir}", "确定");
            else
                EditorUtility.DisplayDialog("构建失败", $"构建失败: {errorInfo}", "确定");
        }

        /// <summary>
        /// 获取构建版本号
        /// </summary>
        private static string GetBuildPackageVersion() => DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

        #endregion
    }
}
#endif
