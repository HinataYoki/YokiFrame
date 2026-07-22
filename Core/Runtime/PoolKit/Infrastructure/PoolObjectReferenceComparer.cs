#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 面向 object 键的引用相等比较器，用于诊断表避免重写 Equals 的对象互相覆盖。
    /// </summary>
    internal sealed class PoolObjectReferenceComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// 获取共享比较器实例。
        /// </summary>
        public static readonly PoolObjectReferenceComparer Instance = new();

        /// <summary>
        /// 按引用比较两个对象。
        /// </summary>
        /// <param name="x">左侧对象。</param>
        /// <param name="y">右侧对象。</param>
        /// <returns>引用相同时返回 true。</returns>
        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        /// <summary>
        /// 获取运行时引用哈希。
        /// </summary>
        /// <param name="obj">对象实例。</param>
        /// <returns>引用哈希。</returns>
        public int GetHashCode(object obj)
        {
            return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
