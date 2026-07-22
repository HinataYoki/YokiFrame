#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 暴露 Bind 组件最小只读信息，供 Unity Editor 扫描器读取。
    /// </summary>
    public interface IBind
    {
        /// <summary>获取绑定语义。</summary>
        BindType Bind { get; }

        /// <summary>获取生成字段名。</summary>
        string Name { get; }

        /// <summary>获取最终目标类型名。</summary>
        string Type { get; }

        /// <summary>获取生成字段注释。</summary>
        string Comment { get; }

        /// <summary>获取绑定节点 Transform。</summary>
        Transform Transform { get; }
    }
}
#endif
