using System;

namespace YokiFrame
{
    /// <summary>
    /// Declares the hierarchy path used when a <see cref="MonoSingleton{T}"/> is auto-created.
    /// </summary>
    /// <example>
    /// <c>[MonoSingletonPath("YokiFrame/ActionKit/Queue")]</c>
    /// </example>
    [AttributeUsage(AttributeTargets.Class)]
    public class MonoSingletonPathAttribute : Attribute
    {
        /// <summary>
        /// Hierarchy path used to find or create the singleton GameObject.
        /// </summary>
        public string PathInHierarchy { get; }

        /// <summary>
        /// Whether the created GameObject should use a <c>RectTransform</c>.
        /// </summary>
        public bool IsRectTransform { get; }

        /// <summary>
        /// Creates a hierarchy-path hint for a MonoSingleton.
        /// </summary>
        public MonoSingletonPathAttribute(string pathInHierarchy, bool isRectTransform = false)
        {
            PathInHierarchy = pathInHierarchy;
            IsRectTransform = isRectTransform;
        }
    }
}
