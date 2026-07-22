#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>将 PoolDebugger 的真实状态和显式诊断操作接入统一 Kit Interaction。</summary>
    internal sealed class PoolKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "PoolKit";
        private const string STATE_SNAPSHOT = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE_SNAPSHOT });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "check_leak", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "set_tracking", YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(KIT, "clear_history", YokiFrameCommandKind.UserAction)
            });
        private readonly PoolKitCommandHandler mHandler = new();

        /// <summary>获取稳定 PoolKit 标识。</summary>
        public string Kit => KIT;
        /// <summary>获取 PoolKit 固定 state Snapshot。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;
        /// <summary>获取 PoolKit 两个只读和两个 UserAction 命令。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;
        /// <summary>获取 PoolDebugger 单调诊断版本。</summary>
        public long StateVersion => PoolDebugger.DiagnosticVersion;

        /// <summary>判断 PoolKit handler 是否处理指定命令。</summary>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);
        /// <summary>执行 PoolKit 命令并返回终态结果。</summary>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>创建 PoolKit 固定 state Snapshot。</summary>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported PoolKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
