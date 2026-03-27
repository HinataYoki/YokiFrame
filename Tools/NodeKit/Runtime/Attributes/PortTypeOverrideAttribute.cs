using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 覆盖端口的值类型，使其与序列化字段类型不同
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class PortTypeOverrideAttribute : Attribute
    {
        public Type Type { get; }

        public PortTypeOverrideAttribute(Type type)
        {
            Type = type;
        }
    }
}
