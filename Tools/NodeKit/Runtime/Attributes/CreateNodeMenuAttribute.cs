using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 设置节点在创建菜单中的路径
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CreateNodeMenuAttribute : Attribute
    {
        public string MenuName { get; }
        public int Order { get; }

        public CreateNodeMenuAttribute(string menuName, int order = 0)
        {
            MenuName = menuName;
            Order = order;
        }
    }
}
