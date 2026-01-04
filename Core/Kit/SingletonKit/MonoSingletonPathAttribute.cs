using System;

namespace YokiFrame
{
    /// <summary>
    /// 用于定义生成的 Mono 单例所在的层级路径
    /// 例如: [MonoSingletonPath("YokiFrame/ActionKit/Queue")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MonoSingletonPathAttribute : Attribute
    {
        public string PathInHierarchy { get; }
        public bool IsRectTransform { get; }

        public MonoSingletonPathAttribute(string pathInHierarchy, bool isRectTransform = false)
        {
            PathInHierarchy = pathInHierarchy;
            IsRectTransform = isRectTransform;
        }
    }
}
