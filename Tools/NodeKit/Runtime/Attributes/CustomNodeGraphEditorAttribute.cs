using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 关联自定义图编辑器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomNodeGraphEditorAttribute : Attribute
    {
        public Type InspectedType { get; }

        public CustomNodeGraphEditorAttribute(Type inspectedType)
        {
            InspectedType = inspectedType;
        }
    }
}
