#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class LiveExportRequest
    {
        public string Id { get; }
        public string OutputPath { get; }
        /// <summary>保存一次导出请求。不校验标识或路径，校验留在导出流程。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="outputPath">目标脚本路径，可为 null。</param>
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
        /// <summary>捕获可持久化的导出目标。运行时临时引用应由实现拒绝。</summary>
        /// <param name="attachment">活动行为附件。</param>
        /// <returns>目标身份、场景和字段。</returns>
        LiveExportTarget CaptureExportTarget(IDisposable attachment);
        /// <summary>给持久化源码盖上导出标识。不写盘。</summary>
        /// <param name="source">宿主包装后的源码。</param>
        /// <param name="exportId">本版导出标识。</param>
        /// <returns>带版本标记的源码。</returns>
        string VersionExportSource(string source, string exportId);
        /// <summary>读取已暂存的导出版本。不执行用户代码。</summary>
        /// <param name="exportId">导出标识。</param>
        /// <returns>导出版本。</returns>
        LiveExportVersion ReadExport(string exportId);
        /// <summary>按上一版路径和类名创建再导出版本，并保留已有目标。不提交文件。</summary>
        /// <param name="previousExportId">上一版导出标识。</param>
        /// <param name="members">新的成员源码。</param>
        /// <param name="batchId">新批次标识。</param>
        /// <returns>尚未暂存的版本。</returns>
        LiveExportVersion CreateReexport(string previousExportId, string members, string batchId);
        /// <summary>把版本写入暂存区。已有持久类的新导出应由实现拒绝。</summary>
        /// <param name="batchId">批次标识。</param>
        /// <param name="versions">待暂存版本。</param>
        /// <returns>暂存后的批次。</returns>
        LiveExportBatch StageExports(string batchId, IReadOnlyList<LiveExportVersion> versions);
        /// <summary>在守卫通过后把批次排队到宿主空闲时提交。不得绑定会随脚本释放的上下文。</summary>
        /// <param name="batchId">批次标识。</param>
        /// <param name="guard">提交时复查会话的回调。</param>
        void QueueExportCommit(string batchId, Action guard);
    }
}
#endif
