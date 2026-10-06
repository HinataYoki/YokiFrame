#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed partial class YokiFrameLiveCodeManager
    {
        public async Task<YokiFrameLiveExportBatch> ExportMany(IReadOnlyList<YokiFrameLiveExportRequest> requests,
            string batchId, Action guard, CancellationToken token)
        {
            Guard(guard);
            var host = RequireExportHost();
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before exporting.");
            if (requests == null || requests.Count == 0 || requests.Count > 64)
                throw new ArgumentException("ExportMany requires 1..64 behaviour requests.");
            batchId = ValidateBatchId(batchId);
            var scope = ExportScope();
            int epoch = mEpoch;
            var versions = new List<YokiFrameLiveExportVersion>();
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, YokiFrameLiveExportVersion>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            mBatchAttaching = true;
            try
            {
                foreach (var request in requests)
                {
                    if (request == null || !ids.Add(request.Id)) throw new ArgumentException("Duplicate export ID.");
                    var entry = RequireBehaviour(request.Id, guard);
                    if (!mHost.IsAlive(entry.Attachment)) throw new InvalidOperationException("Behaviour is unavailable: " + request.Id);
                    string path = request.OutputPath == null ? "" : request.OutputPath.Replace('\\', '/');
                    string source = mHost.WrapBehaviour(entry.ClassName, entry.Members, true);
                    if (!groups.TryGetValue(path, out var version))
                    {
                        version = new YokiFrameLiveExportVersion
                        {
                            ExportId = Guid.NewGuid().ToString("N"), BatchId = batchId,
                            ClassName = entry.ClassName, SourcePath = path
                        };
                        version.Source = host.VersionExportSource(source, version.ExportId);
                        version.SourceHash = Hash(version.Source);
                        groups.Add(path, version); sources.Add(path, source); versions.Add(version);
                    }
                    else if (version.ClassName != entry.ClassName || sources[path] != source)
                        throw new ArgumentException("Shared script paths require identical class and member source.");
                    version.Targets.Add(host.CaptureExportTarget(entry.Attachment));
                }
                if (versions.Count > YokiFrameLiveExportStore.MaxVersions) throw new ArgumentException("At most 16 distinct scripts.");
                foreach (var version in versions)
                    await Compile(version.Source, scope, epoch, guard, token, false);
                Check(scope, epoch, guard);
                token.ThrowIfCancellationRequested();
                return host.StageExports(batchId, versions);
            }
            finally { mBatchAttaching = false; }
        }

        public async Task<YokiFrameLiveExportBatch> Reexport(string previousExportId, string members,
            string batchId, Action guard, CancellationToken token)
        {
            Guard(guard);
            var host = RequireExportHost();
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before exporting.");
            if (string.IsNullOrWhiteSpace(members)) throw new ArgumentException("Members must be nonempty.");
            var scope = ExportScope();
            int epoch = mEpoch;
            mBatchAttaching = true;
            try
            {
                var version = host.CreateReexport(previousExportId, members, ValidateBatchId(batchId));
                await Compile(version.Source, scope, epoch, guard, token, false);
                Check(scope, epoch, guard);
                token.ThrowIfCancellationRequested();
                return host.StageExports(version.BatchId, new[] { version });
            }
            finally { mBatchAttaching = false; }
        }

        public void CommitExport(string batchId, Action guard)
        {
            Guard(guard);
            var scope = ExportScope();
            int epoch = mEpoch;
            // The request is durable; do not bind its callback to the submitting script's disposed context.
            RequireExportHost().QueueExportCommit(batchId, () => Check(scope, epoch, () => { }));
        }

        private IYokiFrameLiveExportHost RequireExportHost() => mHost as IYokiFrameLiveExportHost
            ?? throw new NotSupportedException("Versioned export is not supported by this host.");

        private YokiFrameEngineDomainState ExportScope()
        {
            var state = mState();
            return new YokiFrameEngineDomainState
            { SessionId = state.SessionId, Generation = state.Generation, ActiveTarget = state.ActiveTarget };
        }

        private static string ValidateBatchId(string id)
        {
            if (id == null) return Guid.NewGuid().ToString("N");
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("batchId must be a 32 digit GUID.");
            return id;
        }
    }
}
#endif
