#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3

using System;
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetResourceProvider
    {
        /// <summary>复制并校验探测清单，拒绝空清单和未就绪 package。</summary>
        private static ResourcePackage[] CopyReadyPackages(IReadOnlyList<ResourcePackage> packages)
        {
            if (packages == null || packages.Count == 0)
                throw new ArgumentException("At least one YooAsset package is required.", nameof(packages));

            var ready = new List<ResourcePackage>(packages.Count);
            for (int index = 0; index < packages.Count; index++)
            {
                ResourcePackage package = packages[index];
                if (package == null || ContainsPackage(ready, package.PackageName))
                    continue;
                if (!YooAssetPackageReadiness.IsReady(package))
                {
                    throw new InvalidOperationException(
                        "YooAsset ResourcePackage must be initialized successfully before installing the ResKit provider.");
                }

                ready.Add(package);
            }

            if (ready.Count == 0)
                throw new ArgumentException("At least one YooAsset package is required.", nameof(packages));
            return ready.ToArray();
        }

        /// <summary>判断探测清单是否已经包含同名 package，避免重复探测。</summary>
        private static bool ContainsPackage(List<ResourcePackage> packages, string packageName)
        {
            for (int index = 0; index < packages.Count; index++)
            {
                if (string.Equals(packages[index].PackageName, packageName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 按显式包名或登记顺序解析本次加载使用的 package。
        /// 显式包未登记时立即失败，不回退到自动探测。
        /// </summary>
        private ResourcePackage ResolvePackage(string path, out string location)
        {
            YooAssetLocation.Split(path, out string packageName, out location);
            if (!string.IsNullOrEmpty(packageName))
                return RequirePackage(packageName);

            for (int index = 0; index < mPackages.Length; index++)
            {
                ResourcePackage candidate = mPackages[index];
                if (YooAssetPackageReadiness.IsReady(candidate) && candidate.IsLocationValid(location))
                    return candidate;
            }

            return mPackages[0];
        }

        /// <summary>在构造时固定的探测清单中查找显式 package，缺失时给出稳定错误。</summary>
        private ResourcePackage RequirePackage(string packageName)
        {
            ResourcePackage selected = FindPackage(packageName);
            if (selected != null)
                return selected;

            throw new InvalidOperationException(
                "YooAsset package '" + packageName + "' is not registered with the ResKit provider.");
        }

        /// <summary>在构造时固定的探测清单中按 Ordinal 名称查找 package。</summary>
        private ResourcePackage FindPackage(string packageName)
        {
            for (int index = 0; index < mPackages.Length; index++)
            {
                ResourcePackage package = mPackages[index];
                if (string.Equals(package.PackageName, packageName, StringComparison.Ordinal))
                    return package;
            }

            return null;
        }
    }
}

#endif
