#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>把 SpatialKit 诊断摘要序列化为有界 state JSON。</summary>
    internal static class SpatialKitSnapshotWriter
    {
        private const int MAX_INDEXES = 128;
        private const int STATE_DENSITY_RESOLUTION = 8;

        /// <summary>创建 SpatialKit 统计 JSON。</summary>
        /// <returns>包含版本、索引数量、实体数量和分区数量的 JSON。</returns>
        internal static string WriteStats()
        {
            SpatialKitDiagnosticsSnapshot snapshot = SpatialKit.CreateDiagnosticsSnapshot();
            int entityCount = 0;
            int partitionCount = 0;
            int hashGridCount = 0;
            int quadtreeCount = 0;
            int octreeCount = 0;
            for (int index = 0; index < snapshot.Indexes.Count; index++)
            {
                SpatialIndexDiagnosticsSnapshot item = snapshot.Indexes[index];
                entityCount += item.Count;
                partitionCount += item.PartitionCount;
                if (string.Equals(item.IndexKind, "HashGrid", StringComparison.Ordinal)) hashGridCount++;
                else if (string.Equals(item.IndexKind, "Quadtree", StringComparison.Ordinal)) quadtreeCount++;
                else if (string.Equals(item.IndexKind, "Octree", StringComparison.Ordinal)) octreeCount++;
            }

            var builder = new StringBuilder(256);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(SpatialKit.GetDiagnosticsVersion())
                .Append(",\"activeIndexCount\":")
                .Append(snapshot.Indexes.Count)
                .Append(",\"totalCreatedIndexCount\":")
                .Append(snapshot.TotalCreatedIndexCount)
                .Append(",\"releasedIndexCount\":")
                .Append(snapshot.ReleasedIndexCount)
                .Append(",\"entityCount\":")
                .Append(entityCount)
                .Append(",\"partitionCount\":")
                .Append(partitionCount)
                .Append(",\"hashGridCount\":")
                .Append(hashGridCount)
                .Append(",\"quadtreeCount\":")
                .Append(quadtreeCount)
                .Append(",\"octreeCount\":")
                .Append(octreeCount)
                .Append('}');
            return builder.ToString();
        }

        /// <summary>创建包含统计和索引详情的完整 Workbench 状态。</summary>
        /// <returns>固定 schema 的 SpatialKit state JSON。</returns>
        internal static string WriteWorkbench()
        {
            SpatialKitDiagnosticsSnapshot snapshot = SpatialKit.CreateDiagnosticsSnapshot();
            IReadOnlyList<SpatialDensitySnapshot> densities = SpatialKit.CreateDensitySnapshots(STATE_DENSITY_RESOLUTION);
            var builder = new StringBuilder(2048);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(SpatialKit.GetDiagnosticsVersion())
                .Append(",\"stats\":")
                .Append(WriteStats(snapshot))
                .Append(",\"indexes\":[");

            int count = Math.Min(snapshot.Indexes.Count, MAX_INDEXES);
            for (int index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendIndex(builder, snapshot.Indexes[index], FindDensity(densities, snapshot.Indexes[index].DiagnosticsId));
            }

            builder.Append("],\"indexCount\":")
                .Append(snapshot.Indexes.Count)
                .Append(",\"indexesTruncated\":")
                .Append(snapshot.Indexes.Count > count ? "true" : "false")
                .Append('}');
            return builder.ToString();
        }

        /// <summary>创建只包含索引详情的列表 JSON。</summary>
        /// <returns>包含 indexes、总数和裁剪标记的 JSON。</returns>
        internal static string WriteIndexes()
        {
            SpatialKitDiagnosticsSnapshot snapshot = SpatialKit.CreateDiagnosticsSnapshot();
            var builder = new StringBuilder(2048);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(SpatialKit.GetDiagnosticsVersion())
                .Append(",\"indexes\":[");
            int count = Math.Min(snapshot.Indexes.Count, MAX_INDEXES);
            for (int index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendIndex(builder, snapshot.Indexes[index], null);
            }

            builder.Append("],\"count\":")
                .Append(snapshot.Indexes.Count)
                .Append(",\"indexesTruncated\":")
                .Append(snapshot.Indexes.Count > count ? "true" : "false")
                .Append('}');
            return builder.ToString();
        }

        /// <summary>创建指定索引的详细密度 JSON；未指定索引时返回全部有界密度。</summary>
        /// <param name="payloadJson">可选 diagnosticsId 和 resolution JSON。</param>
        /// <returns>固定 schema 的密度 JSON。</returns>
        internal static string WriteDensity(string payloadJson)
        {
            ReadDensityRequest(payloadJson, out string diagnosticsId, out int resolution);
            IReadOnlyList<SpatialDensitySnapshot> densities = SpatialKit.CreateDensitySnapshots(resolution);
            var builder = new StringBuilder(4096);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(SpatialKit.GetDiagnosticsVersion())
                .Append(",\"resolution\":")
                .Append(resolution)
                .Append(",\"indexes\":[");
            bool first = true;
            int matchedCount = 0;
            for (int index = 0; index < densities.Count; index++)
            {
                SpatialDensitySnapshot density = densities[index];
                if (!string.IsNullOrWhiteSpace(diagnosticsId)
                    && !string.Equals(diagnosticsId, density.DiagnosticsId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                matchedCount++;
                AppendDensity(builder, density, true);
            }

            builder.Append("],\"count\":").Append(matchedCount).Append('}');
            return builder.ToString();
        }

        /// <summary>创建供 CLI/AI 使用的统计与密度聚合诊断。</summary>
        /// <returns>固定 schema 的 SpatialKit 分析 JSON。</returns>
        internal static string WriteAnalysis()
        {
            SpatialKitDiagnosticsSnapshot snapshot = SpatialKit.CreateDiagnosticsSnapshot();
            IReadOnlyList<SpatialDensitySnapshot> densities = SpatialKit.CreateDensitySnapshots(32);
            long version = SpatialKit.GetDiagnosticsVersion();
            var builder = new StringBuilder(4096);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(version)
                .Append(",\"stats\":")
                .Append(WriteStats(snapshot))
                .Append(",\"density\":{\"schemaVersion\":1,\"version\":")
                .Append(version)
                .Append(",\"resolution\":32,\"indexes\":[");
            bool first = true;
            for (int i = 0; i < densities.Count; i++)
            {
                if (!first) builder.Append(',');
                first = false;
                AppendDensity(builder, densities[i], true);
            }
            builder.Append("],\"count\":").Append(densities.Count).Append("}}");
            return builder.ToString();
        }

        /// <summary>使用已采样索引生成统计对象，避免同一命令重复清理弱引用。</summary>
        /// <param name="snapshot">当前诊断采样。</param>
        /// <returns>嵌入 Workbench 状态的统计 JSON。</returns>
        private static string WriteStats(SpatialKitDiagnosticsSnapshot snapshot)
        {
            int entityCount = 0;
            int partitionCount = 0;
            for (int index = 0; index < snapshot.Indexes.Count; index++)
            {
                entityCount += snapshot.Indexes[index].Count;
                partitionCount += snapshot.Indexes[index].PartitionCount;
            }

            var builder = new StringBuilder(192);
            builder.Append("{\"activeIndexCount\":")
                .Append(snapshot.Indexes.Count)
                .Append(",\"totalCreatedIndexCount\":")
                .Append(snapshot.TotalCreatedIndexCount)
                .Append(",\"releasedIndexCount\":")
                .Append(snapshot.ReleasedIndexCount)
                .Append(",\"entityCount\":")
                .Append(entityCount)
                .Append(",\"partitionCount\":")
                .Append(partitionCount)
                .Append('}');
            return builder.ToString();
        }

        /// <summary>追加一个索引的稳定诊断字段。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="index">索引诊断摘要。</param>
        private static void AppendIndex(
            StringBuilder builder,
            SpatialIndexDiagnosticsSnapshot index,
            SpatialDensitySnapshot density)
        {
            builder.Append("{\"diagnosticsId\":\"")
                .Append(JsonHelper.EscapeString(index.DiagnosticsId))
                .Append("\",\"indexKind\":\"")
                .Append(JsonHelper.EscapeString(index.IndexKind))
                .Append("\",\"entityTypeName\":\"")
                .Append(JsonHelper.EscapeString(index.EntityTypeName))
                .Append("\",\"count\":")
                .Append(index.Count)
                .Append(",\"plane\":\"")
                .Append(JsonHelper.EscapeString(index.PlaneName))
                .Append("\",\"cellSize\":")
                .Append(index.HasCellSize ? FormatFloat(index.CellSize) : "0")
                .Append(",\"maxDepth\":")
                .Append(index.MaxDepth)
                .Append(",\"maxEntitiesPerNode\":")
                .Append(index.MaxEntitiesPerNode)
                .Append(",\"partitionCount\":")
                .Append(index.PartitionCount)
                .Append(",\"createdAtUtc\":\"")
                .Append(JsonHelper.EscapeString(index.CreatedAtUtc))
                .Append("\",\"bounds2D\":");
            AppendBounds2D(builder, index);
            builder.Append(",\"bounds3D\":");
            AppendBounds3D(builder, index);
            builder.Append(",\"density\":");
            if (density == null)
            {
                builder.Append("null");
            }
            else
            {
                AppendDensity(builder, density, true);
            }
            builder.Append('}');
        }

        /// <summary>追加密度统计和固定大小 bin 数组。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="density">密度快照。</param>
        /// <param name="includeBins">是否包含完整 bin 数组。</param>
        private static void AppendDensity(StringBuilder builder, SpatialDensitySnapshot density, bool includeBins)
        {
            builder.Append("{\"diagnosticsId\":\"")
                .Append(JsonHelper.EscapeString(density.DiagnosticsId))
                .Append("\",\"indexKind\":\"")
                .Append(JsonHelper.EscapeString(density.IndexKind))
                .Append("\",\"plane\":\"")
                .Append(density.Plane.ToString())
                .Append("\",\"resolution\":")
                .Append(density.Resolution)
                .Append(",\"minA\":")
                .Append(FormatFloat(density.MinA))
                .Append(",\"minB\":")
                .Append(FormatFloat(density.MinB))
                .Append(",\"maxA\":")
                .Append(FormatFloat(density.MaxA))
                .Append(",\"maxB\":")
                .Append(FormatFloat(density.MaxB))
                .Append(",\"totalBins\":")
                .Append(density.TotalBins)
                .Append(",\"occupiedBins\":")
                .Append(density.OccupiedBins)
                .Append(",\"minCount\":")
                .Append(density.MinCount)
                .Append(",\"meanCount\":")
                .Append(density.MeanCount)
                .Append(",\"p95Count\":")
                .Append(density.P95Count)
                .Append(",\"maxCount\":")
                .Append(density.MaxCount)
                .Append(",\"hotspots\":[");
            for (int index = 0; index < density.Hotspots.Count; index++)
            {
                if (index > 0) builder.Append(',');
                SpatialDensityHotspot hotspot = density.Hotspots[index];
                builder.Append("{\"x\":").Append(hotspot.X)
                    .Append(",\"y\":").Append(hotspot.Y)
                    .Append(",\"count\":").Append(hotspot.Count).Append('}');
            }

            builder.Append(']');
            if (includeBins)
            {
                builder.Append(",\"bins\":[");
                for (int index = 0; index < density.Counts.Length; index++)
                {
                    if (index > 0) builder.Append(',');
                    builder.Append(density.Counts[index]);
                }

                builder.Append(']');
            }

            builder.Append('}');
        }

        /// <summary>按诊断编号查找密度快照。</summary>
        private static SpatialDensitySnapshot FindDensity(
            IReadOnlyList<SpatialDensitySnapshot> densities,
            string diagnosticsId)
        {
            for (int index = 0; index < densities.Count; index++)
            {
                if (string.Equals(densities[index].DiagnosticsId, diagnosticsId, StringComparison.Ordinal))
                {
                    return densities[index];
                }
            }

            return null;
        }

        /// <summary>验证 density 请求并限制分辨率，防止生成过大 payload。</summary>
        private static void ReadDensityRequest(string payloadJson, out string diagnosticsId, out int resolution)
        {
            diagnosticsId = string.Empty;
            resolution = 32;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return;
            }

            diagnosticsId = JsonHelper.ExtractString(payloadJson, "diagnosticsId") ?? string.Empty;
            if (JsonHelper.TryExtractInt(payloadJson, "resolution", out int value))
            {
                resolution = value;
            }

            resolution = Math.Max(4, Math.Min(64, resolution));
        }

        /// <summary>追加可选二维边界。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="index">索引诊断摘要。</param>
        private static void AppendBounds2D(StringBuilder builder, SpatialIndexDiagnosticsSnapshot index)
        {
            if (!index.HasBounds2D)
            {
                builder.Append("null");
                return;
            }

            YokiRect bounds = index.Bounds2D;
            builder.Append("{\"x\":")
                .Append(FormatFloat(bounds.X))
                .Append(",\"y\":")
                .Append(FormatFloat(bounds.Y))
                .Append(",\"width\":")
                .Append(FormatFloat(bounds.Width))
                .Append(",\"height\":")
                .Append(FormatFloat(bounds.Height))
                .Append('}');
        }

        /// <summary>追加可选三维边界。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="index">索引诊断摘要。</param>
        private static void AppendBounds3D(StringBuilder builder, SpatialIndexDiagnosticsSnapshot index)
        {
            if (!index.HasBounds3D)
            {
                builder.Append("null");
                return;
            }

            YokiBounds bounds = index.Bounds3D;
            builder.Append("{\"center\":");
            AppendVector3(builder, bounds.Center);
            builder.Append(",\"size\":");
            AppendVector3(builder, bounds.Size);
            builder.Append('}');
        }

        /// <summary>追加三维向量 JSON。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="value">待写入向量。</param>
        private static void AppendVector3(StringBuilder builder, YokiVector3 value)
        {
            builder.Append("{\"x\":")
                .Append(FormatFloat(value.X))
                .Append(",\"y\":")
                .Append(FormatFloat(value.Y))
                .Append(",\"z\":")
                .Append(FormatFloat(value.Z))
                .Append('}');
        }

        /// <summary>使用 invariant culture 格式化有限浮点数。</summary>
        /// <param name="value">待格式化浮点数。</param>
        /// <returns>合法 JSON 数字文本。</returns>
        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif
