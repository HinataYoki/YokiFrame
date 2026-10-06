#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>Pure result queries include historical records; cancellation requires live ownership.</summary>
    public sealed class YokiFrameEngineRunOperation : IYokiFrameEngineOperation
    {
        private readonly IYokiFrameEngineRunScheduler mScheduler;

        public YokiFrameEngineRunOperation(IYokiFrameEngineRunScheduler scheduler, string action)
        {
            if (action != "run_result" && action != "run_lookup" && action != "run_cancel")
                throw new System.ArgumentException("Unknown run action.", nameof(action));
            mScheduler = scheduler;
            Descriptor = new YokiFrameEngineOperationDescriptor(action,
                action == "run_cancel" ? YokiFrameCommandKind.UserAction : YokiFrameCommandKind.ReadOnly,
                isDiagnostic: action != "run_cancel", isCancellation: action == "run_cancel",
                targets: YokiFrameEngineExecutionTargets.ALL, isTargetAgnostic: true);
        }

        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            string action = Descriptor.Action;
            if (!YokiFrameEngineRunPayload.TryReadId(request.PayloadJson,
                action == "run_lookup" ? "requestId" : "runId", out string id, out string error))
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, error);
            YokiFrameEngineRunRecord record;
            bool found = action == "run_lookup"
                ? mScheduler.TryLookupReadOnly(id, out record) : mScheduler.TryReadRun(id, out record);
            if (!found)
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.RUN_NOT_FOUND, "Run was not found.");
            if (action == "run_cancel")
            {
                if (!mScheduler.TryCancel(record.RunId, out string code, out error))
                    return YokiFrameCommandResult.Error(code, error);
                mScheduler.TryReadRun(record.RunId, out record);
            }
            string json = string.Empty;
            if (record.ResultPath.Length > 0) mScheduler.TryReadResultFile(record.ResultPath, out json);
            return YokiFrameCommandResult.Success(YokiFrameEngineRunJson.WriteResult(record, json, action));
        }
    }
}
#endif
