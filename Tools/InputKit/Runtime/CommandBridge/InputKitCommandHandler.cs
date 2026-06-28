using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// InputKit 命令桥处理器。
    /// 这里只输出当前输入状态、上下文和缓冲诊断，不把高频输入轮询搬进文件桥。
    /// </summary>
    public sealed class InputKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        /// <summary>
        /// Kit 名称。
        /// </summary>
        public string KitName
        {
            get { return "InputKit"; }
        }

        /// <summary>
        /// 支持的命令动作。
        /// </summary>
        public string[] SupportedActions
        {
            get
            {
                return new[]
                {
                    "stats",
                    "list_actions",
                    "list_contexts",
                    "get_workbench_snapshot"
                };
            }
        }

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            return BuildStatsJson(InputKit.CreateDiagnosticsSnapshot());
        }

        /// <summary>
        /// 处理 InputKit 命令桥动作。
        /// </summary>
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return BuildStatsJson(InputKit.CreateDiagnosticsSnapshot());
                case "list_actions":
                    return BuildActionsJson(InputKit.CreateDiagnosticsSnapshot().Actions);
                case "list_contexts":
                    return BuildContextsJson(InputKit.CreateDiagnosticsSnapshot());
                case "get_workbench_snapshot":
                    return BuildWorkbenchSnapshotJson();
                default:
                    throw new NotSupportedException("Unknown InputKit action '" + action + "'");
            }
        }

        private static string BuildWorkbenchSnapshotJson()
        {
            var snapshot = InputKit.CreateDiagnosticsSnapshot();
            var stats = BuildStatsJson(snapshot);
            var actions = BuildActionsJson(snapshot.Actions);
            var contexts = BuildContextsJson(snapshot);

            var sb = new StringBuilder(stats.Length + actions.Length + contexts.Length + 64);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"actions\":");
            sb.Append(actions);
            sb.Append(",\"contexts\":");
            sb.Append(contexts);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStatsJson(InputKitDiagnosticsSnapshot snapshot)
        {
            int pressedCount = 0;
            int releasedCount = 0;
            int bufferedActionCount = snapshot.BufferedInputCount;

            for (var i = 0; i < snapshot.Actions.Count; i++)
            {
                var action = snapshot.Actions[i];
                if (action.IsPressed)
                    pressedCount++;
                if (action.WasReleasedThisFrame)
                    releasedCount++;
            }

            var sb = new StringBuilder(256);
            sb.Append("{\"isInitialized\":");
            sb.Append(snapshot.IsInitialized ? "true" : "false");
            sb.Append(",\"backendName\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.BackendName));
            sb.Append("\",\"currentDeviceType\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.CurrentDeviceType.ToString()));
            sb.Append("\",\"isGamepadConnected\":");
            sb.Append(snapshot.IsGamepadConnected ? "true" : "false");
            sb.Append(",\"currentTime\":");
            AppendFloat(sb, snapshot.CurrentTime);
            sb.Append(",\"bufferWindowMs\":");
            AppendFloat(sb, snapshot.BufferWindowMs);
            sb.Append(",\"bufferedInputCount\":");
            sb.Append(bufferedActionCount);
            sb.Append(",\"actionCount\":");
            sb.Append(snapshot.Actions.Count);
            sb.Append(",\"pressedActionCount\":");
            sb.Append(pressedCount);
            sb.Append(",\"releasedActionCount\":");
            sb.Append(releasedCount);
            sb.Append(",\"enabledActionMapCount\":");
            sb.Append(snapshot.EnabledActionMaps.Count);
            sb.Append(",\"activeContextCount\":");
            sb.Append(snapshot.ActiveContexts.Count);
            sb.Append(",\"registeredContextCount\":");
            sb.Append(snapshot.RegisteredContexts.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildActionsJson(List<InputActionDiagnosticsSnapshot> actions)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"actions\":[");
            for (var i = 0; i < actions.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendAction(sb, actions[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(actions.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildContextsJson(InputKitDiagnosticsSnapshot snapshot)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"activeContexts\":[");
            AppendContextsArray(sb, snapshot.ActiveContexts);
            sb.Append("],\"registeredContexts\":[");
            AppendContextsArray(sb, snapshot.RegisteredContexts);
            sb.Append("],\"enabledActionMaps\":[");
            for (var i = 0; i < snapshot.EnabledActionMaps.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append("\"");
                sb.Append(JsonHelper.EscapeString(snapshot.EnabledActionMaps[i] ?? string.Empty));
                sb.Append("\"");
            }

            sb.Append("],\"currentContext\":");
            if (snapshot.ActiveContexts.Count > 0)
            {
                AppendContext(sb, snapshot.ActiveContexts[snapshot.ActiveContexts.Count - 1]);
            }
            else
            {
                sb.Append("null");
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendContextsArray(StringBuilder sb, List<InputContextDiagnosticsSnapshot> contexts)
        {
            for (var i = 0; i < contexts.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendContext(sb, contexts[i]);
            }
        }

        private static void AppendAction(StringBuilder sb, InputActionDiagnosticsSnapshot action)
        {
            sb.Append("{\"actionName\":\"");
            sb.Append(JsonHelper.EscapeString(action.ActionName));
            sb.Append("\",\"isPressed\":");
            sb.Append(action.IsPressed ? "true" : "false");
            sb.Append(",\"wasPressedThisFrame\":");
            sb.Append(action.WasPressedThisFrame ? "true" : "false");
            sb.Append(",\"wasReleasedThisFrame\":");
            sb.Append(action.WasReleasedThisFrame ? "true" : "false");
            sb.Append(",\"value\":");
            AppendFloat(sb, action.Value);
            sb.Append(",\"lastChangedAt\":");
            AppendFloat(sb, action.LastChangedAt);
            sb.Append('}');
        }

        private static void AppendContext(StringBuilder sb, InputContextDiagnosticsSnapshot context)
        {
            sb.Append("{\"contextName\":\"");
            sb.Append(JsonHelper.EscapeString(context.ContextName));
            sb.Append("\",\"priority\":");
            sb.Append(context.Priority);
            sb.Append(",\"stackIndex\":");
            sb.Append(context.StackIndex);
            sb.Append(",\"blockAllLowerPriority\":");
            sb.Append(context.BlockAllLowerPriority ? "true" : "false");
            sb.Append(",\"enabledActionMaps\":[");
            AppendStringArray(sb, context.EnabledActionMaps);
            sb.Append("],\"blockedActions\":[");
            AppendStringArray(sb, context.BlockedActions);
            sb.Append("]}");
        }

        private static void AppendStringArray(StringBuilder sb, string[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append("\"");
                sb.Append(JsonHelper.EscapeString(values[i] ?? string.Empty));
                sb.Append("\"");
            }
        }

        private static void AppendFloat(StringBuilder sb, float value)
        {
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
