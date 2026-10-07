#if UNITY_EDITOR
using System;
using YokiFrame.Json;

namespace YokiFrame
{
    internal sealed class UnityLiveExportOperation : IRoslynOperation
    {
        private readonly UnityLiveCodeHost mHost;
        private readonly LiveCodeManager mManager;
        private readonly Func<bool> mPermitted;
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>
        /// 绑定导出宿主。live_export_status 是只读诊断且不看执行目标；
        /// 其它动作是危险操作，并把 permitted 记为执行许可。
        /// </summary>
        /// <param name="host">读取导出状态的宿主。</param>
        /// <param name="manager">提交导出的管理器。</param>
        /// <param name="action">live_export_status 或 live_export_commit。</param>
        /// <param name="permitted">受信任 C# 许可；状态查询不调用它。</param>
        public UnityLiveExportOperation(UnityLiveCodeHost host, LiveCodeManager manager, string action, Func<bool> permitted)
        {
            mHost = host; mManager = manager; mPermitted = permitted;
            bool read = action == "live_export_status";
            Descriptor = new RoslynOperationDescriptor(action,
                read ? YokiFrameCommandKind.ReadOnly : YokiFrameCommandKind.Dangerous,
                isDiagnostic: read, isTargetAgnostic: read,
                targets: RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play,
                executionPermission: read ? null : permitted);
        }
        /// <summary>
        /// 查询或提交导出。状态查询要求 batchId 与 exportId 恰好一个；
        /// 提交要求许可仍在且 confirmed 为 true，回调里会再查一次许可。
        /// 参数和许可失败都收成 LiveExportRejected，不向外抛。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>成功载荷或 LiveExportRejected。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            try
            {
                using var document = JsonDocument.Parse(request.PayloadJson);
                var root = document.RootElement;
                string batchId = root.TryGetProperty("batchId", out var batch) ? batch.GetString() : "";
                if (Descriptor.Action == "live_export_status")
                {
                    string exportId = root.TryGetProperty("exportId", out var item) ? item.GetString() : "";
                    if (string.IsNullOrEmpty(batchId) == string.IsNullOrEmpty(exportId))
                        throw new ArgumentException("Supply exactly one of batchId or exportId.");
                    return YokiFrameCommandResult.Success(mHost.ReadExportStatus(batchId, exportId));
                }
                if (!mPermitted()) throw new InvalidOperationException("Trusted C# permission is required.");
                if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                    throw new ArgumentException("Commit requires confirmed:true.");
                mManager.CommitExport(batchId, () =>
                {
                    if (!mPermitted()) throw new InvalidOperationException("Permission changed.");
                });
                return YokiFrameCommandResult.Success(new RoslynJsonBuilder().StartObject()
                    .Property("operation", "live_export_commit").Property("batchId", batchId)
                    .Property("observeAction", "live_export_status").EndObject().ToString());
            }
            catch (Exception error)
            {
                return YokiFrameCommandResult.Error("LiveExportRejected", error.Message);
            }
        }
    }
}
#endif
