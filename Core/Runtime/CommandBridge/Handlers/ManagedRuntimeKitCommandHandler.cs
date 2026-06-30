using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// ManagedRuntimeKit 命令处理器：为工作台和 AI 提供托管运行时后端选择与工作流动作入口。
    /// </summary>
    public sealed class ManagedRuntimeKitCommandHandler : IKitCommandHandler
    {
        public string KitName
        {
            get { return "ManagedRuntimeKit"; }
        }

        public string[] SupportedActions
        {
            get
            {
                return new[]
                {
                    "get_workbench_snapshot",
                    "list_backends",
                    "validate",
                    "select_backend",
                    "run_action",
                    "get_backend_settings",
                    "save_backend_settings"
                };
            }
        }

        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "list_backends":
                    return ListBackends();
                case "validate":
                    return AppendValidationToString(ManagedRuntimeKit.ValidateCurrent());
                case "select_backend":
                    return SelectBackend(payloadJson);
                case "run_action":
                    return RunAction(payloadJson);
                case "get_backend_settings":
                    return GetBackendSettings(payloadJson);
                case "save_backend_settings":
                    return SaveBackendSettings(payloadJson);
                default:
                    throw new NotSupportedException("Unknown ManagedRuntimeKit action '" + action + "'");
            }
        }

        private static string GetWorkbenchSnapshot()
        {
            var backends = ListBackends();
            var validation = AppendValidationToString(ManagedRuntimeKit.ValidateCurrent());
            var sb = new StringBuilder(backends.Length + validation.Length + 96);
            sb.Append("{\"currentBackendId\":\"");
            sb.Append(JsonHelper.EscapeString(ManagedRuntimeKit.CurrentBackendId));
            sb.Append("\",\"validation\":");
            sb.Append(validation);
            sb.Append(",\"backends\":");
            sb.Append(backends);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListBackends()
        {
            var infos = ManagedRuntimeKit.GetBackendInfos();
            var sb = new StringBuilder(512);
            sb.Append('[');
            for (var i = 0; i < infos.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendBackendInfo(sb, infos[i]);
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string SelectBackend(string payloadJson)
        {
            var backendId = JsonHelper.ExtractString(payloadJson, "backendId");
            var result = ManagedRuntimeKit.SelectBackend(backendId);
            var sb = new StringBuilder(160);
            sb.Append("{\"success\":");
            sb.Append(result.Success ? "true" : "false");
            sb.Append(",\"status\":\"");
            sb.Append(JsonHelper.EscapeString(result.Status.ToString()));
            sb.Append("\",\"backendId\":\"");
            sb.Append(JsonHelper.EscapeString(result.BackendId));
            sb.Append("\",\"message\":\"");
            sb.Append(JsonHelper.EscapeString(result.Message));
            sb.Append("\"}");
            return sb.ToString();
        }

        private static string RunAction(string payloadJson)
        {
            var backendId = JsonHelper.ExtractString(payloadJson, "backendId");
            var actionId = JsonHelper.ExtractString(payloadJson, "actionId");
            var actionPayload = JsonHelper.ExtractRaw(payloadJson, "payload") ?? "{}";
            var result = ManagedRuntimeKit.ExecuteAction(backendId, actionId, actionPayload);
            return AppendActionResultToString(result);
        }

        private static string GetBackendSettings(string payloadJson)
        {
            var backendId = JsonHelper.ExtractString(payloadJson, "backendId");
            var result = ManagedRuntimeKit.GetBackendSettings(backendId);
            return AppendActionResultToString(result);
        }

        private static string SaveBackendSettings(string payloadJson)
        {
            var backendId = JsonHelper.ExtractString(payloadJson, "backendId");
            var settingsPayload = JsonHelper.ExtractRaw(payloadJson, "settings") ?? "{}";
            var result = ManagedRuntimeKit.SaveBackendSettings(backendId, settingsPayload);
            return AppendActionResultToString(result);
        }

        private static void AppendBackendInfo(StringBuilder sb, ManagedRuntimeInfo info)
        {
            sb.Append("{\"backendId\":\"");
            sb.Append(JsonHelper.EscapeString(info.BackendId));
            sb.Append("\",\"displayName\":\"");
            sb.Append(JsonHelper.EscapeString(info.DisplayName));
            sb.Append("\",\"hostName\":\"");
            sb.Append(JsonHelper.EscapeString(info.HostName));
            sb.Append("\",\"targetName\":\"");
            sb.Append(JsonHelper.EscapeString(info.TargetName));
            sb.Append("\",\"executionMode\":\"");
            sb.Append(JsonHelper.EscapeString(info.ExecutionMode));
            sb.Append("\",\"availability\":\"");
            sb.Append(JsonHelper.EscapeString(info.Availability.ToString()));
            sb.Append("\",\"capabilities\":\"");
            sb.Append(JsonHelper.EscapeString(info.Capabilities.ToString()));
            sb.Append("\",\"description\":\"");
            sb.Append(JsonHelper.EscapeString(info.Description));
            sb.Append("\",\"actions\":");
            AppendActions(sb, ManagedRuntimeKit.GetActions(info.BackendId));
            sb.Append(",\"settings\":");
            AppendBackendSettings(sb, info.BackendId);
            sb.Append('}');
        }

        private static void AppendBackendSettings(StringBuilder sb, string backendId)
        {
            var result = ManagedRuntimeKit.GetBackendSettings(backendId);
            if (result != null && result.Success && IsJsonObjectOrArray(result.DataJson))
            {
                sb.Append(result.DataJson);
                return;
            }

            sb.Append("null");
        }

        private static void AppendActions(StringBuilder sb, ManagedRuntimeActionDescriptor[] actions)
        {
            sb.Append('[');
            if (actions != null)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    AppendAction(sb, actions[i]);
                }
            }

            sb.Append(']');
        }

        private static void AppendAction(StringBuilder sb, ManagedRuntimeActionDescriptor action)
        {
            sb.Append("{\"actionId\":\"");
            sb.Append(JsonHelper.EscapeString(action.ActionId));
            sb.Append("\",\"displayName\":\"");
            sb.Append(JsonHelper.EscapeString(action.DisplayName));
            sb.Append("\",\"description\":\"");
            sb.Append(JsonHelper.EscapeString(action.Description));
            sb.Append("\",\"supported\":");
            sb.Append(action.Supported ? "true" : "false");
            sb.Append(",\"requiresConfirmation\":");
            sb.Append(action.RequiresConfirmation ? "true" : "false");
            sb.Append(",\"destructive\":");
            sb.Append(action.Destructive ? "true" : "false");
            sb.Append('}');
        }

        private static string AppendValidationToString(ManagedRuntimeValidationResult validation)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"isValid\":");
            sb.Append(validation != null && validation.IsValid ? "true" : "false");
            sb.Append(",\"info\":");
            if (validation != null && validation.Info != null)
                AppendBackendInfoWithoutActions(sb, validation.Info);
            else
                sb.Append("null");
            sb.Append(",\"diagnostics\":");
            AppendDiagnostics(sb, validation != null ? validation.Diagnostics : null);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendBackendInfoWithoutActions(StringBuilder sb, ManagedRuntimeInfo info)
        {
            sb.Append("{\"backendId\":\"");
            sb.Append(JsonHelper.EscapeString(info.BackendId));
            sb.Append("\",\"displayName\":\"");
            sb.Append(JsonHelper.EscapeString(info.DisplayName));
            sb.Append("\",\"hostName\":\"");
            sb.Append(JsonHelper.EscapeString(info.HostName));
            sb.Append("\",\"targetName\":\"");
            sb.Append(JsonHelper.EscapeString(info.TargetName));
            sb.Append("\",\"executionMode\":\"");
            sb.Append(JsonHelper.EscapeString(info.ExecutionMode));
            sb.Append("\",\"availability\":\"");
            sb.Append(JsonHelper.EscapeString(info.Availability.ToString()));
            sb.Append("\",\"capabilities\":\"");
            sb.Append(JsonHelper.EscapeString(info.Capabilities.ToString()));
            sb.Append("\",\"description\":\"");
            sb.Append(JsonHelper.EscapeString(info.Description));
            sb.Append("\"}");
        }

        private static void AppendDiagnostics(StringBuilder sb, IReadOnlyList<ManagedRuntimeDiagnostic> diagnostics)
        {
            sb.Append('[');
            if (diagnostics != null)
            {
                for (var i = 0; i < diagnostics.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    var diagnostic = diagnostics[i];
                    sb.Append("{\"severity\":\"");
                    sb.Append(JsonHelper.EscapeString(diagnostic.Severity.ToString()));
                    sb.Append("\",\"code\":\"");
                    sb.Append(JsonHelper.EscapeString(diagnostic.Code));
                    sb.Append("\",\"message\":\"");
                    sb.Append(JsonHelper.EscapeString(diagnostic.Message));
                    sb.Append("\",\"hint\":\"");
                    sb.Append(JsonHelper.EscapeString(diagnostic.Hint));
                    sb.Append("\"}");
                }
            }

            sb.Append(']');
        }

        private static string AppendActionResultToString(ManagedRuntimeActionResult result)
        {
            var sb = new StringBuilder(192);
            sb.Append("{\"success\":");
            sb.Append(result != null && result.Success ? "true" : "false");
            sb.Append(",\"backendId\":\"");
            sb.Append(JsonHelper.EscapeString(result != null ? result.BackendId : string.Empty));
            sb.Append("\",\"actionId\":\"");
            sb.Append(JsonHelper.EscapeString(result != null ? result.ActionId : string.Empty));
            sb.Append("\",\"message\":\"");
            sb.Append(JsonHelper.EscapeString(result != null ? result.Message : string.Empty));
            sb.Append("\"");

            if (result != null && !string.IsNullOrEmpty(result.ErrorCode))
            {
                sb.Append(",\"errorCode\":\"");
                sb.Append(JsonHelper.EscapeString(result.ErrorCode));
                sb.Append("\"");
            }

            if (result != null && !string.IsNullOrEmpty(result.DataJson))
            {
                sb.Append(",\"data\":");
                AppendRawJsonOrString(sb, result.DataJson);
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendRawJsonOrString(StringBuilder sb, string json)
        {
            if (IsJsonObjectOrArray(json))
            {
                sb.Append(json);
                return;
            }

            sb.Append('"');
            sb.Append(JsonHelper.EscapeString(json));
            sb.Append('"');
        }

        private static bool IsJsonObjectOrArray(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            var trimmed = json.Trim();
            if (trimmed.Length < 2)
                return false;

            return (trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}') ||
                   (trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']');
        }
    }
}
