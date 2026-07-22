#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Architecture 注册表的只读列表和 Workbench 聚合快照命令。
    /// </summary>
    public sealed class ArchitectureCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT_NAME = "Architecture";
        private static readonly string[] sSupportedActions =
        {
            "list_architectures",
            "get_workbench_snapshot"
        };

        /// <summary>创建只处理 Architecture 只读诊断命令的 handler。</summary>
        public ArchitectureCommandHandler() : base(KIT_NAME, sSupportedActions) { }

        /// <summary>
        /// 执行 Architecture action，并把注册表副本写成稳定 JSON。
        /// </summary>
        /// <param name="action">只读 action。</param>
        /// <returns>Architecture 工作台 payload。</returns>
        public string HandleAction(string action)
        {
            if (!string.Equals(action, "list_architectures", StringComparison.Ordinal)
                && !string.Equals(action, "get_workbench_snapshot", StringComparison.Ordinal))
            {
                throw new NotSupportedException("Unknown Architecture action '" + action + "'.");
            }

            var architectures = new List<ArchitectureDebugInfo>();
            ArchitectureRegistry.GetAll(architectures);
            return ArchitectureJsonWriter.WriteWorkbench(
                architectures,
                ArchitectureRegistry.DiagnosticVersion);
        }

        /// <summary>
        /// 执行新版 Runtime CommandBridge 请求，并把异常转换为 terminal error。
        /// </summary>
        /// <param name="request">已通过 handler 匹配的请求。</param>
        /// <returns>终态命令结果。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                return YokiFrameCommandResult.Success(HandleAction(request.Action));
            }
            catch (NotSupportedException exception)
            {
                return YokiFrameCommandResult.Error("UnknownCommand", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("ArchitectureQueryFailed", exception.Message);
            }
        }
    }
}
#endif
