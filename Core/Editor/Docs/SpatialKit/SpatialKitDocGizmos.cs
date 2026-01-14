#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SpatialKit 可视化调试
    /// </summary>
    internal static class SpatialKitDocGizmos
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "可视化调试",
                Description = "Scene 视图中绘制空间索引结构。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "绘制空间索引",
                        Code = @"#if UNITY_EDITOR
using YokiFrame.Editor;

private void OnDrawGizmos()
{
    if (mHashGrid != default)
        SpatialKitGizmos.DrawHashGrid(mHashGrid, Color.green);
    
    if (mQuadtree != default)
        SpatialKitGizmos.DrawQuadtree(mQuadtree, Color.cyan);
    
    if (mOctree != default)
        SpatialKitGizmos.DrawOctree(mOctree, Color.yellow);
}
#endif",
                        Explanation = "SpatialKitGizmos 提供三种索引的绘制方法。"
                    }
                }
            };
        }
    }
}
#endif
