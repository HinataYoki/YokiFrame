using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed partial class SpatialHashGrid<T> where T : ISpatialEntity
    {
        /// <summary>判断矩形 cell 范围是否应改为扫描实体，避免极大空范围拖垮查询。</summary>
        private bool ShouldUseLinearScan(int minCellA, int maxCellA, int minCellB, int maxCellB)
        {
            long spanA = (long)maxCellA - minCellA + 1L;
            long spanB = (long)maxCellB - minCellB + 1L;
            int occupiedCellCount = mCells.Count;
            return occupiedCellCount == 0
                || spanA > occupiedCellCount
                || spanB > occupiedCellCount
                || spanA * spanB > occupiedCellCount;
        }

        /// <summary>在线性扫描中追加位于完整三维包围盒内的实体。</summary>
        private void AppendAllWithinBounds(YokiBounds bounds, List<T> results)
        {
            foreach (T entity in mEntities.Values)
            {
                if (bounds.Contains(entity.Position))
                {
                    results.Add(entity);
                }
            }
        }

        /// <summary>构造有限最近邻查询的溢出安全 cell 范围，并选择网格或线性路径。</summary>
        private T QueryNearestWithinDistance(YokiVector3 position, float maxDistance, Func<T, bool> filter)
        {
            float nearestDistanceSquared = maxDistance * maxDistance;
            int searchRadius = SpatialMath.CeilToInt(maxDistance * mInverseCellSize);
            int centerA = SpatialMath.FloorToInt(position.X * mInverseCellSize);
            int centerB = SpatialMath.FloorToInt(SpatialMath.GetPlaneCoordinate(position, mPlane) * mInverseCellSize);
            int minCellA = ClampCellCoordinate((long)centerA - searchRadius);
            int maxCellA = ClampCellCoordinate((long)centerA + searchRadius);
            int minCellB = ClampCellCoordinate((long)centerB - searchRadius);
            int maxCellB = ClampCellCoordinate((long)centerB + searchRadius);
            if (ShouldUseLinearScan(minCellA, maxCellA, minCellB, maxCellB))
            {
                return QueryNearestLinear(position, nearestDistanceSquared, filter);
            }

            return QueryNearestCells(
                position,
                filter,
                nearestDistanceSquared,
                minCellA,
                maxCellA,
                minCellB,
                maxCellB);
        }

        /// <summary>在有界 cell 范围内查找满足距离和过滤条件的最近实体。</summary>
        private T QueryNearestCells(
            YokiVector3 position,
            Func<T, bool> filter,
            float nearestDistanceSquared,
            int minCellA,
            int maxCellA,
            int minCellB,
            int maxCellB)
        {
            T nearest = default(T);
            bool found = false;
            for (long cellA = minCellA; cellA <= maxCellA; cellA++)
            {
                for (long cellB = minCellB; cellB <= maxCellB; cellB++)
                {
                    if (!mCells.TryGetValue(ComputeHash((int)cellA, (int)cellB), out List<T> cell)) continue;
                    UpdateNearestFromCell(cell, position, filter, ref nearest, ref nearestDistanceSquared, ref found);
                }
            }

            return found ? nearest : default(T);
        }

        /// <summary>把一个 cell 中满足约束的实体合并到当前最近邻结果。</summary>
        private void UpdateNearestFromCell(
            List<T> cell,
            YokiVector3 position,
            Func<T, bool> filter,
            ref T nearest,
            ref float nearestDistanceSquared,
            ref bool found)
        {
            for (int index = 0; index < cell.Count; index++)
            {
                T entity = cell[index];
                if (filter != null && !filter(entity)) continue;
                float distanceSquared = SpatialMath.GetProjectedDistanceSquared(entity.Position, position, mPlane);
                if (distanceSquared > nearestDistanceSquared) continue;
                nearest = entity;
                nearestDistanceSquared = distanceSquared;
                found = true;
            }
        }

        /// <summary>在线性扫描中按给定距离平方查找最近实体。</summary>
        private T QueryNearestLinear(YokiVector3 position, float nearestDistanceSquared, Func<T, bool> filter)
        {
            T nearest = default(T);
            bool found = false;
            foreach (T entity in mEntities.Values)
            {
                if (filter != null && !filter(entity)) continue;
                float distanceSquared = SpatialMath.GetProjectedDistanceSquared(entity.Position, position, mPlane);
                if (distanceSquared > nearestDistanceSquared) continue;
                nearest = entity;
                nearestDistanceSquared = distanceSquared;
                found = true;
            }

            return found ? nearest : default(T);
        }

        /// <summary>把 long cell 坐标钳制到哈希编码支持的 int 范围。</summary>
        private static int ClampCellCoordinate(long value)
        {
            if (value <= int.MinValue) return int.MinValue;
            if (value >= int.MaxValue) return int.MaxValue;
            return (int)value;
        }
    }
}
