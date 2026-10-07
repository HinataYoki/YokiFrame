#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class LiveAttachmentRequest
    {
        public string Id { get; }
        public object Target { get; }
        public string ClassName { get; }
        public string Members { get; }
        public LiveAttachmentRequest(string id, object target, string className, string members)
        {
            Id = id; Target = target; ClassName = className; Members = members;
        }
    }

    public sealed class LiveBatchItem
    {
        public string Id { get; internal set; }
        public string Status { get; internal set; } = "notStarted";
        public string Error { get; internal set; } = "";
        public LiveCodeHandle Handle { get; internal set; }
    }

    public sealed class LiveBatchResult
    {
        public bool Success { get; internal set; }
        public string Stage { get; internal set; } = "validation";
        public string Error { get; internal set; } = "";
        public bool UserCodeMayHaveRun { get; internal set; }
        public int RequiredAssemblies { get; internal set; }
        public long RequiredBytes { get; internal set; }
        public RoslynBudgetStatus Budget { get; internal set; }
        internal readonly List<LiveBatchItem> mItems = new List<LiveBatchItem>();
        public IReadOnlyList<LiveBatchItem> Items => mItems.AsReadOnly();
    }

    public sealed class RoslynBudgetStatus
    {
        public int LoadedAssemblies { get; internal set; }
        public long LoadedBytes { get; internal set; }
        public int MaxAssemblies => RoslynLoadBudget.MaxAssemblies;
        public long MaxLoadedBytes => RoslynLoadBudget.MaxLoadedBytes;
        public int RemainingAssemblies => MaxAssemblies - LoadedAssemblies;
        public long RemainingBytes => MaxLoadedBytes - LoadedBytes;
        public string BudgetWarning { get; internal set; }
        public string RecoveryHint => BudgetWarning.Length == 0 ? "" :
            "Use live_snapshot before an explicit reload; verify complete/errors and explicitly Restore. No automatic replay.";
    }
}
#endif
