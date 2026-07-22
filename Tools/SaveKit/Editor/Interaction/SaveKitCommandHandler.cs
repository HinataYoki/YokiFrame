#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>提供 SaveKit 统计和完整 Workbench 状态的两个只读命令。</summary>
    internal sealed class SaveKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT = "SaveKit";
        private const string STATS = "stats";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private static readonly string[] sSupportedActions =
        {
            STATS,
            GET_WORKBENCH_SNAPSHOT
        };

        /// <summary>创建仅允许 SaveKit 查询 action 的命令处理器。</summary>
        internal SaveKitCommandHandler() : base(KIT, sSupportedActions)
        {
        }

        /// <summary>创建当前 SaveKit 的完整状态，且不会初始化默认后端。</summary>
        /// <returns>固定 schema 的 Workbench JSON。</returns>
        internal string CreateWorkbenchSnapshot()
        {
            return SaveKitSnapshotWriter.WriteWorkbench();
        }

        /// <summary>执行已经匹配的查询 action，并把后端诊断异常转换为 terminal response。</summary>
        /// <param name="request">已校验 Kit/action 的请求。</param>
        /// <returns>只读 JSON 或稳定错误结果。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                return request.Action == STATS
                    ? YokiFrameCommandResult.Success(SaveKitSnapshotWriter.WriteStats())
                    : YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("SaveKitCommandFailed", exception.Message);
            }
        }
    }
}
#endif
