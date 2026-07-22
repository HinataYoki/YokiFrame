#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Linq;
using NUnit.Framework;
using YokiFrame;

namespace YokiFrame.Tests
{
    /// <summary>验证 SpatialKit Editor-only Gizmo 快照的几何语义和预算边界。</summary>
    public sealed class SpatialKitGizmoDiagnosticsTests
    {
        /// <summary>验证 Octree 输出真实三维节点，并在预算不足时明确标记裁剪。</summary>
        [Test]
        public void OctreeGizmoSnapshot_ContainsVolumeNodesAndTruncation()
        {
            Octree<GizmoEntity> tree = SpatialKit.CreateOctree<GizmoEntity>(
                new YokiBounds(YokiVector3.Zero, new YokiVector3(20f, 20f, 20f)),
                maxDepth: 4,
                maxEntitiesPerNode: 1);
            tree.Insert(new GizmoEntity(1, new YokiVector3(-4f, -4f, -4f)));
            tree.Insert(new GizmoEntity(2, new YokiVector3(4f, 4f, 4f)));

            SpatialGizmoDiagnosticsFrame frame = SpatialKit.CreateGizmoDiagnosticsFrame(2, 1);
            SpatialGizmoIndexSnapshot snapshot = frame.Indexes.Single(
                item => item.DiagnosticsId == tree.DiagnosticsId);

            Assert.IsTrue(snapshot.IsVolume);
            Assert.AreEqual(2, snapshot.Nodes.Count);
            Assert.IsTrue(snapshot.Nodes.All(node => node.IsVolume));
            Assert.AreEqual(1, snapshot.Entities.Count);
            Assert.IsTrue(snapshot.NodesTruncated);
            Assert.IsTrue(snapshot.EntitiesTruncated);
        }

        /// <summary>验证 HashGrid 与 Quadtree 保留各自二维投影平面和节点边界。</summary>
        [Test]
        public void PlanarGizmoSnapshots_PreservePlaneAndBounds()
        {
            SpatialHashGrid<GizmoEntity> grid = SpatialKit.CreateHashGrid<GizmoEntity>(2f, SpatialPlane.XY);
            Quadtree<GizmoEntity> tree = SpatialKit.CreateQuadtree<GizmoEntity>(
                new YokiRect(-10f, -10f, 20f, 20f),
                maxDepth: 4,
                maxEntitiesPerNode: 1,
                plane: SpatialPlane.XZ);
            GizmoEntity entity = new(1, new YokiVector3(3f, 2f, 4f));
            grid.Insert(entity);
            tree.Insert(entity);

            SpatialGizmoDiagnosticsFrame frame = SpatialKit.CreateGizmoDiagnosticsFrame(32, 32);
            SpatialGizmoIndexSnapshot gridSnapshot = frame.Indexes.Single(
                item => item.DiagnosticsId == grid.DiagnosticsId);
            SpatialGizmoIndexSnapshot treeSnapshot = frame.Indexes.Single(
                item => item.DiagnosticsId == tree.DiagnosticsId);

            Assert.AreEqual(SpatialPlane.XY, gridSnapshot.Plane);
            Assert.IsFalse(gridSnapshot.Nodes[0].IsVolume);
            Assert.AreEqual(2f, gridSnapshot.Nodes[0].Bounds2D.Width);
            Assert.AreEqual(SpatialPlane.XZ, treeSnapshot.Plane);
            Assert.AreEqual(20f, treeSnapshot.Nodes[0].Bounds2D.Width);
        }

        /// <summary>提供具有稳定编号和不可变位置的测试实体。</summary>
        private sealed class GizmoEntity : ISpatialEntity
        {
            /// <summary>创建测试实体。</summary>
            internal GizmoEntity(int spatialId, YokiVector3 position)
            {
                SpatialId = spatialId;
                Position = position;
            }

            /// <summary>获取稳定空间编号。</summary>
            public int SpatialId { get; }

            /// <summary>获取测试位置。</summary>
            public YokiVector3 Position { get; }
        }
    }
}
#endif
