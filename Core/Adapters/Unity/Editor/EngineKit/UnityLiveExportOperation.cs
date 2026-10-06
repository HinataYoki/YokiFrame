#if UNITY_EDITOR
using System;
using YokiFrame.Json;

namespace YokiFrame
{
    internal sealed class UnityLiveExportOperation : IYokiFrameEngineOperation
    {
        private readonly UnityLiveCodeHost mHost;
        private readonly YokiFrameLiveCodeManager mManager;
        private readonly Func<bool> mPermitted;
        public YokiFrameEngineOperationDescriptor Descriptor { get; }
        public UnityLiveExportOperation(UnityLiveCodeHost host, YokiFrameLiveCodeManager manager, string action, Func<bool> permitted)
        {
            mHost = host; mManager = manager; mPermitted = permitted;
            bool read = action == "live_export_status";
            Descriptor = new YokiFrameEngineOperationDescriptor(action,
                read ? YokiFrameCommandKind.ReadOnly : YokiFrameCommandKind.Dangerous,
                isDiagnostic: read, isTargetAgnostic: read,
                targets: YokiFrameEngineExecutionTarget.Editor | YokiFrameEngineExecutionTarget.Play,
                executionPermission: read ? null : permitted);
        }
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            try
            {
                using var document = JsonDocument.Parse(request.PayloadJson);
                var root = document.RootElement;
                string batchId = root.TryGetProperty("batchId", out var batch) ? batch.GetString() : "";
                if (Descriptor.Action == "live_export_status")
                {
                    string exportId = root.TryGetProperty("exportId", out var item) ? item.GetString() : "";
                    if (string.IsNullOrEmpty(batchId) == string.IsNullOrEmpty(exportId))
                        throw new ArgumentException("Supply exactly one of batchId or exportId.");
                    return YokiFrameCommandResult.Success(mHost.ReadExportStatus(batchId, exportId));
                }
                if (!mPermitted()) throw new InvalidOperationException("Trusted C# permission is required.");
                if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                    throw new ArgumentException("Commit requires confirmed:true.");
                mManager.CommitExport(batchId, () =>
                {
                    if (!mPermitted()) throw new InvalidOperationException("Permission changed.");
                });
                return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder().StartObject()
                    .Property("operation", "live_export_commit").Property("batchId", batchId)
                    .Property("observeAction", "live_export_status").EndObject().ToString());
            }
            catch (Exception error)
            {
                return YokiFrameCommandResult.Error("LiveExportRejected", error.Message);
            }
        }
    }
}
#endif
