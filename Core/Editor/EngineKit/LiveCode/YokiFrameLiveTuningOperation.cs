#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class YokiFrameLiveTuningOperation : IYokiFrameEngineOperation
    {
        private readonly YokiFrameLiveTuningBinder mBinder;
        public YokiFrameEngineOperationDescriptor Descriptor { get; }
        public YokiFrameLiveTuningOperation(YokiFrameLiveTuningBinder binder, string action, Func<bool> permission = null,
            YokiFrameEngineExecutionTarget targets = YokiFrameEngineExecutionTarget.Play)
        {
            if (action != "live_tuning_bind" && action != "live_tuning_status"
                && action != "live_tuning_unbind" && action != "live_tuning_refresh")
                throw new ArgumentException("Unknown live tuning action.");
            mBinder = binder;
            bool read = action == "live_tuning_status";
            bool cancel = action == "live_tuning_unbind";
            Descriptor = new YokiFrameEngineOperationDescriptor(action,
                read ? YokiFrameCommandKind.ReadOnly : cancel ? YokiFrameCommandKind.UserAction : YokiFrameCommandKind.Dangerous,
                isDiagnostic: read, isCancellation: cancel,
                targets: read || cancel ? YokiFrameEngineExecutionTargets.ALL : targets,
                isTargetAgnostic: read || cancel, executionPermission: read || cancel ? null : permission);
        }
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            try
            {
                var json = new YokiFrameEngineJsonBuilder().StartObject().Property("operation", Descriptor.Action);
                if (Descriptor.Action == "live_tuning_status")
                {
                    json.Name("bindings").StartArray();
                    foreach (var status in mBinder.List()) Write(json, status);
                    return YokiFrameCommandResult.Success(json.EndArray().EndObject().ToString());
                }
                using var document = JsonDocument.Parse(request.PayloadJson);
                var root = document.RootElement;
                string id = YokiFrameLiveFieldRequest.RequiredText(root, "id");
                if (Descriptor.Action == "live_tuning_unbind")
                    return YokiFrameCommandResult.Success(json.Property("removed", mBinder.Unbind(id)).EndObject().ToString());
                if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                    throw new ArgumentException("Tuning bind/refresh requires confirmed:true.");
                var result = Descriptor.Action == "live_tuning_bind"
                    ? mBinder.Bind(id, YokiFrameLiveFieldRequest.RequiredText(root, "path")) : mBinder.Refresh(id);
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
        private static void Write(YokiFrameEngineJsonBuilder json, YokiFrameLiveTuningStatus status)
        {
            json.StartObject().Property("id", status.Id).Property("path", status.Path)
                .Property("state", status.State).Property("error", status.Error)
                .Property("applyCount", status.ApplyCount).Property("appliedFields", status.AppliedFields)
                .Property("contentHash", status.ContentHash).EndObject();
        }
    }
}
#endif
