#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooInitConfigDrawer
    {
        private static List<string> CreateBuildPipelineChoices()
        {
#if YOOASSET_3_0_OR_NEWER
            return new()
            {
                nameof(EBuildPipeline.ScriptableBuildPipeline),
                nameof(EBuildPipeline.ArchiveFileBuildPipeline),
                nameof(EBuildPipeline.RawFileBuildPipeline)
            };
#else
            return new()
            {
                nameof(EBuildPipeline.ScriptableBuildPipeline),
                nameof(EBuildPipeline.BuiltinBuildPipeline),
                nameof(EBuildPipeline.RawFileBuildPipeline)
            };
#endif
        }

        private static List<string> GetBuildPackageNames(SerializedProperty property)
        {
            List<string> packages = new();
#if YOOASSET_3_0_OR_NEWER
            try
            {
                foreach (var package in BundleCollectorSettingData.Setting.Packages)
                    AddUnique(packages, package.PackageName);
            }
            catch
            {
            }
#else
            try
            {
                foreach (var package in AssetBundleCollectorSettingData.Setting.Packages)
                    AddUnique(packages, package.PackageName);
            }
            catch
            {
            }
#endif

            var configPackages = property.FindPropertyRelative(nameof(YooInitConfig.PackageNames));
            if (configPackages != null)
            {
                for (var i = 0; i < configPackages.arraySize; i++)
                    AddUnique(packages, configPackages.GetArrayElementAtIndex(i).stringValue);
            }

            if (packages.Count == 0)
                packages.Add(YooInitConfig.DEFAULT_PACKAGE_NAME);

            return packages;
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (string.IsNullOrEmpty(value) || list.Contains(value))
                return;

            list.Add(value);
        }

        private static string GetBuildPipeline(string packageName)
        {
#if YOOASSET_3_0_OR_NEWER
            return BundleBuilderSetting.GetPackageBuildPipeline(packageName);
#else
            return AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName);
#endif
        }

        private static void SetBuildPipeline(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageBuildPipeline(packageName, pipelineName);
#else
            AssetBundleBuilderSetting.SetPackageBuildPipeline(packageName, pipelineName);
#endif
        }

        private static ECompressOption GetCompressOption(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            return BundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
#endif
        }

        private static void SetCompressOption(string packageName, string pipelineName, ECompressOption option)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageCompressOption(packageName, pipelineName, option);
#else
            AssetBundleBuilderSetting.SetPackageCompressOption(packageName, pipelineName, option);
#endif
        }

        private static int GetCopyOptionIndex(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            return (int)BundleBuilderSetting.GetPackageBundledCopyOption(packageName, pipelineName);
#else
            return (int)AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
#endif
        }

        private static void SetCopyOption(string packageName, string pipelineName, int index)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageBundledCopyOption(packageName, pipelineName, (EBundledCopyOption)index);
#else
            AssetBundleBuilderSetting.SetPackageBuildinFileCopyOption(packageName, pipelineName, (EBuildinFileCopyOption)index);
#endif
        }

        private static string GetCopyParams(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            return BundleBuilderSetting.GetPackageBundledCopyParams(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
#endif
        }

        private static void SetCopyParams(string packageName, string pipelineName, string value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageBundledCopyParams(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageBuildinFileCopyParams(packageName, pipelineName, value);
#endif
        }

        private static bool GetClearBuildCache(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            return BundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
#endif
        }

        private static void SetClearBuildCache(string packageName, string pipelineName, bool value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageClearBuildCache(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageClearBuildCache(packageName, pipelineName, value);
#endif
        }

        private static bool GetUseAssetDependencyDB(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            return BundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);
#endif
        }

        private static void SetUseAssetDependencyDB(string packageName, string pipelineName, bool value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageUseAssetDependencyDB(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageUseAssetDependencyDB(packageName, pipelineName, value);
#endif
        }

        private static string GetEncryptionServiceClassName(string packageName, string pipelineName)
        {
#if YOOASSET_3_0_OR_NEWER
            return BundleBuilderSetting.GetPackageBundleEncryptorClassName(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipelineName);
#endif
        }

        private static void SetEncryptionServiceClassName(string packageName, string pipelineName, string value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleBuilderSetting.SetPackageBundleEncryptorClassName(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageEncyptionServicesClassName(packageName, pipelineName, value);
#endif
        }

        private static List<string> GetEncryptionServiceClassNames()
        {
#if YOOASSET_3_0_OR_NEWER
            List<string> result = new() { typeof(EncryptionNone).FullName };
            try
            {
                var types = TypeCache.GetTypesDerivedFrom<IBundleEncryptor>();
                foreach (var type in types)
                {
                    if (!type.IsAbstract && !type.IsInterface)
                        AddUnique(result, type.FullName);
                }
            }
            catch
            {
            }
            return result;
#else
            List<string> result = new() { typeof(EncryptionNone).FullName };
            try
            {
                var types = TypeCache.GetTypesDerivedFrom<IEncryptionServices>();
                foreach (var type in types)
                {
                    if (!type.IsAbstract && !type.IsInterface)
                        AddUnique(result, type.FullName);
                }
            }
            catch
            {
            }
            return result;
#endif
        }
    }
}
#endif
#endif
