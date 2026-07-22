#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace YokiFrame
{
    /// <summary>按需创建有界 ActionKit 状态，不在调度 Tick 热路径分配。</summary>
    internal static partial class ActionKitSnapshotWriter
    {
        private const int MAX_ROOTS = 64;
        private const int MAX_NODES = 256;
        private const int MAX_DEPTH = 16;
        private const int MAX_STACK_ROOTS = 4;
        private const int MAX_STACK_FRAMES = 24;
        private const int MIN_REDUCED_NODES = 16;
        private const int MAX_TYPE_LENGTH = 128;
        private const int MAX_DEBUG_LENGTH = 240;
        private const int COMPACT_ROOTS = 16;
        private const int COMPACT_EVENTS = 16;

        /// <summary>创建只包含累计指标和诊断开关的轻量结果。</summary>
        /// <returns>固定 schema 的 stats JSON。</returns>
        internal static string WriteStats()
        {
            var builder = new StringBuilder(320);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(ActionKitScheduler.DiagnosticVersion);
            AppendStats(builder, ActionKitScheduler.ExecutingCount);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>按本轮降级预算创建完整且语法有效的 ActionKit state。</summary>
        /// <param name="snapshotVersion">本次快照对应的稳定诊断版本。</param>
        /// <param name="controllers">本轮稳定根 controller 快照。</param>
        /// <param name="events">本轮最新优先终态快照。</param>
        /// <param name="nodeLimit">允许写入的动作节点数量。</param>
        /// <param name="stackFrameLimit">每个已采集根允许写入的调用帧数量。</param>
        /// <param name="eventLimit">允许写入的终态数量。</param>
        /// <returns>保留总量和裁剪事实的完整 JSON。</returns>
        private static string BuildWorkbench(
            long snapshotVersion,
            IReadOnlyList<IActionController> controllers,
            ActionKitTerminalEvent[] events,
            int nodeLimit,
            int stackFrameLimit,
            int eventLimit)
        {
            var builder = new StringBuilder(8192);
            WriteContext context = new();
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(snapshotVersion);
            AppendStats(builder, controllers.Count);
            AppendRoots(builder, controllers, nodeLimit, stackFrameLimit, ref context);
            AppendEvents(builder, events, eventLimit);
            builder.Append(",\"nodesTruncated\":").Append(ToJson(context.NodesTruncated));
            builder.Append(",\"depthTruncated\":").Append(ToJson(context.DepthTruncated));
            builder.Append(",\"stackTruncated\":").Append(ToJson(context.StackTruncated));
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>超出共享内存预算时重建紧凑 schema，保留摘要并省略高成本诊断文本。</summary>
        /// <param name="snapshotVersion">本次快照对应的稳定诊断版本。</param>
        /// <param name="controllers">当前全部活动根。</param>
        /// <returns>确定低于 64 KiB 的兼容状态 JSON。</returns>
        private static string BuildCompactWorkbench(
            long snapshotVersion,
            IReadOnlyList<IActionController> controllers)
        {
            var builder = new StringBuilder(12288);
            builder.Append("{\"schemaVersion\":1,\"version\":")
                .Append(snapshotVersion);
            AppendStats(builder, controllers.Count);
            bool childrenOmitted = AppendCompactRoots(builder, controllers);
            AppendCompactEvents(builder);
            builder.Append(",\"nodesTruncated\":")
                .Append(ToJson(childrenOmitted || controllers.Count > COMPACT_ROOTS));
            builder.Append(",\"depthTruncated\":false");
            builder.Append(",\"stackTruncated\":")
                .Append(ToJson(ActionStackTraceService.Count > 0));
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>追加有限根摘要，并报告是否省略了任何子节点。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="controllers">当前全部活动根。</param>
        /// <returns>任一保留根存在子节点时返回 true。</returns>
        private static bool AppendCompactRoots(
            StringBuilder builder,
            IReadOnlyList<IActionController> controllers)
        {
            int rootCount = Math.Min(controllers.Count, COMPACT_ROOTS);
            bool childrenOmitted = false;
            builder.Append(",\"roots\":[");
            for (var index = 0; index < rootCount; index++)
            {
                if (index > 0) builder.Append(',');
                IAction action = controllers[index].Action;
                childrenOmitted |= action is IActionContainerInternal container && container.ChildCount > 0;
                AppendCompactRoot(builder, controllers[index], action);
            }

            builder.Append("],\"rootCount\":").Append(rootCount);
            builder.Append(",\"rootTotal\":").Append(controllers.Count);
            builder.Append(",\"rootsTruncated\":").Append(ToJson(controllers.Count > rootCount));
            builder.Append(",\"nodeCount\":").Append(rootCount);
            return childrenOmitted;
        }

        /// <summary>追加不含 debug、stack 和 children 内容的单个根摘要。</summary>
        private static void AppendCompactRoot(
            StringBuilder builder,
            IActionController controller,
            IAction action)
        {
            builder.Append("{\"actionId\":\"")
                .Append(controller.CurExecuteActionID.ToString(CultureInfo.InvariantCulture)).Append('"');
            builder.Append(",\"type\":");
            AppendString(builder, action == null ? "Released" : action.GetType().Name, MAX_TYPE_LENGTH);
            builder.Append(",\"status\":");
            AppendString(builder, action == null ? "Finished" : action.ActionState.ToString(), MAX_TYPE_LENGTH);
            builder.Append(",\"paused\":").Append(ToJson(action != null && action.Paused));
            builder.Append(",\"deinited\":").Append(ToJson(action == null || action.Deinited));
            builder.Append(",\"debugInfo\":\"\",\"updateMode\":");
            AppendString(builder, controller.UpdateMode.ToString(), MAX_TYPE_LENGTH);
            builder.Append(",\"cancelRequested\":").Append(ToJson(controller.IsCancelled));
            builder.Append(",\"stackTrace\":[],\"children\":[]}");
        }

        /// <summary>追加最新十六条终态摘要，省略可导致 payload 膨胀的错误文本。</summary>
        private static void AppendCompactEvents(StringBuilder builder)
        {
            ActionKitTerminalEvent[] events = ActionKitDiagnosticHistory.CreateLatestSnapshot();
            int eventCount = Math.Min(events.Length, COMPACT_EVENTS);
            builder.Append(",\"events\":[");
            for (var index = 0; index < eventCount; index++)
            {
                if (index > 0) builder.Append(',');
                ActionKitTerminalEvent terminalEvent = events[index];
                builder.Append("{\"actionId\":\"")
                    .Append(terminalEvent.ActionId.ToString(CultureInfo.InvariantCulture)).Append('"');
                builder.Append(",\"actionType\":");
                AppendString(builder, terminalEvent.ActionType, MAX_TYPE_LENGTH);
                builder.Append(",\"outcome\":");
                AppendString(builder, terminalEvent.Outcome.ToString(), MAX_TYPE_LENGTH);
                builder.Append(",\"frame\":").Append(terminalEvent.Frame);
                builder.Append(",\"errorMessage\":\"\"}");
            }

            builder.Append("],\"eventCount\":").Append(eventCount);
            builder.Append(",\"eventTotal\":").Append(ActionKitDiagnosticHistory.TotalCount);
            builder.Append(",\"eventsTruncated\":")
                .Append(ToJson(ActionKitDiagnosticHistory.TotalCount > eventCount));
        }

        /// <summary>追加累计指标和诊断开关。</summary>
        /// <param name="builder">目标 JSON builder。</param>
        /// <param name="activeRootCount">当前活动根总量。</param>
        private static void AppendStats(StringBuilder builder, int activeRootCount)
        {
            builder.Append(",\"stats\":{\"frameCount\":").Append(ActionKitScheduler.FrameCount);
            builder.Append(",\"activeRootCount\":").Append(activeRootCount);
            builder.Append(",\"finishedCount\":").Append(ActionKitScheduler.FinishedCount);
            builder.Append(",\"cancelledCount\":").Append(ActionKitScheduler.CancelledCount);
            builder.Append(",\"faultedCount\":").Append(ActionKitScheduler.FaultedCount);
            builder.Append(",\"terminalEventCount\":").Append(ActionKitDiagnosticHistory.TotalCount);
            builder.Append(",\"stackTraceEnabled\":").Append(ToJson(ActionStackTraceService.Enabled));
            builder.Append(",\"stackTraceCount\":").Append(ActionStackTraceService.Count).Append('}');
        }

        /// <summary>追加有界活动根列表。</summary>
        private static void AppendRoots(
            StringBuilder builder,
            IReadOnlyList<IActionController> controllers,
            int nodeLimit,
            int stackFrameLimit,
            ref WriteContext context)
        {
            int rootLimit = Math.Min(controllers.Count, MAX_ROOTS);
            var rootCount = 0;
            builder.Append(",\"roots\":[");
            for (var index = 0; index < rootLimit && context.NodesWritten < nodeLimit; index++)
            {
                if (rootCount > 0)
                {
                    builder.Append(',');
                }

                AppendRoot(builder, controllers[index], nodeLimit, stackFrameLimit, ref context);
                rootCount++;
            }

            if (rootCount < rootLimit) context.NodesTruncated = true;
            builder.Append("],\"rootCount\":").Append(rootCount);
            builder.Append(",\"rootTotal\":").Append(controllers.Count);
            builder.Append(",\"rootsTruncated\":").Append(ToJson(controllers.Count > rootCount));
            builder.Append(",\"nodeCount\":").Append(context.NodesWritten);
        }

        /// <summary>追加一个根 controller 及其动作树。</summary>
        private static void AppendRoot(
            StringBuilder builder,
            IActionController controller,
            int nodeLimit,
            int stackFrameLimit,
            ref WriteContext context)
        {
            IAction action = controller.Action;
            if (action == null)
            {
                context.NodesWritten++;
                builder.Append("{\"actionId\":\"")
                    .Append(controller.CurExecuteActionID.ToString(CultureInfo.InvariantCulture))
                    .Append("\",\"type\":\"Released\",\"children\":[]}");
                return;
            }

            AppendNodeStart(builder, action);
            builder.Append(",\"updateMode\":");
            AppendString(builder, controller.UpdateMode.ToString(), MAX_TYPE_LENGTH);
            builder.Append(",\"cancelRequested\":").Append(ToJson(controller.IsCancelled));
            AppendStackTrace(builder, action.ActionID, stackFrameLimit, ref context);
            AppendChildren(builder, action, 0, nodeLimit, ref context);
            builder.Append('}');
        }

        /// <summary>追加一个动作节点的公共字段。</summary>
        private static void AppendNodeStart(StringBuilder builder, IAction action)
        {
            builder.Append("{\"actionId\":\"")
                .Append(action.ActionID.ToString(CultureInfo.InvariantCulture)).Append('"');
            builder.Append(",\"type\":");
            AppendString(builder, action.GetType().Name, MAX_TYPE_LENGTH);
            builder.Append(",\"status\":");
            AppendString(builder, action.ActionState.ToString(), MAX_TYPE_LENGTH);
            builder.Append(",\"paused\":").Append(ToJson(action.Paused));
            builder.Append(",\"deinited\":").Append(ToJson(action.Deinited));
            builder.Append(",\"debugInfo\":");
            AppendString(builder, GetDebugInfo(action), MAX_DEBUG_LENGTH);
        }

        /// <summary>递归追加直接子节点，并严格限制总节点数和深度。</summary>
        private static void AppendChildren(
            StringBuilder builder,
            IAction action,
            int depth,
            int nodeLimit,
            ref WriteContext context)
        {
            context.NodesWritten++;
            builder.Append(",\"children\":[");
            if (!(action is IActionContainerInternal container))
            {
                builder.Append(']');
                return;
            }

            if (depth >= MAX_DEPTH)
            {
                context.DepthTruncated |= container.ChildCount > 0;
                builder.Append(']');
                return;
            }

            var writtenChildren = 0;
            for (var index = 0; index < container.ChildCount; index++)
            {
                if (context.NodesWritten >= nodeLimit)
                {
                    context.NodesTruncated = true;
                    break;
                }

                if (writtenChildren++ > 0)
                {
                    builder.Append(',');
                }

                IAction child = container.GetChild(index);
                AppendNodeStart(builder, child);
                AppendChildren(builder, child, depth + 1, nodeLimit, ref context);
                builder.Append('}');
            }

            builder.Append(']');
        }

        /// <summary>追加最多四个根的有界调用堆栈，防止诊断 payload 超过共享内存预算。</summary>
        private static void AppendStackTrace(
            StringBuilder builder,
            ulong actionId,
            int stackFrameLimit,
            ref WriteContext context)
        {
            builder.Append(",\"stackTrace\":[");
            if (!ActionStackTraceService.TryGet(actionId, out StackTrace stackTrace))
            {
                builder.Append(']');
                return;
            }

            if (context.StackRootsWritten >= MAX_STACK_ROOTS)
            {
                context.StackTruncated = true;
                builder.Append(']');
                return;
            }

            context.StackRootsWritten++;
            if (stackFrameLimit <= 0)
            {
                context.StackTruncated = true;
                builder.Append(']');
                return;
            }

            StackFrame[] frames = stackTrace.GetFrames() ?? Array.Empty<StackFrame>();
            int frameCount = Math.Min(frames.Length, stackFrameLimit);
            for (var index = 0; index < frameCount; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendStackFrame(builder, frames[index]);
            }

            if (frames.Length > frameCount)
            {
                context.StackTruncated = true;
            }

            builder.Append(']');
        }

        /// <summary>追加不暴露绝对路径的单个调用帧。</summary>
        private static void AppendStackFrame(StringBuilder builder, StackFrame frame)
        {
            MethodBase method = frame.GetMethod();
            string methodName = method == null
                ? string.Empty
                : (method.DeclaringType?.FullName ?? string.Empty) + "." + method.Name;
            string fileName = frame.GetFileName();
            builder.Append("{\"method\":");
            AppendString(builder, methodName, MAX_DEBUG_LENGTH);
            builder.Append(",\"file\":");
            AppendString(builder, string.IsNullOrEmpty(fileName) ? string.Empty : Path.GetFileName(fileName), MAX_TYPE_LENGTH);
            builder.Append(",\"line\":").Append(frame.GetFileLineNumber()).Append('}');
        }

        /// <summary>追加最新优先的有界终态事件。</summary>
        private static void AppendEvents(
            StringBuilder builder,
            ActionKitTerminalEvent[] events,
            int eventLimit)
        {
            int eventCount = Math.Min(events.Length, Math.Max(0, eventLimit));
            builder.Append(",\"events\":[");
            for (var index = 0; index < eventCount; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                ActionKitTerminalEvent terminalEvent = events[index];
                builder.Append("{\"actionId\":\"")
                    .Append(terminalEvent.ActionId.ToString(CultureInfo.InvariantCulture)).Append('"');
                builder.Append(",\"actionType\":");
                AppendString(builder, terminalEvent.ActionType, MAX_TYPE_LENGTH);
                builder.Append(",\"outcome\":");
                AppendString(builder, terminalEvent.Outcome.ToString(), MAX_TYPE_LENGTH);
                builder.Append(",\"frame\":").Append(terminalEvent.Frame);
                builder.Append(",\"errorMessage\":");
                AppendString(
                    builder,
                    terminalEvent.ErrorMessage,
                    ActionKitDiagnosticHistory.MAX_ERROR_MESSAGE_LENGTH);
                builder.Append('}');
            }

            builder.Append("],\"eventCount\":").Append(eventCount);
            builder.Append(",\"eventTotal\":").Append(ActionKitDiagnosticHistory.TotalCount);
            builder.Append(",\"eventsTruncated\":")
                .Append(ToJson(ActionKitDiagnosticHistory.TotalCount > eventCount));
        }

        /// <summary>安全读取自定义诊断文本，异常时回落类型名。</summary>
        private static string GetDebugInfo(IAction action)
        {
            try
            {
                return action.GetDebugInfo() ?? string.Empty;
            }
            catch (Exception)
            {
                return action.GetType().Name;
            }
        }

        /// <summary>追加已裁剪并正确转义的 JSON 字符串。</summary>
        private static void AppendString(StringBuilder builder, string value, int maxLength)
        {
            string normalized = value ?? string.Empty;
            if (normalized.Length > maxLength)
            {
                int length = maxLength;
                if (length > 0
                    && char.IsHighSurrogate(normalized[length - 1])
                    && char.IsLowSurrogate(normalized[length]))
                    length--;
                normalized = normalized.Substring(0, length);
            }

            builder.Append('"').Append(JsonHelper.EscapeString(normalized)).Append('"');
        }

        /// <summary>返回 JSON 布尔字面量。</summary>
        private static string ToJson(bool value) => value ? "true" : "false";

        /// <summary>保存单次 JSON 写入的全局预算与裁剪事实。</summary>
        private struct WriteContext
        {
            internal int NodesWritten;
            internal int StackRootsWritten;
            internal bool NodesTruncated;
            internal bool DepthTruncated;
            internal bool StackTruncated;
        }
    }
}
#endif
