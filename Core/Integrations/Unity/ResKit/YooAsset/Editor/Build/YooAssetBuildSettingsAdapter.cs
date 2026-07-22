#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAsset Editor 构建设置适配器。
    /// 该类型只负责 YooAsset.Editor API 映射，不包含任何 Inspector 样式逻辑。
    /// </summary>
    internal static class YooAssetBuildSettingsAdapter
    {
        /// <summary>获取 YooAsset 当前可用的构建管线名称。</summary>
        internal static IReadOnlyList<string> GetBuildPipelineNames()
        {
#if YOKIFRAME_YOOASSET_3
            return new[]
            {
                nameof(EBuildPipeline.ScriptableBuildPipeline),
                nameof(EBuildPipeline.ArchiveFileBuildPipeline),
                nameof(EBuildPipeline.RawFileBuildPipeline)
            };
#else
            return new[]
            {
                nameof(EBuildPipeline.ScriptableBuildPipeline),
                nameof(EBuildPipeline.BuiltinBuildPipeline),
                nameof(EBuildPipeline.RawFileBuildPipeline)
            };
#endif
        }

        /// <summary>读取资源收集器中已有的 package 名称，保持收集器声明顺序。</summary>
        internal static List<string> GetCollectorPackageNames()
        {
            List<string> names = new();
            try
            {
#if YOKIFRAME_YOOASSET_3
                foreach (var package in BundleCollectorSettingData.Setting.Packages)
#else
                foreach (var package in AssetBundleCollectorSettingData.Setting.Packages)
#endif
                    AddUnique(names, package.PackageName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("读取 YooAsset 收集器 package 失败：" + exception.Message);
            }

            if (names.Count == 0)
                names.Add(YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME);
            return names;
        }

        /// <summary>将收集器 package 快照同步到运行时初始化参数。</summary>
        internal static bool SynchronizePackageNames(SerializedProperty optionsProperty)
        {
            if (optionsProperty == null)
                return false;

            SerializedProperty packages = optionsProperty.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.PackageNames));
            if (packages == null)
                return false;

            List<string> names = GetCollectorPackageNames();
            if (PackageNamesMatch(packages, names))
                return false;

            packages.arraySize = names.Count;
            for (int index = 0; index < names.Count; index++)
                packages.GetArrayElementAtIndex(index).stringValue = names[index];
            optionsProperty.serializedObject.ApplyModifiedProperties();
            return true;
        }

        /// <summary>读取 package 当前选择的构建管线。</summary>
        internal static string GetBuildPipeline(string packageName)
        {
#if YOKIFRAME_YOOASSET_3
            return BundleBuilderSetting.GetPackageBuildPipeline(packageName);
#else
            return AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName);
#endif
        }

        /// <summary>写入 package 当前选择的构建管线。</summary>
        internal static void SetBuildPipeline(string packageName, string pipelineName)
        {
#if YOKIFRAME_YOOASSET_3
            BundleBuilderSetting.SetPackageBuildPipeline(packageName, pipelineName);
#else
            AssetBundleBuilderSetting.SetPackageBuildPipeline(packageName, pipelineName);
#endif
        }

        /// <summary>读取 package 当前压缩方式。</summary>
        internal static ECompressOption GetCompressOption(string packageName, string pipelineName)
        {
#if YOKIFRAME_YOOASSET_3
            return BundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
#endif
        }

        /// <summary>写入 package 当前压缩方式。</summary>
        internal static void SetCompressOption(string packageName, string pipelineName, ECompressOption value)
        {
#if YOKIFRAME_YOOASSET_3
            BundleBuilderSetting.SetPackageCompressOption(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageCompressOption(packageName, pipelineName, value);
#endif
        }

        /// <summary>读取首包拷贝策略。</summary>
        internal static int GetCopyOption(string packageName, string pipelineName)
        {
#if YOKIFRAME_YOOASSET_3
            return (int)BundleBuilderSetting.GetPackageBundledCopyOption(packageName, pipelineName);
#else
            return (int)AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(
                packageName,
                pipelineName);
#endif
        }

        /// <summary>写入首包拷贝策略。</summary>
        internal static void SetCopyOption(string packageName, string pipelineName, int value)
        {
#if YOKIFRAME_YOOASSET_3
            BundleBuilderSetting.SetPackageBundledCopyOption(
                packageName,
                pipelineName,
                (EBundledCopyOption)value);
#else
            AssetBundleBuilderSetting.SetPackageBuildinFileCopyOption(
                packageName,
                pipelineName,
                (EBuildinFileCopyOption)value);
#endif
        }

        /// <summary>读取首包拷贝标签参数。</summary>
        internal static string GetCopyParams(string packageName, string pipelineName)
        {
#if YOKIFRAME_YOOASSET_3
            return BundleBuilderSetting.GetPackageBundledCopyParams(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
#endif
        }

        /// <summary>写入首包拷贝标签参数。</summary>
        internal static void SetCopyParams(string packageName, string pipelineName, string value)
        {
#if YOKIFRAME_YOOASSET_3
            BundleBuilderSetting.SetPackageBundledCopyParams(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageBuildinFileCopyParams(packageName, pipelineName, value);
#endif
        }

        /// <summary>读取是否清空构建缓存。</summary>
        internal static bool GetClearBuildCache(string packageName, string pipelineName)
        {
#if YOKIFRAME_YOOASSET_3
            return BundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
#endif
        }

        /// <summary>写入是否清空构建缓存。</summary>
        internal static void SetClearBuildCache(string packageName, string pipelineName, bool value)
        {
#if YOKIFRAME_YOOASSET_3
            BundleBuilderSetting.SetPackageClearBuildCache(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageClearBuildCache(packageName, pipelineName, value);
#endif
        }

        /// <summary>读取是否使用资源依赖缓存。</summary>
        internal static bool GetUseAssetDependencyDatabase(string packageName, string pipelineName)
        {
#if YOKIFRAME_YOOASSET_3
            return BundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);
#else
            return AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);
#endif
        }

        /// <summary>写入是否使用资源依赖缓存。</summary>
        internal static void SetUseAssetDependencyDatabase(string packageName, string pipelineName, bool value)
        {
#if YOKIFRAME_YOOASSET_3
            BundleBuilderSetting.SetPackageUseAssetDependencyDB(packageName, pipelineName, value);
#else
            AssetBundleBuilderSetting.SetPackageUseAssetDependencyDB(packageName, pipelineName, value);
#endif
        }

        /// <summary>向列表添加非空且不重复的名称。</summary>
        private static void AddUnique(List<string> names, string value)
        {
            if (!string.IsNullOrEmpty(value) && !names.Contains(value))
                names.Add(value);
        }

        /// <summary>检查序列化 package 列表是否已与收集器快照一致。</summary>
        private static bool PackageNamesMatch(
            SerializedProperty packages,
            IReadOnlyList<string> names)
        {
            if (packages.arraySize != names.Count)
                return false;

            for (int index = 0; index < names.Count; index++)
            {
                if (!string.Equals(
                        packages.GetArrayElementAtIndex(index).stringValue,
                        names[index],
                        StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }
}
#endif
