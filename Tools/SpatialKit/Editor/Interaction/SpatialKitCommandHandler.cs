#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>提供 SpatialKit 只读统计、索引列表和 Workbench 快照命令。</summary>
    internal sealed class SpatialKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT = "SpatialKit";
        private const string STATS = "stats";
        private const string LIST_INDEXES = "list_indexes";
        private const string DENSITY = "density";
        private const string ANALYZE = "analyze";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private static readonly string[] sSupportedActions =
        {
            STATS,
            LIST_INDEXES,
            DENSITY,
            ANALYZE,
            GET_WORKBENCH_SNAPSHOT
        };

        /// <summary>创建只读 SpatialKit 命令处理器。</summary>
        internal SpatialKitCommandHandler() : base(KIT, sSupportedActions)
        {
        }

        /// <summary>创建当前 SpatialKit 的 state snapshot。</summary>
        /// <returns>固定 schema 的 SpatialKit JSON。</returns>
        internal string CreateWorkbenchSnapshot()
        {
            return SpatialKitSnapshotWriter.WriteWorkbench();
        }

        /// <summary>执行匹配的 SpatialKit 命令并返回终态结果。</summary>
        /// <param name="request">已通过 Kit/action 匹配的请求。</param>
        /// <returns>成功或失败的命令终态。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                if (request.Action == STATS)
                {
                    return YokiFrameCommandResult.Success(SpatialKitSnapshotWriter.WriteStats());
                }

                if (request.Action == LIST_INDEXES)
                {
                    return YokiFrameCommandResult.Success(SpatialKitSnapshotWriter.WriteIndexes());
                }

                if (request.Action == DENSITY)
                {
                    return YokiFrameCommandResult.Success(SpatialKitSnapshotWriter.WriteDensity(request.PayloadJson));
                }

                if (request.Action == ANALYZE)
                {
                    return YokiFrameCommandResult.Success(SpatialKitSnapshotWriter.WriteAnalysis());
                }

                return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("SpatialKitCommandFailed", exception.Message);
            }
        }
    }
}
#endif
