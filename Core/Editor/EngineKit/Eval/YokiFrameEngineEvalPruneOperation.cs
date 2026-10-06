#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// eval_prune：回收 eval 生成源码与记录（§13 cleanup）。
    /// </summary>
    public sealed class YokiFrameEngineEvalPruneOperation : IYokiFrameEngineOperation
    {
        private readonly IYokiFrameEngineScriptEvalService mScriptService;

        /// <summary>创建 eval_prune 操作。</summary>
        /// <param name="scriptService">脚本 eval 服务；宿主只有脚本 eval 时也要能回收。</param>
        public YokiFrameEngineEvalPruneOperation(
            IYokiFrameEngineScriptEvalService scriptService)
        {
            mScriptService = scriptService;
            Descriptor = new YokiFrameEngineOperationDescriptor(
                "eval_prune",
                YokiFrameCommandKind.Maintenance,
                isDiagnostic: false,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTargets.ALL);
        }

        /// <summary>获取操作描述。</summary>
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        /// <summary>回收；payload 支持 {"includeNonTerminal":true}。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (mScriptService == null)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.UNAVAILABLE,
                    "This host does not wire the eval subsystem.");
            }

            if (!TryReadIncludeNonTerminal(request.PayloadJson, out bool includeNonTerminal, out string error))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, error);
            }

            int removedScripts = mScriptService.Prune(includeNonTerminal);
            return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("operation", "eval_prune")
                .Property("removedRecords", removedScripts)
                .Property("removedScripts", removedScripts)
                .Property("includeNonTerminal", includeNonTerminal)
                .EndObject()
                .ToString());
        }

        private static bool TryReadIncludeNonTerminal(string payloadJson, out bool includeNonTerminal, out string error)
        {
            includeNonTerminal = false;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return true;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(payloadJson))
                {
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        error = "eval_prune payload must be a JSON object.";
                        return false;
                    }

                    if (root.TryGetProperty("includeNonTerminal", out JsonElement value)
                        && value.ValueKind != JsonValueKind.Null)
                    {
                        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                        {
                            error = "eval_prune includeNonTerminal must be a boolean.";
                            return false;
                        }

                        includeNonTerminal = value.ValueKind == JsonValueKind.True;
                    }
                }
            }
            catch (JsonException exception)
            {
                error = "eval_prune payload is not valid JSON: " + exception.Message;
                return false;
            }

            return true;
        }
    }
}
#endif
