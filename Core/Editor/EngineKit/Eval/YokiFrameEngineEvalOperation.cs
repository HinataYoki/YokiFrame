#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>Godot's independent inline script eval. No C# source generation or fallback.</summary>
    public sealed class YokiFrameEngineEvalOperation : IYokiFrameEngineOperation
    {
        private readonly IYokiFrameEngineScriptEvalService mService;
        public YokiFrameEngineEvalOperation(IYokiFrameEngineScriptEvalService service,
            YokiFrameEngineExecutionTarget targets = YokiFrameEngineExecutionTarget.Editor)
        {
            mService = service;
            Descriptor = new YokiFrameEngineOperationDescriptor("eval", YokiFrameCommandKind.Dangerous, targets: targets);
        }
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (mService == null)
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.UNAVAILABLE, "Script eval is not installed.");
            if (!YokiFrameEngineEvalRequest.TryParse(request.PayloadJson, out var parsed, out string error))
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, error);
            if (!string.Equals(parsed.Language, "script", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(parsed.Language, mService.Language, StringComparison.OrdinalIgnoreCase))
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.UNSUPPORTED, "Unsupported eval language: " + parsed.Language);
            var record = mService.Submit(parsed.Id, parsed.Code, out string code, out error);
            if (record == null) return YokiFrameCommandResult.Error(code, error);
            return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder().StartObject()
                .Property("operation", "eval").Property("language", mService.Language).Property("accepted", true)
                .Property("evalId", record.Id).Property("state", record.State)
                .Property("observeAction", "eval_result").EndObject().ToString());
        }
    }
}
#endif
