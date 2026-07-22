#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>提供 PoolKit Workbench 状态、跟踪配置、泄漏检查和历史清理。</summary>
    public sealed class PoolKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT_NAME = "PoolKit";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private const string CHECK_LEAK = "check_leak";
        private const string SET_TRACKING = "set_tracking";
        private const string CLEAR_HISTORY = "clear_history";
        private static readonly string[] sSupportedActions =
        {
            GET_WORKBENCH_SNAPSHOT,
            CHECK_LEAK,
            SET_TRACKING,
            CLEAR_HISTORY
        };

        /// <summary>创建支持 PoolKit 四个正式 Workbench action 的 handler。</summary>
        public PoolKitCommandHandler() : base(KIT_NAME, sSupportedActions) { }

        /// <summary>创建当前 PoolKit 的固定有界 state。</summary>
        /// <returns>适合 Snapshot 和 Shared Memory 的 JSON。</returns>
        public string CreateWorkbenchSnapshot()
        {
            return PoolKitJsonWriter.WriteWorkbench(PoolKitSnapshotBuilder.Create());
        }

        /// <summary>执行匹配 action，并把 payload 错误转换为 terminal error。</summary>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                if (request.Action == SET_TRACKING) return SetTracking(request.PayloadJson);
                if (request.Action == CLEAR_HISTORY) return ClearHistory();
                return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("PoolKitCommandFailed", exception.Message);
            }
        }

        /// <summary>原子校验三个诊断开关语义后应用当前会话。</summary>
        private YokiFrameCommandResult SetTracking(string payloadJson)
        {
            if (!JsonHelper.TryExtractBool(payloadJson, "trackingEnabled", out var trackingEnabled)
                || !JsonHelper.TryExtractBool(payloadJson, "eventHistoryEnabled", out var eventHistoryEnabled)
                || !JsonHelper.TryExtractBool(payloadJson, "stackTraceEnabled", out var stackTraceEnabled))
            {
                throw new ArgumentException(
                    "PoolKit set_tracking requires trackingEnabled, eventHistoryEnabled and stackTraceEnabled booleans.");
            }

            PoolDebugger.Configure(trackingEnabled, eventHistoryEnabled, stackTraceEnabled);
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }

        /// <summary>清空有界事件历史并返回完整新 state。</summary>
        private YokiFrameCommandResult ClearHistory()
        {
            PoolDebugger.ClearEventHistory();
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }
    }
}
#endif
