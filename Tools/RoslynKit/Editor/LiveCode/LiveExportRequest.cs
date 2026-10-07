#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class LiveExportRequest
    {
        public string Id { get; }
        public string OutputPath { get; }
        public LiveExportRequest(string id, string outputPath) { Id = id; OutputPath = outputPath; }
    }

    public sealed class LiveExportTarget
    {
        public string TargetId { get; set; }
        public string ScenePath { get; set; }
        public string Fields { get; set; }
    }

    public sealed class LiveExportVersion
    {
        public string ExportId { get; set; }
        public string BatchId { get; set; }
        public string PreviousExportId { get; set; } = "";
        public string ClassName { get; set; }
        public string SourcePath { get; set; }
        public string Source { get; set; }
        public string SourceHash { get; set; }
        public string PreviousMetaHash { get; set; } = "";
        public List<LiveExportTarget> Targets { get; } = new List<LiveExportTarget>();
    }

    public sealed class LiveExportBatch
    {
        public string BatchId { get; set; }
        public string State { get; set; } = "Prepared";
        public string Error { get; set; } = "";
        public List<string> ExportIds { get; } = new List<string>();
    }

    /// <summary>Optional persistent export adapter; unsupported hosts retain their existing API.</summary>
    public interface ILiveExportHost
    {
        LiveExportTarget CaptureExportTarget(IDisposable attachment);
        string VersionExportSource(string source, string exportId);
        LiveExportVersion ReadExport(string exportId);
        LiveExportVersion CreateReexport(string previousExportId, string members, string batchId);
        LiveExportBatch StageExports(string batchId, IReadOnlyList<LiveExportVersion> versions);
        void QueueExportCommit(string batchId, Action guard);
    }
}
#endif
