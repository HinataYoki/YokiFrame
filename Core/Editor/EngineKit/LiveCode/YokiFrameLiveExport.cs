#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class YokiFrameLiveExportRequest
    {
        public string Id { get; }
        public string OutputPath { get; }
        public YokiFrameLiveExportRequest(string id, string outputPath) { Id = id; OutputPath = outputPath; }
    }

    public sealed class YokiFrameLiveExportTarget
    {
        public string TargetId { get; set; }
        public string ScenePath { get; set; }
        public string Fields { get; set; }
    }

    public sealed class YokiFrameLiveExportVersion
    {
        public string ExportId { get; set; }
        public string BatchId { get; set; }
        public string PreviousExportId { get; set; } = "";
        public string ClassName { get; set; }
        public string SourcePath { get; set; }
        public string Source { get; set; }
        public string SourceHash { get; set; }
        public string PreviousMetaHash { get; set; } = "";
        public List<YokiFrameLiveExportTarget> Targets { get; } = new List<YokiFrameLiveExportTarget>();
    }

    public sealed class YokiFrameLiveExportBatch
    {
        public string BatchId { get; set; }
        public string State { get; set; } = "Prepared";
        public string Error { get; set; } = "";
        public List<string> ExportIds { get; } = new List<string>();
    }

    /// <summary>Optional persistent export adapter; unsupported hosts retain their existing API.</summary>
    public interface IYokiFrameLiveExportHost
    {
        YokiFrameLiveExportTarget CaptureExportTarget(IDisposable attachment);
        string VersionExportSource(string source, string exportId);
        YokiFrameLiveExportVersion ReadExport(string exportId);
        YokiFrameLiveExportVersion CreateReexport(string previousExportId, string members, string batchId);
        YokiFrameLiveExportBatch StageExports(string batchId, IReadOnlyList<YokiFrameLiveExportVersion> versions);
        void QueueExportCommit(string batchId, Action guard);
    }
}
#endif
