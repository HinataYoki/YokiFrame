#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed partial class LiveCodeManager
    {
        /// <summary>把活动行为按脚本路径分组、编译并暂存。同一路径必须是同类同成员；编译不加载程序集。期间拒绝其他批量。</summary>
        /// <param name="requests">1 到 64 个行为导出请求。</param>
        /// <param name="batchId">批次标识；null 时生成 N 格式 GUID。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <returns>宿主暂存后的批次。</returns>
        public async Task<LiveExportBatch> ExportMany(IReadOnlyList<LiveExportRequest> requests,
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
            var versions = new List<LiveExportVersion>();
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, LiveExportVersion>(StringComparer.OrdinalIgnoreCase);
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
                        version = new LiveExportVersion
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
                if (versions.Count > LiveExportStore.MaxVersions) throw new ArgumentException("At most 16 distinct scripts.");
                foreach (var version in versions)
                    await Compile(version.Source, scope, epoch, guard, token, false);
                Check(scope, epoch, guard);
                token.ThrowIfCancellationRequested();
                return host.StageExports(batchId, versions);
            }
            finally { mBatchAttaching = false; }
        }

        /// <summary>按上一版导出和成员源码创建再导出并暂存。不加载程序集，期间拒绝其他批量。</summary>
        /// <param name="previousExportId">上一版导出标识。</param>
        /// <param name="members">新的成员源码，不能为空白。</param>
        /// <param name="batchId">批次标识；null 时生成。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <returns>只含这一版的暂存批次。</returns>
        public async Task<LiveExportBatch> Reexport(string previousExportId, string members,
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

        /// <summary>把已暂存批次排队提交。回调只复查当时的会话，不捕获提交脚本的可释放上下文。</summary>
        /// <param name="batchId">批次标识。</param>
        /// <param name="guard">提交前的宿主守卫。</param>
        public void CommitExport(string batchId, Action guard)
        {
            Guard(guard);
            var scope = ExportScope();
            int epoch = mEpoch;
            // The request is durable; do not bind its callback to the submitting script's disposed context.
            RequireExportHost().QueueExportCommit(batchId, () => Check(scope, epoch, () => { }));
        }

        /// <summary>要求当前宿主实现版本化导出。</summary>
        /// <returns>导出宿主。</returns>
        private ILiveExportHost RequireExportHost() => mHost as ILiveExportHost
            ?? throw new NotSupportedException("Versioned export is not supported by this host.");

        /// <summary>复制当前会话、代际和目标，供导出完成前复查。</summary>
        /// <returns>域状态副本。</returns>
        private RoslynDomainState ExportScope()
        {
            var state = mState();
            return new RoslynDomainState
            { SessionId = state.SessionId, Generation = state.Generation, ActiveTarget = state.ActiveTarget };
        }

        /// <summary>接受 N 格式 GUID；null 时生成新标识。其他文本拒绝。</summary>
        /// <param name="id">调用方批次标识。</param>
        /// <returns>可用的批次标识。</returns>
        private static string ValidateBatchId(string id)
        {
            if (id == null) return Guid.NewGuid().ToString("N");
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("batchId must be a 32 digit GUID.");
            return id;
        }
    }
}
#endif
