using System;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 端口方向
    /// </summary>
    public enum PortIO
    {
        Input,
        Output
    }

    /// <summary>
    /// 连接类型
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>新连接覆盖旧连接</summary>
        Override,
        /// <summary>允许多个连接</summary>
        Multiple
    }

    /// <summary>
    /// 类型约束
    /// </summary>
    public enum TypeConstraint
    {
        /// <summary>无约束</summary>
        None,
        /// <summary>允许继承类型（输入可接受输出的派生类）</summary>
        Inherited,
        /// <summary>严格类型匹配</summary>
        Strict,
        /// <summary>反向继承（输出可接受输入的派生类）</summary>
        InheritedInverse,
        /// <summary>双向继承</summary>
        InheritedAny
    }

    /// <summary>
    /// 后备值显示模式
    /// </summary>
    public enum ShowBackingValue
    {
        /// <summary>始终显示</summary>
        Always,
        /// <summary>未连接时显示</summary>
        Unconnected,
        /// <summary>从不显示</summary>
        Never
    }
}
