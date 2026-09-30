#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("YokiFrame.Unity.ResKit.YooAsset.Tests")]

namespace YokiFrame.Unity
{
    /// <summary>
    /// 解析 ResKit 路径中的显式 YooAsset package 前缀。
    /// 语法固定为 <c>package:{包名}/{location}</c>；没有此前缀时保持自动探测。
    /// </summary>
    internal static class YooAssetLocation
    {
        private const string PACKAGE_PREFIX = "package:";

        /// <summary>把调用方路径拆成可选包名和 YooAsset location。</summary>
        /// <param name="path">ResKit 收到的原始路径。</param>
        /// <param name="packageName">显式包名；未指定时为空。</param>
        /// <param name="location">交给 YooAsset 的 location。</param>
        public static void Split(string path, out string packageName, out string location)
        {
            packageName = null;
            location = path;
            if (string.IsNullOrEmpty(path)
                || path.Length <= PACKAGE_PREFIX.Length
                || !path.StartsWith(PACKAGE_PREFIX, StringComparison.Ordinal))
            {
                return;
            }

            int separator = path.IndexOf('/', PACKAGE_PREFIX.Length);
            if (separator <= PACKAGE_PREFIX.Length || separator >= path.Length - 1)
            {
                throw new ArgumentException(
                    "Explicit YooAsset path must use package:{packageName}/{location}.",
                    nameof(path));
            }

            packageName = path.Substring(PACKAGE_PREFIX.Length, separator - PACKAGE_PREFIX.Length);
            location = path.Substring(separator + 1);
            if (packageName.IndexOf('/') >= 0 || string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException(
                    "Explicit YooAsset path must use package:{packageName}/{location}.",
                    nameof(path));
            }
        }
    }
}
#endif
