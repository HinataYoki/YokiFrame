#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Engine state snapshot 的写出器：domain_state 字段 + 能力矩阵 + 活动服务与运行摘要。
    /// </summary>
    /// <remarks>
    /// 为什么快照比 <c>domain_state</c> 更富：Workbench 只读快照文件，看不到命令返回值。
    /// 把能力矩阵与服务摘要并入快照，页面就能显示真实数据而不需要新增命令管线；
    /// <c>domain_state</c> 本身保持精简（高频只读命令不应携带矩阵）。
    /// 矩阵与 <c>engine_capabilities</c> 共用 <see cref="RoslynCapabilityProjection"/>，两处不会漂移。
    /// </remarks>
    internal static class RoslynSnapshotWriter
    {
        /// <summary>快照里最多列出的最近运行数量；超出时置 truncatedRuns。</summary>
        public const int MAX_RUNS = 10;

        /// <summary>写出 state snapshot。</summary>
        /// <param name="state">引擎状态。</param>
        /// <param name="settings">开关快照。</param>
        /// <param name="operations">操作集合。</param>
        /// <param name="hostTargets">宿主承载目标。</param>
        /// <param name="objects">只读对象目录。</param>
        /// <returns>snapshot JSON。</returns>
        public static string Write(
            RoslynDomainState state,
            RoslynSettingsSnapshot settings,
            IReadOnlyList<IRoslynOperation> operations,
            RoslynExecutionTarget hostTargets,
            RoslynObjectCatalog objects,
            IReadOnlyList<RoslynRunRecord> recentRuns = null)
        {
            RoslynJsonBuilder builder = RoslynJsonWriter.WriteDomainStateInto(
                new RoslynJsonBuilder(),
                state,
                settings);
            builder.Property("hostTargets", RoslynExecutionTargets.Format(hostTargets));
            RoslynJsonWriter.WriteCapabilityArray(
                builder,
                "capabilities",
                RoslynCapabilityProjection.Create(operations, settings, hostTargets));

            objects.WriteSnapshot(builder, state);
            AppendRecentRuns(builder, recentRuns);
            return builder.EndObject().ToString();
        }

        /// <summary>追加最近运行记录（按提交时间倒序，最多 MAX_RUNS 条）；无记录时不写该键。</summary>
        /// <param name="builder">目标 builder。</param>
        /// <param name="recentRuns">最近运行记录。</param>
        private static void AppendRecentRuns(
            RoslynJsonBuilder builder,
            IReadOnlyList<RoslynRunRecord> recentRuns)
        {
            if (recentRuns == null)
            {
                return;
            }

            int count = recentRuns.Count < MAX_RUNS ? recentRuns.Count : MAX_RUNS;
            builder.Property("recentRunCount", recentRuns.Count)
                .Property("truncatedRuns", recentRuns.Count > count)
                .Name("recentRuns")
                .StartArray();
            for (var index = 0; index < count; index++)
            {
                RoslynRunRecord run = recentRuns[index];
                builder.StartObject()
                    .Property("runId", run.RunId)
                    .Property("requestId", run.RequestId)
                    .Property("kind", run.Kind.Length == 0 ? "legacy" : run.Kind)
                    .Property("legacyEntry", run.LegacyEntry)
                    .Property("target", run.Target)
                    .Property("state", run.State.ToString())
                    .Property("errorCode", run.ErrorCode)
                    .Property("resultPath", run.ResultPath)
                    .Property("submittedAtUtc", run.SubmittedAtUtc.ToString("u"))
                    .Property("updatedAtUtc", run.UpdatedAtUtc.ToString("u"))
                    .EndObject();
            }

            builder.EndArray();
        }
    }
}
#endif
