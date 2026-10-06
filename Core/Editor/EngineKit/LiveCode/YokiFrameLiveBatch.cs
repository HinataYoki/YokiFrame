#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class YokiFrameLiveAttachmentRequest
    {
        public string Id { get; }
        public object Target { get; }
        public string ClassName { get; }
        public string Members { get; }
        public YokiFrameLiveAttachmentRequest(string id, object target, string className, string members)
        {
            Id = id; Target = target; ClassName = className; Members = members;
        }
    }

    public sealed class YokiFrameLiveBatchItem
    {
        public string Id { get; internal set; }
        public string Status { get; internal set; } = "notStarted";
        public string Error { get; internal set; } = "";
        public YokiFrameLiveCodeHandle Handle { get; internal set; }
    }

    public sealed class YokiFrameLiveBatchResult
    {
        public bool Success { get; internal set; }
        public string Stage { get; internal set; } = "validation";
        public string Error { get; internal set; } = "";
        public bool UserCodeMayHaveRun { get; internal set; }
        public int RequiredAssemblies { get; internal set; }
        public long RequiredBytes { get; internal set; }
        public YokiFrameRoslynBudgetStatus Budget { get; internal set; }
        internal readonly List<YokiFrameLiveBatchItem> mItems = new List<YokiFrameLiveBatchItem>();
        public IReadOnlyList<YokiFrameLiveBatchItem> Items => mItems.AsReadOnly();
    }

    public sealed class YokiFrameRoslynBudgetStatus
    {
        public int LoadedAssemblies { get; internal set; }
        public long LoadedBytes { get; internal set; }
        public int MaxAssemblies => YokiFrameRoslynLoadBudget.MaxAssemblies;
        public long MaxLoadedBytes => YokiFrameRoslynLoadBudget.MaxLoadedBytes;
        public int RemainingAssemblies => MaxAssemblies - LoadedAssemblies;
        public long RemainingBytes => MaxLoadedBytes - LoadedBytes;
        public string BudgetWarning { get; internal set; }
        public string RecoveryHint => BudgetWarning.Length == 0 ? "" :
            "Use live_snapshot before an explicit reload; verify complete/errors and explicitly Restore. No automatic replay.";
    }
}
#endif
