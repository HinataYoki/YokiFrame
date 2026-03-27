using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 设置节点的宽度
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeWidthAttribute : Attribute
    {
        public int Width { get; }

        public NodeWidthAttribute(int width)
        {
            Width = width;
        }
    }
}
