#if !GODOT
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 声明 MonoSingleton 在层级视图中的创建路径
    /// </summary>
    /// <example>
    /// <c>[MonoSingletonPath("YokiFrame/ActionKit/Queue")]</c>（示例：ActionKit 队列单例路径）
    /// </example>
    [AttributeUsage(AttributeTargets.Class)]
    public class MonoSingletonPathAttribute : Attribute
    {
        /// <summary>
        /// 获取层级路径，用于查找或创建单例 GameObject。
        /// </summary>
        public string PathInHierarchy { get; }

        /// <summary>
        /// 获取创建的 GameObject 是否使用 RectTransform。
        /// </summary>
        public bool IsRectTransform { get; }

        /// <summary>
        /// 创建单例层级路径声明。
        /// </summary>
        /// <param name="pathInHierarchy">层级路径。</param>
        /// <param name="isRectTransform">创建的 GameObject 是否使用 RectTransform。</param>
        public MonoSingletonPathAttribute(string pathInHierarchy, bool isRectTransform = false)
        {
            PathInHierarchy = pathInHierarchy;
            IsRectTransform = isRectTransform;
        }
    }
}
#endif
