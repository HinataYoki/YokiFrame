using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 关联自定义节点编辑器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomNodeEditorAttribute : Attribute
    {
        public Type InspectedType { get; }

        public CustomNodeEditorAttribute(Type inspectedType)
        {
            InspectedType = inspectedType;
        }
    }
}
