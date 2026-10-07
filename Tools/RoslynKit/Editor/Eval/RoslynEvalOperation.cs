#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>Godot's independent inline script eval. No C# source generation or fallback.</summary>
    public sealed class RoslynEvalOperation : IRoslynOperation
    {
        private readonly IRoslynScriptEvalService mService;
        public RoslynEvalOperation(IRoslynScriptEvalService service,
            RoslynExecutionTarget targets = RoslynExecutionTarget.Editor)
        {
            mService = service;
            Descriptor = new RoslynOperationDescriptor("eval", YokiFrameCommandKind.Dangerous, targets: targets);
        }
        public RoslynOperationDescriptor Descriptor { get; }

        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (mService == null)
                return YokiFrameCommandResult.Error(RoslynErrorCodes.UNAVAILABLE, "Script eval is not installed.");
            if (!RoslynEvalRequest.TryParse(request.PayloadJson, out var parsed, out string error))
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            if (!string.Equals(parsed.Language, "script", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(parsed.Language, mService.Language, StringComparison.OrdinalIgnoreCase))
                return YokiFrameCommandResult.Error(RoslynErrorCodes.UNSUPPORTED, "Unsupported eval language: " + parsed.Language);
            var record = mService.Submit(parsed.Id, parsed.Code, out string code, out error);
            if (record == null) return YokiFrameCommandResult.Error(code, error);
            return YokiFrameCommandResult.Success(new RoslynJsonBuilder().StartObject()
                .Property("operation", "eval").Property("language", mService.Language).Property("accepted", true)
                .Property("evalId", record.Id).Property("state", record.State)
                .Property("observeAction", "eval_result").EndObject().ToString());
        }
    }
}
#endif
