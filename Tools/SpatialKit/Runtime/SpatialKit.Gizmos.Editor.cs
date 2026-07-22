#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>提供仅供宿主工具适配层调用的 SpatialKit Gizmo 诊断入口。</summary>
    public static partial class SpatialKit
    {
        /// <summary>按单索引预算创建当前全部空间索引的几何快照。</summary>
        /// <param name="maxNodes">单索引最大节点数量。</param>
        /// <param name="maxEntities">单索引最大实体数量。</param>
        /// <returns>与当前诊断版本绑定的有界几何帧。</returns>
        internal static SpatialGizmoDiagnosticsFrame CreateGizmoDiagnosticsFrame(int maxNodes, int maxEntities)
        {
            return SpatialKitDiagnosticsRegistry.CreateGizmoFrame(maxNodes, maxEntities);
        }
    }
}
#endif
