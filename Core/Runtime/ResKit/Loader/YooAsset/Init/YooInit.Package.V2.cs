#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooInit 包访问功能 — 2.x 版本
    /// 2.x 无 ResourcePackage 概念，所有操作通过 YooAssets 静态 API
    /// </summary>
    public static partial class YooInit
    {
        #region 包访问（2.x 兼容）

        /// <summary>
        /// 获取包名称（2.x 无 ResourcePackage 类型，返回包名引用）
        /// </summary>
        public static string GetPackage(string packageName)
            => sPackageNames.Contains(packageName) ? packageName : null;

        /// <summary>
        /// 尝试获取包名称
        /// </summary>
        public static bool TryGetPackage(string packageName, out string result)
        {
            if (sPackageNames.Contains(packageName))
            {
                result = packageName;
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// 查找包含指定路径的包（2.x 仅返回默认包名）
        /// </summary>
        public static string FindPackageForPath(string path)
        {
            // 2.x 无 CheckLocationValid，直接返回默认包名
            return DefaultPackageName;
        }

        /// <summary>
        /// 尝试查找包含指定路径的包
        /// </summary>
        public static bool TryFindPackageForPath(string path, out string packageName)
        {
            packageName = DefaultPackageName;
            return !string.IsNullOrEmpty(packageName);
        }

        /// <summary>
        /// 检查路径有效性（2.x 无 CheckLocationValid，默认返回 true）
        /// </summary>
        public static bool CheckPathValid(string path)
            => !string.IsNullOrEmpty(path);

        #endregion
    }
}
#endif
