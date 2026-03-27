using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 设置节点的着色颜色
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeTintAttribute : Attribute
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public NodeTintAttribute(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public NodeTintAttribute(int r, int g, int b)
        {
            R = r / 255f;
            G = g / 255f;
            B = b / 255f;
        }

        public NodeTintAttribute(string hex)
        {
            if (hex.StartsWith("#")) hex = hex[1..];
            R = Convert.ToInt32(hex[..2], 16) / 255f;
            G = Convert.ToInt32(hex[2..4], 16) / 255f;
            B = Convert.ToInt32(hex[4..6], 16) / 255f;
        }
    }
}
