using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 命令处理器：为 Tauri/AI 提供动作树快照和堆栈追踪开关。
    /// </summary>
    public sealed class ActionKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        private const int MAX_ROOTS = 128;
        private const int MAX_DEPTH = 16;
        private const int MAX_STACK_FRAMES = 24;

        private static readonly List<IActionController> sControllerBuffer = new List<IActionController>(64);

        public string KitName => "ActionKit";
        public string[] SupportedActions => new[] { "stats", "get_workbench_snapshot", "set_stack_trace", "clear_stack_trace" };

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            return GetStats();
        }

        /// <summary>
        /// 处理 ActionKit 命令桥请求。
        /// </summary>
        /// <param name="action">命令动作。</param>
        /// <param name="payloadJson">命令负载 JSON。</param>
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "set_stack_trace":
                    return SetStackTrace(payloadJson);
                case "clear_stack_trace":
                    ActionStackTraceService.Clear();
                    return "{\"cleared\":true,\"stackTraceCount\":0}";
                default:
                    throw new NotSupportedException("Unknown ActionKit action '" + action + "'");
            }
        }

        private static string GetWorkbenchSnapshot()
        {
            var stats = GetStats();
            var roots = BuildRootsJson();

            var sb = new StringBuilder(stats.Length + roots.Length + 48);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"roots\":");
            sb.Append(roots);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetStats()
        {
            ActionKitScheduler.GetExecutingActionControllers(sControllerBuffer);
            var activeCount = sControllerBuffer.Count;
            sControllerBuffer.Clear();

            var sb = new StringBuilder(192);
            sb.Append("{\"activeCount\":");
            sb.Append(activeCount);
            sb.Append(",\"finishedCount\":");
            sb.Append(ActionKitScheduler.FinishedCount);
            sb.Append(",\"cancelledCount\":");
            sb.Append(ActionKitScheduler.CancelledCount);
            sb.Append(",\"frameCount\":");
            sb.Append(ActionKitScheduler.FrameCount);
            sb.Append(",\"stackTraceEnabled\":");
            sb.Append(ActionStackTraceService.Enabled ? "true" : "false");
            sb.Append(",\"stackTraceCount\":");
            sb.Append(ActionStackTraceService.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SetStackTrace(string payloadJson)
        {
            ActionStackTraceService.Enabled = ExtractBool(payloadJson, "enabled", ActionStackTraceService.Enabled);
            return GetStats();
        }

        private static string BuildRootsJson()
        {
            ActionKitScheduler.GetExecutingActionControllers(sControllerBuffer);

            var sb = new StringBuilder(512);
            sb.Append('[');
            var count = 0;
            for (var i = 0; i < sControllerBuffer.Count && count < MAX_ROOTS; i++)
            {
                var controller = sControllerBuffer[i];
                if (controller == null || controller.Action == null)
                    continue;

                if (count > 0)
                    sb.Append(',');

                BuildActionNodeJson(sb, controller.Action, controller, 0);
                count++;
            }
            sb.Append(']');
            sControllerBuffer.Clear();
            return sb.ToString();
        }

        private static void BuildActionNodeJson(StringBuilder sb, IAction action, IActionController controller, int depth)
        {
            var typeName = GetActionTypeName(action);
            var childCount = GetChildCount(action);
            var currentChildIndex = GetCurrentChildIndex(action);

            sb.Append("{\"id\":\"");
            sb.Append(action.ActionID.ToString(CultureInfo.InvariantCulture));
            sb.Append("\",\"actionId\":");
            sb.Append(action.ActionID);
            sb.Append(",\"type\":\"");
            sb.Append(JsonHelper.EscapeString(typeName));
            sb.Append("\",\"debugInfo\":\"");
            sb.Append(JsonHelper.EscapeString(action.GetDebugInfo()));
            sb.Append("\",\"status\":\"");
            sb.Append(action.ActionState.ToString());
            sb.Append("\",\"paused\":");
            sb.Append(action.Paused ? "true" : "false");
            sb.Append(",\"deinited\":");
            sb.Append(action.Deinited ? "true" : "false");
            sb.Append(",\"executorName\":\"");
            sb.Append(JsonHelper.EscapeString(controller != null ? "PlayerLoop" : string.Empty));
            sb.Append("\",\"updateMode\":\"");
            sb.Append(JsonHelper.EscapeString(controller != null ? controller.UpdateMode.ToString() : string.Empty));
            sb.Append("\",\"childCount\":");
            sb.Append(childCount);
            sb.Append(",\"currentChildIndex\":");
            sb.Append(currentChildIndex);
            sb.Append(",\"stackTrace\":");
            AppendStackTraceJson(sb, action.ActionID);
            sb.Append(",\"children\":[");

            if (depth < MAX_DEPTH)
            {
                for (var i = 0; i < childCount; i++)
                {
                    var child = GetChild(action, i);
                    if (child == null)
                        continue;

                    if (i > 0)
                        sb.Append(',');

                    BuildActionNodeJson(sb, child, null, depth + 1);
                }
            }

            sb.Append("]}");
        }

        private static int GetChildCount(IAction action)
        {
            if (action is Sequence sequence)
                return sequence.EditorGetActions().Count;
            if (action is Parallel parallel)
                return parallel.EditorGetActions().Count;
            if (action is Repeat repeat)
            {
                var repeatSequence = repeat.EditorGetSequence();
                return repeatSequence != null ? repeatSequence.EditorGetActions().Count : 0;
            }

            return 0;
        }

        private static IAction GetChild(IAction action, int index)
        {
            if (index < 0)
                return null;

            if (action is Sequence sequence)
            {
                var actions = sequence.EditorGetActions();
                return index < actions.Count ? actions[index] : null;
            }

            if (action is Parallel parallel)
            {
                var actions = parallel.EditorGetActions();
                return index < actions.Count ? actions[index] : null;
            }

            if (action is Repeat repeat)
            {
                var repeatSequence = repeat.EditorGetSequence();
                if (repeatSequence == null)
                    return null;

                var actions = repeatSequence.EditorGetActions();
                return index < actions.Count ? actions[index] : null;
            }

            return null;
        }

        private static int GetCurrentChildIndex(IAction action)
        {
            if (action is Sequence sequence)
                return sequence.EditorGetCurrentIndex();
            if (action is Repeat repeat)
            {
                var repeatSequence = repeat.EditorGetSequence();
                return repeatSequence != null ? repeatSequence.EditorGetCurrentIndex() : -1;
            }

            return -1;
        }

        private static string GetActionTypeName(IAction action)
        {
            return action != null ? action.GetType().Name : "Unknown";
        }

        private static void AppendStackTraceJson(StringBuilder sb, ulong actionId)
        {
            if (!ActionStackTraceService.TryGet(actionId, out var stackTrace) || stackTrace == null)
            {
                sb.Append("null");
                return;
            }

            var frames = stackTrace.GetFrames();
            var text = new StringBuilder(256);
            var framesJson = new StringBuilder(256);
            var count = 0;

            framesJson.Append('[');
            if (frames != null)
            {
                for (var i = 0; i < frames.Length && count < MAX_STACK_FRAMES; i++)
                {
                    var frame = frames[i];
                    var method = frame.GetMethod();
                    if (!ShouldIncludeFrame(method))
                        continue;

                    var methodName = FormatMethodName(method);
                    var fileName = NormalizeFilePath(frame.GetFileName());
                    var line = frame.GetFileLineNumber();

                    if (text.Length > 0)
                        text.Append('\n');
                    text.Append("-> ");
                    text.Append(methodName);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        text.Append('\n');
                        text.Append("   ");
                        text.Append(fileName);
                        if (line > 0)
                        {
                            text.Append(':');
                            text.Append(line);
                        }
                    }

                    if (count > 0)
                        framesJson.Append(',');

                    framesJson.Append("{\"method\":\"");
                    framesJson.Append(JsonHelper.EscapeString(methodName));
                    framesJson.Append("\",\"filePath\":\"");
                    framesJson.Append(JsonHelper.EscapeString(fileName));
                    framesJson.Append("\",\"line\":");
                    framesJson.Append(line);
                    framesJson.Append('}');
                    count++;
                }
            }
            framesJson.Append(']');

            sb.Append("{\"text\":\"");
            sb.Append(JsonHelper.EscapeString(text.Length > 0 ? text.ToString() : "没有可显示的堆栈帧，可能已被过滤。"));
            sb.Append("\",\"frames\":");
            sb.Append(framesJson);
            sb.Append(",\"frameCount\":");
            sb.Append(count);
            sb.Append('}');
        }

        private static bool ShouldIncludeFrame(MethodBase method)
        {
            if (method == null || method.DeclaringType == null)
                return false;

            var type = method.DeclaringType;
            var ns = type.Namespace ?? string.Empty;
            if (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                ns.StartsWith("UnityEditor", StringComparison.Ordinal))
                return false;

            var typeName = type.Name;
            return typeName.IndexOf("ActionKitCommandHandler", StringComparison.Ordinal) < 0 &&
                   typeName.IndexOf("ActionStackTraceService", StringComparison.Ordinal) < 0 &&
                   typeName.IndexOf("IActionExtensions", StringComparison.Ordinal) < 0;
        }

        private static string FormatMethodName(MethodBase method)
        {
            if (method == null)
                return string.Empty;

            var declaringType = method.DeclaringType != null ? method.DeclaringType.Name : "Unknown";
            return declaringType + "." + method.Name + "()";
        }

        private static string NormalizeFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            var normalized = filePath.Replace('\\', '/');
            var assetsIndex = normalized.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? normalized.Substring(assetsIndex) : normalized;
        }

        private static bool ExtractBool(string json, string fieldName, bool fallback)
        {
            if (string.IsNullOrEmpty(json))
                return fallback;

            var search = "\"" + fieldName + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0)
                return fallback;

            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length)
                return fallback;

            if (json.IndexOf("true", idx, StringComparison.OrdinalIgnoreCase) == idx)
                return true;

            if (json.IndexOf("false", idx, StringComparison.OrdinalIgnoreCase) == idx)
                return false;

            return fallback;
        }
    }
}
