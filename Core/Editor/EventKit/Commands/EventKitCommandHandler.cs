#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 EventKit Workbench 快照命令与跟踪开关，不暴露扫描或触发控制能力。
    /// </summary>
    public sealed class EventKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT_NAME = "EventKit";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private const string SET_TRACKING = "set_tracking";
        private static readonly string[] sSupportedActions = { GET_WORKBENCH_SNAPSHOT, SET_TRACKING };

        /// <summary>创建支持 EventKit 快照与跟踪开关的 handler。</summary>
        public EventKitCommandHandler() : base(KIT_NAME, sSupportedActions) { }

        /// <summary>创建当前 EventKit Runtime 的稳定 Workbench JSON。</summary>
        /// <returns>包含注册、监听器数量和有界活动历史的 payload。</returns>
        public string CreateWorkbenchSnapshot()
        {
            return EventKitJsonWriter.WriteWorkbench(EventKitSnapshotBuilder.Create());
        }

        /// <summary>执行已匹配命令，并把 payload 与运行异常转换为 terminal error。</summary>
        /// <param name="request">已通过 Kit/action 匹配的请求。</param>
        /// <returns>成功快照或终态错误。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                if (request.Action == SET_TRACKING) return SetTracking(request.PayloadJson);
                return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("EventKitQueryFailed", exception.Message);
            }
        }

        /// <summary>解析唯一布尔开关并应用到当前会话，随后返回完整新 state。</summary>
        /// <param name="payloadJson">命令 payload。</param>
        /// <returns>应用后的 Workbench 快照或 payload 错误。</returns>
        private YokiFrameCommandResult SetTracking(string payloadJson)
        {
            if (!JsonHelper.TryExtractBool(payloadJson, "trackingEnabled", out var trackingEnabled))
            {
                throw new ArgumentException("EventKit set_tracking requires a trackingEnabled boolean.");
            }

            EventKitDiagnosticRegistry.Configure(trackingEnabled);
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }
    }
}
#endif
