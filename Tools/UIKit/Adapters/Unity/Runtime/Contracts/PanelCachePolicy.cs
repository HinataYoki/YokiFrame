#if UNITY_2022_3_OR_NEWER
namespace YokiFrame
{
    /// <summary>
    /// 定义 UIKit 面板关闭后的实例保留策略。
    /// </summary>
    public enum PanelCachePolicy
    {
        /// <summary>关闭后进入有界复用缓存；这是默认策略。</summary>
        Reusable = 0,

        /// <summary>关闭后立即销毁实例并释放 Prefab lease。</summary>
        Transient = 1,

        /// <summary>关闭后持续保留实例，直到显式清理 UIKit。</summary>
        Persistent = 2
    }
}
#endif
