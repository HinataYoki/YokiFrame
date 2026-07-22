#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>提供 ActionKit 只读状态和显式堆栈诊断控制。</summary>
    internal sealed class ActionKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT = "ActionKit";
        private const string STATS = "stats";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private const string SET_STACK_TRACE = "set_stack_trace";
        private const string CLEAR_STACK_TRACE = "clear_stack_trace";
        private static readonly string[] sSupportedActions =
        {
            STATS,
            GET_WORKBENCH_SNAPSHOT,
            SET_STACK_TRACE,
            CLEAR_STACK_TRACE
        };

        /// <summary>创建支持两个只读命令和两个显式诊断命令的 handler。</summary>
        internal ActionKitCommandHandler() : base(KIT, sSupportedActions) { }

        /// <summary>创建当前 ActionKit 的有界 Workbench 状态。</summary>
        /// <returns>固定 schema 的完整 ActionKit JSON。</returns>
        internal string CreateWorkbenchSnapshot()
        {
            return ActionKitSnapshotWriter.WriteWorkbench();
        }

        /// <summary>执行匹配命令，并把输入错误转换为 terminal response。</summary>
        /// <param name="request">已经通过通用策略的 ActionKit 请求。</param>
        /// <returns>命令终态结果。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                if (request.Action == STATS)
                {
                    return YokiFrameCommandResult.Success(ActionKitSnapshotWriter.WriteStats());
                }

                if (request.Action == SET_STACK_TRACE)
                {
                    return SetStackTrace(request.PayloadJson);
                }

                if (request.Action == CLEAR_STACK_TRACE)
                {
                    ActionStackTraceService.Clear();
                }

                return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("ActionKitCommandFailed", exception.Message);
            }
        }

        /// <summary>校验 enabled 布尔值并切换后续根 Action 的堆栈捕获。</summary>
        /// <param name="payloadJson">必须只包含 enabled 布尔值的扁平 JSON 对象。</param>
        /// <returns>应用设置后的完整新状态。</returns>
        private YokiFrameCommandResult SetStackTrace(string payloadJson)
        {
            if (!TryParseStackTracePayload(payloadJson, out bool enabled))
            {
                throw new ArgumentException(
                    "ActionKit set_stack_trace requires exactly one enabled JSON boolean.");
            }

            ActionStackTraceService.Enabled = enabled;
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }

        /// <summary>严格解析只含 enabled 布尔值的顶层 JSON 对象。</summary>
        /// <param name="payloadJson">待校验的完整 JSON。</param>
        /// <param name="enabled">成功时返回堆栈开关值。</param>
        /// <returns>对象、字段、类型和尾部内容全部有效时返回 true。</returns>
        private static bool TryParseStackTracePayload(string payloadJson, out bool enabled)
        {
            enabled = false;
            if (string.IsNullOrWhiteSpace(payloadJson)) return false;
            var index = 0;
            SkipWhitespace(payloadJson, ref index);
            if (!TryConsume(payloadJson, ref index, '{')) return false;
            SkipWhitespace(payloadJson, ref index);
            if (!TryConsumeLiteral(payloadJson, ref index, "\"enabled\"")) return false;
            SkipWhitespace(payloadJson, ref index);
            if (!TryConsume(payloadJson, ref index, ':')) return false;
            SkipWhitespace(payloadJson, ref index);
            if (!TryReadBoolean(payloadJson, ref index, out enabled)) return false;
            SkipWhitespace(payloadJson, ref index);
            if (!TryConsume(payloadJson, ref index, '}')) return false;
            SkipWhitespace(payloadJson, ref index);
            return index == payloadJson.Length;
        }

        /// <summary>读取标准 JSON true 或 false，拒绝字符串布尔值。</summary>
        private static bool TryReadBoolean(string json, ref int index, out bool value)
        {
            if (TryConsumeLiteral(json, ref index, "true")) { value = true; return true; }
            if (TryConsumeLiteral(json, ref index, "false")) { value = false; return true; }
            value = false;
            return false;
        }

        /// <summary>在当前位置消费固定 JSON 文本。</summary>
        private static bool TryConsumeLiteral(string json, ref int index, string expected)
        {
            if (index + expected.Length > json.Length) return false;
            if (string.CompareOrdinal(json, index, expected, 0, expected.Length) != 0) return false;
            index += expected.Length;
            return true;
        }

        /// <summary>在当前位置消费指定 JSON 结构字符。</summary>
        private static bool TryConsume(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected) return false;
            index++;
            return true;
        }

        /// <summary>跳过 JSON 标准允许的四种空白字符。</summary>
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char current = json[index];
                if (current != ' ' && current != '\t' && current != '\r' && current != '\n') return;
                index++;
            }
        }
    }
}
#endif
