#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class LiveTuningOperation : IRoslynOperation
    {
        private readonly LiveTuningBinder mBinder;
        public RoslynOperationDescriptor Descriptor { get; }
        public LiveTuningOperation(LiveTuningBinder binder, string action, Func<bool> permission = null,
            RoslynExecutionTarget targets = RoslynExecutionTarget.Play)
        {
            if (action != "live_tuning_bind" && action != "live_tuning_status"
                && action != "live_tuning_unbind" && action != "live_tuning_refresh")
                throw new ArgumentException("Unknown live tuning action.");
            mBinder = binder;
            bool read = action == "live_tuning_status";
            bool cancel = action == "live_tuning_unbind";
            Descriptor = new RoslynOperationDescriptor(action,
                read ? YokiFrameCommandKind.ReadOnly : cancel ? YokiFrameCommandKind.UserAction : YokiFrameCommandKind.Dangerous,
                isDiagnostic: read, isCancellation: cancel,
                targets: read || cancel ? RoslynExecutionTargets.ALL : targets,
                isTargetAgnostic: read || cancel, executionPermission: read || cancel ? null : permission);
        }
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            try
            {
                var json = new RoslynJsonBuilder().StartObject().Property("operation", Descriptor.Action);
                if (Descriptor.Action == "live_tuning_status")
                {
                    json.Name("bindings").StartArray();
                    foreach (var status in mBinder.List()) Write(json, status);
                    return YokiFrameCommandResult.Success(json.EndArray().EndObject().ToString());
                }
                using var document = JsonDocument.Parse(request.PayloadJson);
                var root = document.RootElement;
                string id = LiveFieldRequest.RequiredText(root, "id");
                if (Descriptor.Action == "live_tuning_unbind")
                    return YokiFrameCommandResult.Success(json.Property("removed", mBinder.Unbind(id)).EndObject().ToString());
                if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                    throw new ArgumentException("Tuning bind/refresh requires confirmed:true.");
                var result = Descriptor.Action == "live_tuning_bind"
                    ? mBinder.Bind(id, LiveFieldRequest.RequiredText(root, "path")) : mBinder.Refresh(id);
                json.Name("binding");
                Write(json, result);
                return YokiFrameCommandResult.Success(json.EndObject().ToString());
            }
            catch (Exception error) when (error is ArgumentException || error is JsonException
                || error is InvalidOperationException || error is System.IO.IOException || error is UnauthorizedAccessException)
            {
                return YokiFrameCommandResult.Error("LiveTuningRejected", error.Message);
            }
        }
        private static void Write(RoslynJsonBuilder json, LiveTuningStatus status)
        {
            json.StartObject().Property("id", status.Id).Property("path", status.Path)
                .Property("state", status.State).Property("error", status.Error)
                .Property("applyCount", status.ApplyCount).Property("appliedFields", status.AppliedFields)
                .Property("contentHash", status.ContentHash).EndObject();
        }
    }
}
#endif
