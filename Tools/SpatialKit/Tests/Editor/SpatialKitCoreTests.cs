#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using NUnit.Framework;
using YokiFrame;

namespace YokiFrame.Tests
{
    /// <summary>验证 SpatialKit 核心索引行为和迁移后的边界契约。</summary>
    public sealed class SpatialKitCoreTests
    {
        /// <summary>验证 HashGrid 的二维投影查询不会把被忽略轴纳入半径距离。</summary>
        [Test]
        public void HashGrid_QueryRadius_UsesProjectedDistance()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(2f, SpatialPlane.XZ);
            grid.Insert(new MutableEntity(1, new YokiVector3(0f, 100f, 0f)));
            var results = new List<MutableEntity>();

            grid.QueryRadius(YokiVector3.Zero, 1f, results);

            Assert.AreEqual(1, results.Count);
        }

        /// <summary>验证极大有限半径不会溢出 cell 循环，并能在线性回退中返回实体。</summary>
        [Test, Timeout(1000)]
        public void HashGrid_QueryRadius_ExtremeFiniteRangeCompletes()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            grid.Insert(new MutableEntity(101, YokiVector3.Zero));
            var results = new List<MutableEntity>();

            grid.QueryRadius(YokiVector3.Zero, float.MaxValue, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(101, results[0].SpatialId);
        }

        /// <summary>验证横跨完整 int cell 范围的 Bounds 查询会限时完成并保留三维过滤。</summary>
        [Test, Timeout(1000)]
        public void HashGrid_QueryBounds_ExtremeFiniteRangeCompletes()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            grid.Insert(new MutableEntity(102, YokiVector3.Zero));
            var results = new List<MutableEntity>();
            YokiBounds bounds = new(YokiVector3.Zero, new YokiVector3(float.MaxValue, 1f, float.MaxValue));

            grid.QueryBounds(bounds, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(102, results[0].SpatialId);
        }

        /// <summary>验证有限最近邻距离产生 int 极值跨度时不会发生加减溢出或长时间空扫描。</summary>
        [Test, Timeout(1000)]
        public void HashGrid_QueryNearest_ExtremeFiniteRangeCompletes()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            grid.Insert(new MutableEntity(103, new YokiVector3(1f, 0f, 0f)));

            MutableEntity nearest = grid.QueryNearest(YokiVector3.Zero, 3_000_000_000f);

            Assert.IsNotNull(nearest);
            Assert.AreEqual(103, nearest.SpatialId);
        }

        /// <summary>验证 Quadtree 通过节点定位正确更新可变引用实体。</summary>
        [Test]
        public void Quadtree_UpdateMutableEntity_DoesNotDuplicate()
        {
            Quadtree<MutableEntity> tree = SpatialKit.CreateQuadtree<MutableEntity>(
                new YokiRect(-20f, -20f, 40f, 40f),
                maxDepth: 4,
                maxEntitiesPerNode: 1,
                plane: SpatialPlane.XZ);
            MutableEntity entity = new MutableEntity(1, YokiVector3.Zero);
            tree.Insert(entity);
            entity.PositionValue = new YokiVector3(10f, 0f, 10f);
            tree.Update(entity);
            var results = new List<MutableEntity>();

            tree.QueryRadius(entity.Position, 1f, results);

            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual(1, results.Count);
        }

        /// <summary>验证 Octree 通过节点定位正确更新可变引用实体。</summary>
        [Test]
        public void Octree_UpdateMutableEntity_DoesNotDuplicate()
        {
            Octree<MutableEntity> tree = SpatialKit.CreateOctree<MutableEntity>(
                new YokiBounds(YokiVector3.Zero, new YokiVector3(40f, 40f, 40f)),
                maxDepth: 4,
                maxEntitiesPerNode: 1);
            MutableEntity entity = new MutableEntity(1, YokiVector3.Zero);
            tree.Insert(entity);
            entity.PositionValue = new YokiVector3(10f, 10f, 10f);
            tree.Update(entity);
            var results = new List<MutableEntity>();

            tree.QueryRadius(entity.Position, 1f, results);

            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual(1, results.Count);
        }

        /// <summary>验证四叉树不会因根边界裁剪而漏掉投影越界实体。</summary>
        [Test]
        public void Quadtree_QueriesOverflowEntityOutsideRoot()
        {
            Quadtree<MutableEntity> tree = SpatialKit.CreateQuadtree<MutableEntity>(
                new YokiRect(0f, 0f, 10f, 10f), plane: SpatialPlane.XZ);
            MutableEntity entity = new MutableEntity(41, new YokiVector3(-20f, 0f, -20f));
            tree.Insert(entity);
            var radiusResults = new List<MutableEntity>();
            var boundsResults = new List<MutableEntity>();

            tree.QueryRadius(entity.Position, 0.1f, radiusResults);
            tree.QueryBounds(
                new YokiBounds(entity.Position, new YokiVector3(0.5f, 0.5f, 0.5f)),
                boundsResults);

            Assert.AreEqual(1, radiusResults.Count);
            Assert.AreEqual(1, boundsResults.Count);
            Assert.AreSame(entity, tree.QueryNearest(entity.Position, 0.1f));
        }

        /// <summary>验证八叉树不会因根边界裁剪而漏掉三维越界实体。</summary>
        [Test]
        public void Octree_QueriesOverflowEntityOutsideRoot()
        {
            Octree<MutableEntity> tree = SpatialKit.CreateOctree<MutableEntity>(
                new YokiBounds(YokiVector3.Zero, new YokiVector3(10f, 10f, 10f)));
            MutableEntity entity = new MutableEntity(42, new YokiVector3(20f, 20f, 20f));
            tree.Insert(entity);
            var radiusResults = new List<MutableEntity>();
            var boundsResults = new List<MutableEntity>();

            tree.QueryRadius(entity.Position, 0.1f, radiusResults);
            tree.QueryBounds(
                new YokiBounds(entity.Position, new YokiVector3(0.5f, 0.5f, 0.5f)),
                boundsResults);

            Assert.AreEqual(1, radiusResults.Count);
            Assert.AreEqual(1, boundsResults.Count);
            Assert.AreSame(entity, tree.QueryNearest(entity.Position, 0.1f));
        }

        /// <summary>验证相同 SpatialId 插入时替换旧实体而不是增加数量。</summary>
        [Test]
        public void InsertDuplicateId_ReplacesStoredEntity()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            grid.Insert(new MutableEntity(7, YokiVector3.Zero));
            grid.Insert(new MutableEntity(7, new YokiVector3(5f, 0f, 0f)));
            var results = new List<MutableEntity>();

            grid.QueryRadius(new YokiVector3(5f, 0f, 0f), 0.1f, results);

            Assert.AreEqual(1, grid.Count);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(7, results[0].SpatialId);
        }

        /// <summary>验证无效网格尺寸会在构造阶段立即失败。</summary>
        [Test]
        public void CreateHashGrid_InvalidCellSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialKit.CreateHashGrid<MutableEntity>(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialKit.CreateHashGrid<MutableEntity>(float.NaN));
        }

        /// <summary>验证快照捕获位置后不受原实体后续移动影响。</summary>
        [Test]
        public void Snapshot_CapturesPositionsAtCreationTime()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            MutableEntity entity = new MutableEntity(11, YokiVector3.Zero);
            grid.Insert(entity);
            ISpatialIndex<MutableEntity> index = grid;
            SpatialIndexSnapshot<MutableEntity> snapshot = index.CreateSnapshot();
            entity.PositionValue = new YokiVector3(10f, 0f, 0f);
            var results = new List<MutableEntity>();

            snapshot.QueryRadius(YokiVector3.Zero, 0.1f, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(11, results[0].SpatialId);
        }

        /// <summary>验证并行批量查询按输入索引写入对应结果列表。</summary>
        [Test]
        public void Snapshot_QueryRadiusBatchParallel_PreservesQuerySlots()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            grid.Insert(new MutableEntity(21, new YokiVector3(0f, 0f, 0f)));
            grid.Insert(new MutableEntity(22, new YokiVector3(10f, 0f, 0f)));
            SpatialIndexSnapshot<MutableEntity> snapshot = grid.CreateSnapshot();
            var queries = new[]
            {
                new SpatialRadiusQuery(YokiVector3.Zero, 0.5f),
                new SpatialRadiusQuery(new YokiVector3(10f, 0f, 0f), 0.5f)
            };
            var results = new List<List<MutableEntity>>
            {
                new List<MutableEntity>(),
                new List<MutableEntity>()
            };

            snapshot.QueryRadiusBatchParallel(queries, results);

            Assert.AreEqual(1, results[0].Count);
            Assert.AreEqual(21, results[0][0].SpatialId);
            Assert.AreEqual(1, results[1].Count);
            Assert.AreEqual(22, results[1][0].SpatialId);
        }

        /// <summary>验证两类并行批量查询都会拒绝重复结果容器，避免并发写入同一 List。</summary>
        [Test]
        public void Snapshot_ParallelBatchQueries_RejectDuplicateResultLists()
        {
            SpatialHashGrid<MutableEntity> grid = SpatialKit.CreateHashGrid<MutableEntity>(1f);
            grid.Insert(new MutableEntity(31, YokiVector3.Zero));
            SpatialIndexSnapshot<MutableEntity> snapshot = grid.CreateSnapshot();
            var sharedResults = new List<MutableEntity>();
            var results = new List<List<MutableEntity>> { sharedResults, sharedResults };
            var radiusQueries = new[]
            {
                new SpatialRadiusQuery(YokiVector3.Zero, 1f),
                new SpatialRadiusQuery(YokiVector3.Zero, 2f)
            };
            var boundsQueries = new[]
            {
                new YokiBounds(YokiVector3.Zero, new YokiVector3(1f, 1f, 1f)),
                new YokiBounds(YokiVector3.Zero, new YokiVector3(2f, 2f, 2f))
            };

            Assert.Throws<ArgumentException>(() => snapshot.QueryRadiusBatchParallel(radiusQueries, results));
            Assert.Throws<ArgumentException>(() => snapshot.QueryBoundsBatchParallel(boundsQueries, results));
        }

        /// <summary>验证 SpatialRadiusQuery 满足泛型值相等契约与重载运算符一致性。</summary>
        [Test]
        public void SpatialRadiusQuery_EqualityAndOperatorsUseValueSemantics()
        {
            var query1 = new SpatialRadiusQuery(new YokiVector3(1f, 2f, 3f), 5f);
            var query2 = new SpatialRadiusQuery(new YokiVector3(1f, 2f, 3f), 5f);
            var query3 = new SpatialRadiusQuery(new YokiVector3(1f, 2f, 3f), 10f);
            var defaultQuery = default(SpatialRadiusQuery);

            Assert.AreEqual(query1, query2);
            Assert.IsTrue(query1 == query2);
            Assert.IsFalse(query1 != query2);
            Assert.IsTrue(query1 != query3);
            Assert.IsTrue(query1 != defaultQuery);
            Assert.AreEqual(query1.GetHashCode(), query2.GetHashCode());
        }

        /// <summary>提供可变引用实体以覆盖真实游戏对象移动路径。</summary>
        private sealed class MutableEntity : ISpatialEntity
        {
            /// <summary>创建测试实体。</summary>
            /// <param name="spatialId">实体编号。</param>
            /// <param name="position">初始位置。</param>
            public MutableEntity(int spatialId, YokiVector3 position)
            {
                SpatialId = spatialId;
                PositionValue = position;
            }

            /// <summary>获取实体编号。</summary>
            public int SpatialId { get; }

            /// <summary>获取或设置测试实体位置。</summary>
            public YokiVector3 PositionValue { get; set; }

            /// <summary>获取当前空间位置。</summary>
            public YokiVector3 Position { get { return PositionValue; } }
        }
    }
}
#endif
