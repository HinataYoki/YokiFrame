using System;

namespace YokiFrame
{
    /// <summary>
    /// 资源缓存键，由资源类型和资源路径共同确定。
    /// </summary>
    public readonly struct ResCacheKey : IEquatable<ResCacheKey>
    {
        /// <summary>
        /// 资源对象类型。
        /// </summary>
        public readonly Type AssetType;

        /// <summary>
        /// 资源路径。
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// 创建资源缓存键。
        /// </summary>
        /// <param name="assetType">资源对象类型。</param>
        /// <param name="path">资源路径。</param>
        public ResCacheKey(Type assetType, string path)
        {
            AssetType = assetType;
            Path = path;
        }

        /// <summary>
        /// 判断当前缓存键是否与另一个缓存键相等。
        /// </summary>
        /// <param name="other">用于比较的另一个缓存键。</param>
        /// <returns>两者资源类型和路径都相同时返回 true。</returns>
        public bool Equals(ResCacheKey other) => AssetType == other.AssetType && Path == other.Path;

        /// <summary>
        /// 判断当前缓存键是否与指定对象相等。
        /// </summary>
        /// <param name="obj">用于比较的对象。</param>
        /// <returns>对象为同值缓存键时返回 true。</returns>
        public override bool Equals(object obj) => obj is ResCacheKey other && Equals(other);

        /// <summary>
        /// 获取当前缓存键的哈希码。
        /// </summary>
        /// <returns>由资源类型和路径组合得到的哈希码。</returns>
        public override int GetHashCode() => HashCode.Combine(AssetType, Path);
    }
}
