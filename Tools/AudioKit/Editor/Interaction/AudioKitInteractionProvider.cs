#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 AudioKit 只读状态接入统一 Kit Interaction。</summary>
    internal sealed class AudioKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "AudioKit";
        private const string STATE = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames = Array.AsReadOnly(new[] { STATE });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "stats", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly)
            });
        private readonly AudioKitCommandHandler mHandler = new();

        /// <summary>获取稳定 Kit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取固定 state Snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取固定的两个只读观察命令。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取 Runtime 工具状态单调版本。</summary>
        public long StateVersion => AudioKit.DiagnosticVersion;

        /// <summary>判断 handler 是否处理指定 AudioKit 请求。</summary>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);

        /// <summary>执行 AudioKit 请求并返回终态结果。</summary>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>创建固定 AudioKit state payload。</summary>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE, StringComparison.Ordinal))
                throw new ArgumentException("Unsupported AudioKit snapshot: " + snapshotName, nameof(snapshotName));
            return AudioKitSnapshotWriter.WriteWorkbench();
        }
    }
}
#endif
