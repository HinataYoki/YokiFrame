#if UNITY_5_3_OR_NEWER
using System;

namespace YokiFrame
{
    /// <summary>
    /// 声明 MonoSingleton 在 Unity 层级视图中的查找或创建路径。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MonoSingletonPathAttribute : Attribute
    {
        /// <summary>
        /// 获取层级路径，用于查找或创建单例 GameObject。
        /// </summary>
        public string PathInHierarchy { get; private set; }

        /// <summary>
        /// 获取最后一级 GameObject 是否使用 RectTransform。
        /// </summary>
        public bool IsRectTransform { get; private set; }

        /// <summary>
        /// 创建单例层级路径声明。
        /// </summary>
        /// <param name="pathInHierarchy">层级路径。</param>
        /// <param name="isRectTransform">最后一级 GameObject 是否使用 RectTransform。</param>
        public MonoSingletonPathAttribute(string pathInHierarchy, bool isRectTransform = false)
        {
            PathInHierarchy = pathInHierarchy;
            IsRectTransform = isRectTransform;
        }
    }
}
#endif
