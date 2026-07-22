#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>将 ResKit 有界状态和受控诊断命令接入统一 Kit Interaction。</summary>
    internal sealed class ResKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "ResKit";
        private const string STATE = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "stats", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "list_resources", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_resource_detail", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "diagnose_resource", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_unload_history", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "clear_history", YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(KIT, "set_tracking", YokiFrameCommandKind.UserAction)
            });
        private readonly ResKitCommandHandler mHandler = new();

        /// <summary>获取稳定 ResKit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取唯一且有界的 state Snapshot。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取六个只读和两个 UserAction 命令。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>O(1) 获取 ResKit 单调诊断版本。</summary>
        public long StateVersion => ResKit.DiagnosticVersion;

        /// <summary>判断 ResKit handler 是否处理指定请求。</summary>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);

        /// <summary>执行已允许的 ResKit 命令。</summary>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>创建唯一 state Snapshot，未知名称会明确拒绝。</summary>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported ResKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
