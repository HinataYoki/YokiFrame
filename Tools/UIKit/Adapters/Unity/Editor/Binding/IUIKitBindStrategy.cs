#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 描述 Bind 代码生成形态，文件布局由统一生成服务解释。
    /// </summary>
    internal enum UIKitBindOutputKind
    {
        /// <summary>在当前 owner 上生成一个对象引用字段。</summary>
        Member,

        /// <summary>生成面板内部的 UIElement partial 类型。</summary>
        Element,

        /// <summary>生成跨面板复用的 UIComponent partial 类型。</summary>
        Component,

        /// <summary>跳过当前节点及其子树的代码生成。</summary>
        Marker,
    }

    /// <summary>
    /// 定义一种 Editor-only Bind 语义；输出路径和代码提交不属于策略职责。
    /// </summary>
    internal interface IUIKitBindStrategy
    {
        /// <summary>获取兼容旧 Prefab 的 BindType。</summary>
        BindType LegacyType { get; }

        /// <summary>获取生成形态。</summary>
        UIKitBindOutputKind OutputKind { get; }

        /// <summary>获取当前节点是否建立独立的子绑定作用域。</summary>
        bool CanContainChildren { get; }

        /// <summary>
        /// 解析当前 Bind 的类型与对象引用，不执行文件或 Prefab 写入。
        /// </summary>
        /// <param name="bind">待解析的兼容 Bind 组件。</param>
        /// <param name="typeName">成功时输出字段或生成类类型名。</param>
        /// <param name="target">成功时输出可回填对象；生成类型可以为空。</param>
        /// <param name="error">失败时输出可定位原因。</param>
        /// <returns>解析成功时返回 true。</returns>
        bool TryResolve(
            AbstractBind bind,
            out string typeName,
            out Object target,
            out string error);

        /// <summary>
        /// 验证直接子 BindType 是否符合当前语义。
        /// </summary>
        /// <param name="childType">待验证子类型。</param>
        /// <param name="error">不允许时输出原因。</param>
        /// <returns>允许时返回 true。</returns>
        bool TryValidateChild(BindType childType, out string error);
    }
}
#endif
