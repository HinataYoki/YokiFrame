using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 限制节点图中该类型节点的最大数量
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DisallowMultipleNodesAttribute : Attribute
    {
        public int Max { get; }

        public DisallowMultipleNodesAttribute(int max = 1)
        {
            Max = max;
        }
    }
}
