using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 标记字段为输出端口
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OutputAttribute : Attribute
    {
        public ShowBackingValue BackingValue { get; }
        public ConnectionType ConnectionType { get; }
        public TypeConstraint TypeConstraint { get; }
        public bool DynamicPortList { get; }

        public OutputAttribute(
            ShowBackingValue backingValue = ShowBackingValue.Never,
            ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None,
            bool dynamicPortList = false)
        {
            BackingValue = backingValue;
            ConnectionType = connectionType;
            TypeConstraint = typeConstraint;
            DynamicPortList = dynamicPortList;
        }
    }
}
