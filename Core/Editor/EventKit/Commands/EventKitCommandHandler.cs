#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 EventKit 唯一的只读 Workbench 快照命令，不暴露扫描、触发或监控控制能力。
    /// </summary>
    public sealed class EventKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT_NAME = "EventKit";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private static readonly string[] sSupportedActions = { GET_WORKBENCH_SNAPSHOT };

        /// <summary>创建只处理 EventKit 只读快照命令的 handler。</summary>
        public EventKitCommandHandler() : base(KIT_NAME, sSupportedActions) { }

        /// <summary>创建当前 EventKit Runtime 的稳定 Workbench JSON。</summary>
        /// <returns>包含注册、监听器数量和有界活动历史的 payload。</returns>
        public string CreateWorkbenchSnapshot()
        {
            return EventKitJsonWriter.WriteWorkbench(EventKitSnapshotBuilder.Create());
        }

        /// <summary>执行已匹配的只读命令，并把异常转换为 terminal error。</summary>
        /// <param name="request">已通过 Kit/action 匹配的请求。</param>
        /// <returns>成功快照或终态错误。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("EventKitQueryFailed", exception.Message);
            }
        }
    }
}
#endif
