using System;

namespace YokiFrame
{
    /// <summary>由完整资源类型和 Provider 路径组成的稳定缓存键。</summary>
    public readonly struct ResCacheKey : IEquatable<ResCacheKey>
    {
        /// <summary>创建资源缓存键。</summary>
        public ResCacheKey(Type assetType, string path)
        {
            AssetType = assetType ?? throw new ArgumentNullException(nameof(assetType));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary>获取资源对象类型。</summary>
        public Type AssetType { get; }

        /// <summary>获取 Provider 使用的原始路径。</summary>
        public string Path { get; }

        /// <summary>按完整类型和 Ordinal 路径判断两个 key 是否相同。</summary>
        public bool Equals(ResCacheKey other)
        {
            return AssetType == other.AssetType
                && string.Equals(Path, other.Path, StringComparison.Ordinal);
        }

        /// <summary>判断指定对象是否为同值缓存键。</summary>
        public override bool Equals(object obj) => obj is ResCacheKey other && Equals(other);

        /// <summary>生成与相等规则一致的稳定进程内哈希码。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int typeHash = AssetType != null ? AssetType.GetHashCode() : 0;
                int pathHash = Path != null ? StringComparer.Ordinal.GetHashCode(Path) : 0;
                return (typeHash * 397) ^ pathHash;
            }
        }

        /// <summary>判断两个资源缓存键是否相等。</summary>
        /// <param name="left">左侧操作数。</param>
        /// <param name="right">右侧操作数。</param>
        /// <returns>两个键相等时返回 true，否则返回 false。</returns>
        public static bool operator ==(ResCacheKey left, ResCacheKey right) => left.Equals(right);

        /// <summary>判断两个资源缓存键是否不相等。</summary>
        /// <param name="left">左侧操作数。</param>
        /// <param name="right">右侧操作数。</param>
        /// <returns>两个键不相等时返回 true，否则返回 false。</returns>
        public static bool operator !=(ResCacheKey left, ResCacheKey right) => !left.Equals(right);
    }
}
