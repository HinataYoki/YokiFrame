using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// SpatialKit 命令桥处理器。
    /// 只输出空间索引诊断快照，不通过文件桥插入、移动或删除运行时实体，避免把高频空间查询变成跨进程控制流。
    /// </summary>
    public sealed class SpatialKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        /// <inheritdoc />
        public string KitName
        {
            get { return "SpatialKit"; }
        }

        /// <inheritdoc />
        public string[] SupportedActions
        {
            get
            {
                return new[]
                {
                    "stats",
                    "list_indexes",
                    "get_workbench_snapshot"
                };
            }
        }

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            return BuildStatsJson(SpatialKit.CreateDiagnosticsSnapshot());
        }

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return BuildStatsJson(SpatialKit.CreateDiagnosticsSnapshot());
                case "list_indexes":
                    return BuildIndexesJson(SpatialKit.CreateDiagnosticsSnapshot().Indexes);
                case "get_workbench_snapshot":
                    return BuildWorkbenchSnapshotJson();
                default:
                    throw new NotSupportedException("Unknown SpatialKit action '" + action + "'");
            }
        }

        private static string BuildWorkbenchSnapshotJson()
        {
            SpatialKitDiagnosticsSnapshot snapshot = SpatialKit.CreateDiagnosticsSnapshot();
            string stats = BuildStatsJson(snapshot);
            string indexes = BuildIndexesJson(snapshot.Indexes);

            var sb = new StringBuilder(stats.Length + indexes.Length + 48);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"indexes\":");
            sb.Append(indexes);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStatsJson(SpatialKitDiagnosticsSnapshot snapshot)
        {
            int hashGridCount = 0;
            int quadtreeCount = 0;
            int octreeCount = 0;
            int entityCount = 0;
            int partitionCount = 0;

            for (int i = 0; i < snapshot.Indexes.Count; i++)
            {
                SpatialIndexDiagnosticsSnapshot index = snapshot.Indexes[i];
                entityCount += index.Count;
                partitionCount += index.PartitionCount;

                if (string.Equals(index.IndexKind, "HashGrid", StringComparison.Ordinal))
                    hashGridCount++;
                else if (string.Equals(index.IndexKind, "Quadtree", StringComparison.Ordinal))
                    quadtreeCount++;
                else if (string.Equals(index.IndexKind, "Octree", StringComparison.Ordinal))
                    octreeCount++;
            }

            var sb = new StringBuilder(192);
            sb.Append("{\"activeIndexCount\":");
            sb.Append(snapshot.Indexes.Count);
            sb.Append(",\"totalCreatedIndexCount\":");
            sb.Append(snapshot.TotalCreatedIndexCount);
            sb.Append(",\"releasedIndexCount\":");
            sb.Append(snapshot.ReleasedIndexCount);
            sb.Append(",\"entityCount\":");
            sb.Append(entityCount);
            sb.Append(",\"partitionCount\":");
            sb.Append(partitionCount);
            sb.Append(",\"hashGridCount\":");
            sb.Append(hashGridCount);
            sb.Append(",\"quadtreeCount\":");
            sb.Append(quadtreeCount);
            sb.Append(",\"octreeCount\":");
            sb.Append(octreeCount);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildIndexesJson(List<SpatialIndexDiagnosticsSnapshot> indexes)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"indexes\":[");
            for (int i = 0; i < indexes.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendIndex(sb, indexes[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(indexes.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendIndex(StringBuilder sb, SpatialIndexDiagnosticsSnapshot index)
        {
            sb.Append("{\"diagnosticsId\":\"");
            sb.Append(JsonHelper.EscapeString(index.DiagnosticsId));
            sb.Append("\",\"indexKind\":\"");
            sb.Append(JsonHelper.EscapeString(index.IndexKind));
            sb.Append("\",\"entityTypeName\":\"");
            sb.Append(JsonHelper.EscapeString(index.EntityTypeName));
            sb.Append("\",\"count\":");
            sb.Append(index.Count);
            sb.Append(",\"plane\":\"");
            sb.Append(JsonHelper.EscapeString(index.PlaneName));
            sb.Append("\",\"cellSize\":");
            AppendFloat(sb, index.HasCellSize ? index.CellSize : 0f);
            sb.Append(",\"maxDepth\":");
            sb.Append(index.MaxDepth);
            sb.Append(",\"maxEntitiesPerNode\":");
            sb.Append(index.MaxEntitiesPerNode);
            sb.Append(",\"partitionCount\":");
            sb.Append(index.PartitionCount);
            sb.Append(",\"createdAtUtc\":\"");
            sb.Append(JsonHelper.EscapeString(index.CreatedAtUtc));
            sb.Append("\",\"bounds2D\":");
            AppendBounds2D(sb, index);
            sb.Append(",\"bounds3D\":");
            AppendBounds3D(sb, index);
            sb.Append('}');
        }

        private static void AppendBounds2D(StringBuilder sb, SpatialIndexDiagnosticsSnapshot index)
        {
            if (!index.HasBounds2D)
            {
                sb.Append("null");
                return;
            }

            YokiRect rect = index.Bounds2D;
            sb.Append("{\"x\":");
            AppendFloat(sb, rect.X);
            sb.Append(",\"y\":");
            AppendFloat(sb, rect.Y);
            sb.Append(",\"width\":");
            AppendFloat(sb, rect.Width);
            sb.Append(",\"height\":");
            AppendFloat(sb, rect.Height);
            sb.Append('}');
        }

        private static void AppendBounds3D(StringBuilder sb, SpatialIndexDiagnosticsSnapshot index)
        {
            if (!index.HasBounds3D)
            {
                sb.Append("null");
                return;
            }

            YokiBounds bounds = index.Bounds3D;
            sb.Append("{\"center\":");
            AppendVector3(sb, bounds.Center);
            sb.Append(",\"size\":");
            AppendVector3(sb, bounds.Size);
            sb.Append('}');
        }

        private static void AppendVector3(StringBuilder sb, YokiVector3 value)
        {
            sb.Append("{\"x\":");
            AppendFloat(sb, value.X);
            sb.Append(",\"y\":");
            AppendFloat(sb, value.Y);
            sb.Append(",\"z\":");
            AppendFloat(sb, value.Z);
            sb.Append('}');
        }

        private static void AppendFloat(StringBuilder sb, float value)
        {
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
