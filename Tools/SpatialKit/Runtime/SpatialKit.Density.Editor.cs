#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>提供只在 Editor/Tools 编译的密度诊断入口。</summary>
    public static partial class SpatialKit
    {
        /// <summary>按需生成活跃索引的密度聚合，不参与运行时热路径。</summary>
        /// <param name="resolution">每个轴的 bin 数。</param>
        /// <returns>有界密度快照列表。</returns>
        internal static IReadOnlyList<SpatialDensitySnapshot> CreateDensitySnapshots(int resolution)
        {
            return SpatialKitDiagnosticsRegistry.CreateDensitySnapshots(resolution);
        }
    }
}
#endif
