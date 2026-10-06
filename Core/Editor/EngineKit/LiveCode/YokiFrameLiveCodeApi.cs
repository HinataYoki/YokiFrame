#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed class YokiFrameLiveCodeApi
    {
        private readonly YokiFrameLiveCodeManager mManager;
        private readonly Action mGuard;
        private readonly CancellationToken mToken;

        internal YokiFrameLiveCodeApi(YokiFrameLiveCodeManager manager, Action guard, CancellationToken token)
        {
            mManager = manager;
            mGuard = guard;
            mToken = token;
        }

        public Task<YokiFrameLiveCodeHandle> Patch(string id, MethodInfo original, string source,
            string patchType, string patchMethod, string mode = "replace") =>
            mManager.Patch(id, original, source, patchType, patchMethod, mode, mGuard, mToken);
        public Task<YokiFrameLiveCodeHandle> Attach(string id, object target, string className, string members) =>
            mManager.Attach(id, target, className, members, mGuard, mToken);
        public Task<YokiFrameLiveBatchResult> AttachMany(IReadOnlyList<YokiFrameLiveAttachmentRequest> requests) =>
            mManager.AttachMany(requests, mGuard, mToken);
        public Task<string> Export(string id, string outputPath) =>
            mManager.Export(id, outputPath, mGuard, mToken);
        public Task<YokiFrameLiveExportBatch> ExportMany(IReadOnlyList<YokiFrameLiveExportRequest> requests, string batchId = null) =>
            mManager.ExportMany(requests, batchId, mGuard, mToken);
        public Task<YokiFrameLiveExportBatch> Reexport(string previousExportId, string members, string batchId = null) =>
            mManager.Reexport(previousExportId, members, batchId, mGuard, mToken);
        public void CommitExport(string batchId) => mManager.CommitExport(batchId, mGuard);
        public string Bind(string exportId, object target = null) => mManager.Bind(exportId, target, mGuard);
        public object ReadField(string id, string name) => mManager.ReadField(id, name, mGuard);
        public void SetField(string id, string name, object value) => mManager.SetField(id, name, value, mGuard);
        public object Invoke(string id, string method, params object[] arguments) =>
            mManager.Invoke(id, method, arguments, mGuard);
        public IReadOnlyList<YokiFrameLiveCodeHandle> List() { mGuard(); return mManager.List(); }
        public bool Remove(string id) { mGuard(); return mManager.Remove(id); }
        public YokiFrameLiveSnapshot Snapshot() => mManager.Snapshot(mGuard);
        public YokiFrameLiveSnapshot ReadSnapshot(string snapshotId) => mManager.ReadSnapshot(snapshotId, mGuard);
        public Task<YokiFrameLiveRestoreResult> Restore(string snapshotId,
            Func<YokiFrameLiveSnapshotEntry, string> sourceProvider,
            Func<YokiFrameLiveObjectIdentity, object> resolver = null) =>
            mManager.Restore(snapshotId, sourceProvider, resolver, mGuard, mToken);
    }
}
#endif
