using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 对池化引用类型使用引用相等的比较器，避免业务 Equals 影响重复回收判断。
    /// </summary>
    /// <typeparam name="T">比较的对象类型。</typeparam>
    internal sealed class PoolReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        /// <summary>
        /// 获取共享比较器实例。
        /// </summary>
        public static readonly PoolReferenceEqualityComparer<T> Instance = new();

        /// <summary>
        /// 比较两个对象是否为同一个池化实例。
        /// </summary>
        /// <param name="x">左侧对象。</param>
        /// <param name="y">右侧对象。</param>
        /// <returns>相等时返回 true。</returns>
        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        /// <summary>
        /// 获取不受业务 GetHashCode 重写影响的运行时引用哈希。
        /// </summary>
        /// <param name="obj">待计算哈希的对象。</param>
        /// <returns>对象哈希。</returns>
        public int GetHashCode(T obj)
        {
            return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }
}
