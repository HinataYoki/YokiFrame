#if YOKIFRAME_YOOASSET_SUPPORT
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooInit 包访问功能
    /// </summary>
    public static partial class YooInit
    {
        #region 包访问

        /// <summary>
        /// 获取指定名称的资源包
        /// </summary>
        public static ResourcePackage GetPackage(string packageName)
            => sPackages.TryGetValue(packageName, out var package) ? package : default;

        /// <summary>
        /// 尝试获取指定名称的资源包
        /// </summary>
        public static bool TryGetPackage(string packageName, out ResourcePackage package)
            => sPackages.TryGetValue(packageName, out package);

        /// <summary>
        /// 智能查找包含指定路径的资源包
        /// 遍历所有包，返回第一个包含该路径的包
        /// </summary>
        public static ResourcePackage FindPackageForPath(string path)
        {
            foreach (var package in sPackages.Values)
            {
                if (package.CheckLocationValid(path))
                    return package;
            }
            return DefaultPackage;
        }

        /// <summary>
        /// 尝试智能查找包含指定路径的资源包
        /// </summary>
        public static bool TryFindPackageForPath(string path, out ResourcePackage package)
        {
            foreach (var pkg in sPackages.Values)
            {
                if (pkg.CheckLocationValid(path))
                {
                    package = pkg;
                    return true;
                }
            }
            package = default;
            return false;
        }

        /// <summary>
        /// 检查路径有效性（遍历所有包）
        /// </summary>
        public static bool CheckPathValid(string path, ResourcePackage package = null)
        {
            if (package != default)
                return package.CheckLocationValid(path);

            foreach (var pkg in sPackages.Values)
            {
                if (pkg.CheckLocationValid(path))
                    return true;
            }
            return false;
        }

        #endregion
    }
}
#endif
