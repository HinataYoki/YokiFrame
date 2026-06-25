using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// Kit 命令分发器（纯 C#，跨引擎）。
    /// 根据 kit 字段把命令路由到匹配的 IKitCommandHandler。
    /// </summary>
    public sealed class KitCommandDispatcher
    {
        private readonly Dictionary<string, IKitCommandHandler> mHandlers = new();

        /// <summary>
        /// 命令缺少 engineId 时使用的默认宿主引擎标识。
        /// </summary>
        public string DefaultEngineId { get; set; } = "base";

        /// <summary>
        /// 可选命令策略钩子，用于在分发前拒绝或允许命令。
        /// </summary>
        public Func<CommandBridgeCommand, CommandBridgePolicyResult> CommandPolicy { get; set; }

        /// <summary>注册一个 Kit 命令处理器。</summary>
        public void Register(IKitCommandHandler handler)
        {
            if (handler == default) throw new ArgumentNullException(nameof(handler));
            mHandlers[handler.KitName] = handler;
        }

        /// <summary>注销一个 Kit 命令处理器。</summary>
        public void Unregister(string kitName) => mHandlers.Remove(kitName);

        /// <summary>
        /// 构建当前命令桥已注册 Kit/action 目录，供 Tauri 下拉框按真实宿主能力展示。
        /// </summary>
        public string BuildCommandCatalogJson()
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"kits\":[");
            var firstKit = true;
            foreach (var pair in mHandlers)
            {
                var handler = pair.Value;
                if (handler == null)
                    continue;

                var kitName = string.IsNullOrEmpty(handler.KitName) ? pair.Key : handler.KitName;
                if (!CommandBridgeProtocol.IsSafeIdentifier(kitName))
                    continue;

                if (!firstKit)
                    sb.Append(',');

                firstKit = false;
                sb.Append("{\"kit\":\"");
                sb.Append(JsonHelper.EscapeString(kitName));
                sb.Append("\",\"actions\":[");

                var actions = handler.SupportedActions;
                var firstAction = true;
                if (actions != null)
                {
                    for (var i = 0; i < actions.Length; i++)
                    {
                        var action = actions[i];
                        if (!CommandBridgeProtocol.IsSafeIdentifier(action))
                            continue;

                        if (!firstAction)
                            sb.Append(',');

                        firstAction = false;
                        sb.Append("{\"action\":\"");
                        sb.Append(JsonHelper.EscapeString(action));
                        sb.Append("\"}");
                    }
                }

                sb.Append("]}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 分发命令 JSON 并返回响应 JSON。
        /// 未知 kit 或 action 会转成标准错误响应，避免调用方无限等待。
        /// </summary>
        public string Dispatch(string commandJson)
        {
            // 提取路由字段。
            var kit = JsonHelper.ExtractString(commandJson, "kit");
            var action = JsonHelper.ExtractString(commandJson, "action");
            var requestId = JsonHelper.ExtractString(commandJson, "requestId") ?? string.Empty;
            var source = JsonHelper.ExtractString(commandJson, "source") ?? string.Empty;
            var engineId = ResolveEngineId(commandJson);

            if (string.IsNullOrEmpty(kit))
                return JsonHelper.BuildError(requestId, "System", "dispatch", "Missing 'kit' field in command JSON", engineId, "InvalidCommand", false);

            if (string.IsNullOrEmpty(action))
                return JsonHelper.BuildError(requestId, kit, "dispatch", "Missing 'action' field in command JSON", engineId, "InvalidCommand", false);

            if (!string.IsNullOrEmpty(source) && !CommandBridgeProtocol.IsSafeIdentifier(source))
                return JsonHelper.BuildError(requestId, kit, action, "Invalid source identifier '" + source + "'", engineId, "InvalidSource", false);

            if (!CommandBridgeProtocol.IsSafeIdentifier(kit))
                return JsonHelper.BuildError(requestId, kit, action, "Invalid kit identifier '" + kit + "'", engineId, "InvalidKit", false);

            if (!CommandBridgeProtocol.IsSafeIdentifier(action))
                return JsonHelper.BuildError(requestId, kit, action, "Invalid action identifier '" + action + "'", engineId, "InvalidAction", false);

            var payloadJson = JsonHelper.ExtractRaw(commandJson, "payload") ?? "{}";
            var command = new CommandBridgeCommand(requestId, engineId, source, kit, action, payloadJson);
            var policyResult = CommandPolicy != null ? CommandPolicy(command) : CommandBridgePolicyResult.Allow();
            if (policyResult == null || !policyResult.Allowed)
            {
                var code = policyResult != null ? policyResult.ErrorCode : "PolicyDenied";
                var message = policyResult != null ? policyResult.Message : "Command rejected by policy";
                var recoverable = policyResult != null && policyResult.Recoverable;
                return JsonHelper.BuildError(requestId, kit, action, message, engineId, code, recoverable);
            }

            if (kit == "System" && action == "list_commands")
                return JsonHelper.BuildResponse(requestId, kit, action, "success", BuildCommandCatalogJson(), engineId);

            // 路由到已注册的处理器（包含 System）。
            if (!mHandlers.TryGetValue(kit, out var handler))
                return JsonHelper.BuildError(requestId, kit, action, $"No handler registered for kit '{kit}'", engineId, "UnknownKit", false);

            try
            {
                var resultData = handler.HandleAction(action, payloadJson);
                return JsonHelper.BuildResponse(requestId, kit, action, "success", resultData, engineId);
            }
            catch (Exception ex)
            {
                return JsonHelper.BuildError(requestId, kit, action, ex.Message, engineId, "HandlerException", false);
            }
        }

        private string ResolveEngineId(string commandJson)
        {
            var engineId = JsonHelper.ExtractString(commandJson, "engineId");
            return string.IsNullOrEmpty(engineId) ? DefaultEngineId : engineId;
        }
    }
}
