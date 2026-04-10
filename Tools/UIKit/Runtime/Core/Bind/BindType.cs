using UnityEngine;

namespace YokiFrame
{
    public enum BindType
    {
        /// <summary>
        /// 成员
        /// </summary>
        [InspectorName("成员")]
        Member,
        /// <summary>
        /// 元素
        /// </summary>
        [InspectorName("元素")]
        Element,
        /// <summary>
        /// 组件
        /// </summary>
        [InspectorName("组件")]
        Component,
        /// <summary>
        /// 叶子
        /// </summary>
        [InspectorName("叶子")]
        Leaf,
    }
}