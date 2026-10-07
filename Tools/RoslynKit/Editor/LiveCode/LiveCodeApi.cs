#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public sealed class LiveCodeApi
    {
        private readonly LiveCodeManager mManager;
        private readonly Action mGuard;
        private readonly CancellationToken mToken;

        /// <summary>保存调用热更管理器时固定使用的守卫和取消令牌。</summary>
        /// <param name="manager">热更管理器。</param>
        /// <param name="guard">每次变更前执行的守卫。</param>
        /// <param name="token">异步操作的取消令牌。</param>
        internal LiveCodeApi(LiveCodeManager manager, Action guard, CancellationToken token)
        {
            mManager = manager;
            mGuard = guard;
            mToken = token;
        }

        /// <summary>用已绑定的守卫和取消令牌安装方法补丁。</summary>
        /// <param name="id">热更标识。</param>
        /// <param name="original">被补丁方法。</param>
        /// <param name="source">补丁源码。</param>
        /// <param name="patchType">补丁类型全名。</param>
        /// <param name="patchMethod">静态补丁方法名。</param>
        /// <param name="mode">prefix、postfix 或 replace，默认 replace。</param>
        /// <returns>补丁句柄任务。</returns>
        public Task<LiveCodeHandle> Patch(string id, MethodInfo original, string source,
            string patchType, string patchMethod, string mode = "replace") =>
            mManager.Patch(id, original, source, patchType, patchMethod, mode, mGuard, mToken);
        /// <summary>用已绑定的守卫和取消令牌把行为挂到目标。</summary>
        /// <param name="id">热更标识。</param>
        /// <param name="target">挂接对象。</param>
        /// <param name="className">行为类名。</param>
        /// <param name="members">行为成员源码。</param>
        /// <returns>行为句柄任务。</returns>
        public Task<LiveCodeHandle> Attach(string id, object target, string className, string members) =>
            mManager.Attach(id, target, className, members, mGuard, mToken);
        /// <summary>用已绑定的守卫和取消令牌批量挂接行为。</summary>
        /// <param name="requests">挂接请求。</param>
        /// <returns>批量结果任务。</returns>
        public Task<LiveBatchResult> AttachMany(IReadOnlyList<LiveAttachmentRequest> requests) =>
            mManager.AttachMany(requests, mGuard, mToken);
        /// <summary>用已绑定的守卫和取消令牌导出一个行为。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="outputPath">目标脚本路径。</param>
        /// <returns>导出标识任务。</returns>
        public Task<string> Export(string id, string outputPath) =>
            mManager.Export(id, outputPath, mGuard, mToken);
        /// <summary>用已绑定的守卫和取消令牌批量导出。批次标识可省略。</summary>
        /// <param name="requests">导出请求。</param>
        /// <param name="batchId">批次标识；null 时由管理器生成。</param>
        /// <returns>暂存批次任务。</returns>
        public Task<LiveExportBatch> ExportMany(IReadOnlyList<LiveExportRequest> requests, string batchId = null) =>
            mManager.ExportMany(requests, batchId, mGuard, mToken);
        /// <summary>用已绑定的守卫和取消令牌按上一版再导出。</summary>
        /// <param name="previousExportId">上一版导出标识。</param>
        /// <param name="members">新的成员源码。</param>
        /// <param name="batchId">批次标识；null 时由管理器生成。</param>
        /// <returns>暂存批次任务。</returns>
        public Task<LiveExportBatch> Reexport(string previousExportId, string members, string batchId = null) =>
            mManager.Reexport(previousExportId, members, batchId, mGuard, mToken);
        /// <summary>用已绑定的守卫排队提交导出批次。</summary>
        /// <param name="batchId">批次标识。</param>
        public void CommitExport(string batchId) => mManager.CommitExport(batchId, mGuard);
        /// <summary>用已绑定的守卫把导出绑定到目标。</summary>
        /// <param name="exportId">导出标识。</param>
        /// <param name="target">绑定目标；null 时由宿主解释。</param>
        /// <returns>宿主返回的绑定结果。</returns>
        public string Bind(string exportId, object target = null) => mManager.Bind(exportId, target, mGuard);
        /// <summary>用已绑定的守卫读取行为字段。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段值。</returns>
        public object ReadField(string id, string name) => mManager.ReadField(id, name, mGuard);
        /// <summary>用已绑定的守卫写入行为字段。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="name">字段名。</param>
        /// <param name="value">新值。</param>
        public void SetField(string id, string name, object value) => mManager.SetField(id, name, value, mGuard);
        /// <summary>用已绑定的守卫调用行为方法。重载匹配由宿主决定。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="method">方法名。</param>
        /// <param name="arguments">实参。</param>
        /// <returns>方法返回值。</returns>
        public object Invoke(string id, string method, params object[] arguments) =>
            mManager.Invoke(id, method, arguments, mGuard);
        /// <summary>先执行守卫，再列出当前句柄。</summary>
        /// <returns>句柄列表。</returns>
        public IReadOnlyList<LiveCodeHandle> List() { mGuard(); return mManager.List(); }
        /// <summary>先执行守卫，再移除热更。不存在时返回 false。</summary>
        /// <param name="id">热更标识。</param>
        /// <returns>是否移除了已有条目。</returns>
        public bool Remove(string id) { mGuard(); return mManager.Remove(id); }
        /// <summary>用已绑定的守卫捕获当前快照。</summary>
        /// <returns>新快照。</returns>
        public LiveSnapshot Snapshot() => mManager.Snapshot(mGuard);
        /// <summary>用已绑定的守卫读取已保存快照。</summary>
        /// <param name="snapshotId">快照标识。</param>
        /// <returns>快照。</returns>
        public LiveSnapshot ReadSnapshot(string snapshotId) => mManager.ReadSnapshot(snapshotId, mGuard);
        /// <summary>用已绑定的守卫和取消令牌恢复快照。运行时对象由解析器显式映射。</summary>
        /// <param name="snapshotId">快照标识。</param>
        /// <param name="sourceProvider">按条目提供源码。</param>
        /// <param name="resolver">运行时对象映射；持久对象可不使用。</param>
        /// <returns>恢复结果任务。</returns>
        public Task<LiveRestoreResult> Restore(string snapshotId,
            Func<LiveSnapshotEntry, string> sourceProvider,
            Func<LiveObjectIdentity, object> resolver = null) =>
            mManager.Restore(snapshotId, sourceProvider, resolver, mGuard, mToken);
    }
}
#endif
