#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>标识可以从自身 Inspector 独立生成 Designer 的绑定 owner。</summary>
    internal enum UIKitGeneratedOwnerKind
    {
        /// <summary>Panel/Component 内部或保存为独立 Prefab 的局部 UIElement。</summary>
        Element = 1,

        /// <summary>可跨面板复用的 UIComponent。</summary>
        Component = 2,
    }
}
#endif
