#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class LiveCodeOperation : IRoslynOperation
    {
        private readonly LiveCodeManager mManager;

        public LiveCodeOperation(LiveCodeManager manager, string action, System.Func<bool> permission = null,
            RoslynExecutionTarget targets = RoslynExecutionTarget.Play)
        {
            if (action != "live_status" && action != "live_remove" && action != "live_snapshot" && action != "live_set_fields")
                throw new System.ArgumentException("Unknown live code action.");
            mManager = manager;
            Descriptor = new RoslynOperationDescriptor(action,
                action == "live_status" ? YokiFrameCommandKind.ReadOnly :
                    action == "live_set_fields" ? YokiFrameCommandKind.Dangerous : YokiFrameCommandKind.UserAction,
                isDiagnostic: action == "live_status", isCancellation: action == "live_remove",
                targets: action == "live_snapshot" || action == "live_set_fields" ? targets : RoslynExecutionTargets.ALL,
                isTargetAgnostic: action == "live_status" || action == "live_remove",
                executionPermission: action == "live_snapshot" || action == "live_set_fields" ? permission : null);
        }

        public RoslynOperationDescriptor Descriptor { get; }

        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            var json = new RoslynJsonBuilder().StartObject().Property("operation", Descriptor.Action);
            if (Descriptor.Action == "live_set_fields")
            {
                try
                {
                    int count = mManager.SetFields(LiveFieldRequest.Parse(request.PayloadJson), () => { });
                    return YokiFrameCommandResult.Success(json.Property("appliedFields", count).EndObject().ToString());
                }
                catch (System.ArgumentException error) { return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error.Message); }
                catch (JsonException error) { return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error.Message); }
                catch (System.InvalidOperationException error) { return YokiFrameCommandResult.Error("LiveFieldUpdateRejected", error.Message); }
                catch (System.NotSupportedException error) { return YokiFrameCommandResult.Error("LiveFieldUpdateRejected", error.Message); }
            }
            if (Descriptor.Action == "live_snapshot")
            {
                var snapshot = mManager.Snapshot(() => { });
                json.Property("snapshotId", snapshot.SnapshotId).Property("complete", snapshot.Complete)
                    .Property("path", ".yokiframe/engine/live-snapshots/" + snapshot.SnapshotId + ".json")
                    .Property("sessionId", snapshot.SessionId).Property("generation", snapshot.Generation)
                    .Name("entries").StartArray();
                foreach (var item in snapshot.Entries)
                    json.StartObject().Property("id", item.Id).Property("error", item.Error).EndObject();
                return YokiFrameCommandResult.Success(json.EndArray().EndObject().ToString());
            }
            if (Descriptor.Action == "live_remove")
            {
                if (!RoslynRunPayload.TryReadId(request.PayloadJson, "id", out string id, out string error))
                    return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
                return YokiFrameCommandResult.Success(json.Property("id", id)
                    .Property("removed", mManager.Remove(id)).EndObject().ToString());
            }
            var budget = mManager.ReadBudget();
            json.Property("patchInstalled", mManager.PatchInstalled)
                .Property("budgetWarning", budget.BudgetWarning).Property("recoveryHint", budget.RecoveryHint)
                .Property("remainingAssemblies", budget.RemainingAssemblies).Property("remainingBytes", budget.RemainingBytes)
                .Name("handles").StartArray();
            foreach (var handle in mManager.List())
                json.StartObject().Property("id", handle.Id).Property("kind", handle.Kind)
                    .Property("revision", handle.Revision).Property("sourceHash", handle.SourceHash)
                    .Property("sessionId", handle.SessionId).Property("target", handle.Target)
                    .Property("status", handle.Status).EndObject();
            return YokiFrameCommandResult.Success(json.EndArray().EndObject().ToString());
        }
    }
}
#endif
