using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 自动确保图中存在指定类型的节点，并防止其被删除
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireNodeAttribute : Attribute
    {
        public Type Type0 { get; }
        public Type Type1 { get; }
        public Type Type2 { get; }

        public RequireNodeAttribute(Type type)
        {
            Type0 = type;
        }

        public RequireNodeAttribute(Type type, Type type2)
        {
            Type0 = type;
            Type1 = type2;
        }

        public RequireNodeAttribute(Type type, Type type2, Type type3)
        {
            Type0 = type;
            Type1 = type2;
            Type2 = type3;
        }

        public bool Requires(Type type)
        {
            if (type == default) return false;
            return type == Type0 || type == Type1 || type == Type2;
        }
    }
}
