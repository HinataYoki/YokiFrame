#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3

using System;
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetResourceProvider
    {
        /// <summary>复制并校验探测清单，拒绝空清单和未就绪 package。</summary>
        private static List<ResourcePackage> CopyReadyPackages(IReadOnlyList<ResourcePackage> packages)
        {
            if (packages == null || packages.Count == 0)
                throw new ArgumentException("At least one YooAsset package is required.", nameof(packages));

            var ready = new List<ResourcePackage>(packages.Count);
            for (int index = 0; index < packages.Count; index++)
                TryAppendReadyPackage(ready, packages[index]);

            if (ready.Count == 0)
                throw new ArgumentException("At least one YooAsset package is required.", nameof(packages));
            return ready;
        }

        /// <summary>把一个已就绪且未重复的 package 追加到探测清单末尾。</summary>
        private static void TryAppendReadyPackage(List<ResourcePackage> packages, ResourcePackage package)
        {
            if (package == null || ContainsPackage(packages, package.PackageName))
                return;
            EnsurePackageReady(package);
            packages.Add(package);
        }

        /// <summary>拒绝尚未激活 manifest 的 package，避免探测阶段才暴露初始化失败。</summary>
        private static void EnsurePackageReady(ResourcePackage package)
        {
            if (YooAssetPackageReadiness.IsReady(package))
                return;

            throw new InvalidOperationException(
                "YooAsset ResourcePackage must be initialized successfully before installing the ResKit provider.");
        }

        /// <summary>判断探测清单是否已经包含同名 package，避免重复探测。</summary>
        private static bool ContainsPackage(List<ResourcePackage> packages, string packageName)
        {
            return IndexOfPackage(packages, packageName) >= 0;
        }

        /// <summary>按 Ordinal 名称查找探测清单下标，未找到时返回 -1。</summary>
        private static int IndexOfPackage(List<ResourcePackage> packages, string packageName)
        {
            for (int index = 0; index < packages.Count; index++)
            {
                if (string.Equals(packages[index].PackageName, packageName, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// 追加一个已就绪 package。同名 package 原位替换，新包追加到探测顺序末尾。
        /// 不替换 ResKit Provider，也不销毁被替换的旧实例。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        public void AddPackage(ResourcePackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            EnsurePackageReady(package);
            lock (mLock)
            {
                int index = IndexOfPackage(mPackages, package.PackageName);
                if (index >= 0)
                    mPackages[index] = package;
                else
                    mPackages.Add(package);
            }
        }

        /// <summary>
        /// 从探测名单移除 package。不销毁 YooAsset package，也不释放已经交给 ResKit 的资源。
        /// </summary>
        /// <param name="packageName">package 名称。</param>
        /// <returns>名单中存在并已移除时返回 true。</returns>
        public bool RemovePackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return false;

            lock (mLock)
            {
                int index = IndexOfPackage(mPackages, packageName.Trim());
                if (index < 0)
                    return false;

                mPackages.RemoveAt(index);
                return true;
            }
        }

        /// <summary>按登记顺序复制当前探测名单，调用方可在锁外遍历。</summary>
        /// <returns>当前仍登记的 package。</returns>
        public ResourcePackage[] CopyPackages()
        {
            lock (mLock)
            {
                return mPackages.ToArray();
            }
        }

        /// <summary>
        /// 按显式包名或登记顺序解析本次加载使用的 package。
        /// 显式包未登记时立即失败，不回退到自动探测。
        /// </summary>
        private ResourcePackage ResolvePackage(string path, out string location)
        {
            YooAssetLocation.Split(path, out string packageName, out location);
            ResourcePackage[] packages = CopyPackages();
            if (!string.IsNullOrEmpty(packageName))
                return RequirePackage(packages, packageName);

            for (int index = 0; index < packages.Length; index++)
            {
                ResourcePackage candidate = packages[index];
                if (YooAssetPackageReadiness.IsReady(candidate) && IsLocationValid(candidate, location))
                    return candidate;
            }

            if (packages.Length == 0)
            {
                throw new InvalidOperationException(
                    "YooAsset ResourcePackage must be initialized successfully before installing the ResKit provider.");
            }

            return packages[0];
        }

        /// <summary>按当前 YooAsset 主版本调用位置校验 API，隔离 V2 与 V3 的命名差异。</summary>
        private static bool IsLocationValid(ResourcePackage package, string location)
        {
#if YOKIFRAME_YOOASSET_3
            return package.IsLocationValid(location);
#else
            return package.CheckLocationValid(location);
#endif
        }

        /// <summary>在本次加载的探测快照中查找显式 package，缺失时给出稳定错误。</summary>
        private static ResourcePackage RequirePackage(ResourcePackage[] packages, string packageName)
        {
            for (int index = 0; index < packages.Length; index++)
            {
                ResourcePackage package = packages[index];
                if (string.Equals(package.PackageName, packageName, StringComparison.Ordinal))
                    return package;
            }

            throw new InvalidOperationException(
                "YooAsset package '" + packageName + "' is not registered with the ResKit provider.");
        }
    }
}

#endif
