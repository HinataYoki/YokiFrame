#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>Pure result queries include historical records; cancellation requires live ownership.</summary>
    public sealed class RoslynRunOperation : IRoslynOperation
    {
        private readonly IRoslynRunScheduler mScheduler;

        public RoslynRunOperation(IRoslynRunScheduler scheduler, string action)
        {
            if (action != "run_result" && action != "run_lookup" && action != "run_cancel")
                throw new System.ArgumentException("Unknown run action.", nameof(action));
            mScheduler = scheduler;
            Descriptor = new RoslynOperationDescriptor(action,
                action == "run_cancel" ? YokiFrameCommandKind.UserAction : YokiFrameCommandKind.ReadOnly,
                isDiagnostic: action != "run_cancel", isCancellation: action == "run_cancel",
                targets: RoslynExecutionTargets.ALL, isTargetAgnostic: true);
        }

        public RoslynOperationDescriptor Descriptor { get; }

        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            string action = Descriptor.Action;
            if (!RoslynRunPayload.TryReadId(request.PayloadJson,
                action == "run_lookup" ? "requestId" : "runId", out string id, out string error))
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            RoslynRunRecord record;
            bool found = action == "run_lookup"
                ? mScheduler.TryLookupReadOnly(id, out record) : mScheduler.TryReadRun(id, out record);
            if (!found)
                return YokiFrameCommandResult.Error(RoslynErrorCodes.RUN_NOT_FOUND, "Run was not found.");
            if (action == "run_cancel")
            {
                if (!mScheduler.TryCancel(record.RunId, out string code, out error))
                    return YokiFrameCommandResult.Error(code, error);
                mScheduler.TryReadRun(record.RunId, out record);
            }
            string json = string.Empty;
            if (record.ResultPath.Length > 0) mScheduler.TryReadResultFile(record.ResultPath, out json);
            return YokiFrameCommandResult.Success(RoslynRunJson.WriteResult(record, json, action));
        }
    }
}
#endif
