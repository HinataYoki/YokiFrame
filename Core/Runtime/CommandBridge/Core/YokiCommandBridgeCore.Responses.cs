using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 文件命令桥核心的标准响应写入片段。
    /// </summary>
    public sealed partial class YokiCommandBridgeCore
    {
        private string BuildExceptionResponse(string fallbackRequestId, string commandJson, Exception ex)
        {
            var requestId = fallbackRequestId;
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");

            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;
            return JsonHelper.BuildError(requestId, kit, action, ex.Message, engineId, "DispatchException", false);
        }

        private string BuildInvalidRequestIdResponse(string fallbackRequestId, string commandJson, string invalidRequestId)
        {
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;

            var message = "Invalid requestId '" + invalidRequestId +
                          "'. Use 1-128 ASCII letters, digits, '.', '_' or '-' only.";
            return JsonHelper.BuildError(fallbackRequestId, kit, action, message, engineId, "InvalidRequestId", false);
        }

        private string BuildPreDispatchErrorResponse(string responseRequestId, string commandJson, string invalidRequestId)
        {
            if (!string.IsNullOrEmpty(invalidRequestId))
                return BuildInvalidRequestIdResponse(responseRequestId, commandJson, invalidRequestId);

            if (IsPayloadTooLarge(commandJson))
                return BuildPayloadTooLargeResponse(responseRequestId, commandJson);

            if (IsLikelyIncompleteJsonDocument(commandJson))
                return BuildIncompleteCommandResponse(responseRequestId, commandJson);

            if (!IsCompleteJsonDocument(commandJson))
                return BuildInvalidCommandJsonResponse(responseRequestId, commandJson);

            var commandEngineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(commandEngineId) ||
                string.Equals(commandEngineId, mOptions.EngineId, StringComparison.Ordinal))
                return null;

            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var message = "Command engineId '" + commandEngineId + "' does not match adapter engineId '" +
                          mOptions.EngineId + "'.";
            return JsonHelper.BuildError(responseRequestId, kit, action, message, mOptions.EngineId,
                "EngineIdMismatch", false);
        }

        private string BuildIncompleteCommandResponse(string responseRequestId, string commandJson)
        {
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;

            return JsonHelper.BuildError(responseRequestId, kit, action,
                "Command JSON appears incomplete after the pending write grace period.",
                engineId, "IncompleteCommandJson", false);
        }

        private string BuildInvalidCommandJsonResponse(string responseRequestId, string commandJson)
        {
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;

            return JsonHelper.BuildError(responseRequestId, kit, action,
                "Command JSON is not a complete single JSON document.",
                engineId, "InvalidCommandJson", false);
        }

        private string BuildPayloadTooLargeResponse(string responseRequestId, string commandJson)
        {
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;

            mPayloadTooLargeCount++;
            var actualBytes = Encoding.UTF8.GetByteCount(commandJson ?? string.Empty);
            var message = "Command payload is too large (" + actualBytes + " bytes, max " +
                          mOptions.MaxPayloadBytes + " bytes).";
            return JsonHelper.BuildError(responseRequestId, kit, action, message, engineId,
                "PayloadTooLarge", false);
        }

        private string EnforceResultSizeLimit(string responseRequestId, string commandJson, string resultJson)
        {
            if (mOptions.MaxResultBytes <= 0)
                return resultJson;

            var actualBytes = Encoding.UTF8.GetByteCount(resultJson ?? string.Empty);
            if (actualBytes <= mOptions.MaxResultBytes)
                return resultJson;

            mResultTooLargeCount++;
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;

            var message = "Command result is too large (" + actualBytes + " bytes, max " +
                          mOptions.MaxResultBytes + " bytes).";
            return JsonHelper.BuildError(responseRequestId, kit, action, message, engineId,
                "ResultTooLarge", false);
        }

        private string DispatchWithCurrentEngine(string commandJson)
        {
            var previousEngineId = mDispatcher.DefaultEngineId;
            mDispatcher.DefaultEngineId = mOptions.EngineId;
            try
            {
                return mDispatcher.Dispatch(commandJson);
            }
            finally
            {
                mDispatcher.DefaultEngineId = previousEngineId;
            }
        }

        private string BuildBridgeBusyResponse(string responseRequestId, string commandJson)
        {
            var kit = JsonHelper.ExtractString(commandJson ?? string.Empty, "kit");
            var action = JsonHelper.ExtractString(commandJson ?? string.Empty, "action");
            if (string.IsNullOrEmpty(kit))
                kit = "System";
            if (string.IsNullOrEmpty(action))
                action = "dispatch";

            var engineId = JsonHelper.ExtractString(commandJson ?? string.Empty, "engineId");
            if (string.IsNullOrEmpty(engineId))
                engineId = mOptions.EngineId;

            var message = "Command bridge is busy; pending command count exceeded max " +
                          mOptions.MaxPendingCommands + ".";
            return JsonHelper.BuildError(responseRequestId, kit, action, message, engineId,
                "BridgeBusy", true);
        }

        private bool IsPayloadTooLarge(string commandJson)
        {
            if (mOptions.MaxPayloadBytes <= 0)
                return false;

            return Encoding.UTF8.GetByteCount(commandJson ?? string.Empty) > mOptions.MaxPayloadBytes;
        }
    }
}
