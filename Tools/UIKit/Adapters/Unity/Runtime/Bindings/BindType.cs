#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 描述 Bind 节点的兼容语义；数值顺序属于旧 Prefab 序列化契约，不得调整。
    /// </summary>
    public enum BindType
    {
        /// <summary>直接暴露节点上的组件或 GameObject。</summary>
        [InspectorName("成员")]
        Member = 0,

        /// <summary>生成面板内部可复用的嵌套 UIElement 类型。</summary>
        [InspectorName("元素")]
        Element = 1,

        /// <summary>生成跨面板复用的 UIComponent 类型。</summary>
        [InspectorName("组件")]
        Component = 2,

        /// <summary>仅标记节点，不参与字段和代码生成。</summary>
        [InspectorName("叶子")]
        Leaf = 3,
    }
}
#endif
