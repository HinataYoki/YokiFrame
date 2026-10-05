#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 按登记顺序保存已就绪的 YooAsset package。
    /// 自动探测必须沿这份顺序查找，不能使用字典枚举顺序。
    /// </summary>
    internal sealed class YooAssetPackageRegistry
    {
        private readonly List<ResourcePackage> mPackages = new();

        /// <summary>获取当前已登记且仍可用的 package 数量。</summary>
        public int Count
        {
            get
            {
                PruneUnavailable();
                return mPackages.Count;
            }
        }

        /// <summary>
        /// 追加一个已就绪 package；同名已存在时替换实例并保持原位置。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        public void Add(ResourcePackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            PruneUnavailable();
            int index = IndexOf(package.PackageName);
            if (index >= 0)
            {
                mPackages[index] = package;
                return;
            }

            mPackages.Add(package);
        }

        /// <summary>按登记顺序复制当前仍可用的 package。</summary>
        /// <returns>调用方可安全遍历的 package 数组。</returns>
        public ResourcePackage[] Copy()
        {
            PruneUnavailable();
            return mPackages.ToArray();
        }

        /// <summary>按名称查找仍可用的 package。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="package">找到的 package。</param>
        /// <returns>找到时返回 true。</returns>
        public bool TryGet(string packageName, out ResourcePackage package)
        {
            PruneUnavailable();
            int index = IndexOf(packageName);
            if (index < 0)
            {
                package = null;
                return false;
            }

            package = mPackages[index];
            return true;
        }

        /// <summary>按名称移除登记。不销毁 package，只停止后续探测。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <returns>登记中存在并已移除时返回 true。</returns>
        public bool Remove(string packageName)
        {
            PruneUnavailable();
            int index = IndexOf(packageName);
            if (index < 0)
                return false;

            mPackages.RemoveAt(index);
            return true;
        }

        /// <summary>清空登记，供会话重置使用。</summary>
        public void Clear()
        {
            mPackages.Clear();
        }

        /// <summary>按 Ordinal 名称查找登记下标。</summary>
        private int IndexOf(string packageName)
        {
            for (int index = 0; index < mPackages.Count; index++)
            {
                if (string.Equals(mPackages[index].PackageName, packageName, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }

        /// <summary>移除已被项目销毁、无法再参与探测的 package。</summary>
        private void PruneUnavailable()
        {
            for (int index = mPackages.Count - 1; index >= 0; index--)
            {
                if (!YooAssetPackageReadiness.IsReady(mPackages[index]))
                    mPackages.RemoveAt(index);
            }
        }
    }
}
#endif
