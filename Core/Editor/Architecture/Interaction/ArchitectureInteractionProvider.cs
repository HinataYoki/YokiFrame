#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 把 Architecture 注册表接入统一 Kit Interaction，不在宿主层增加专用分支。
    /// </summary>
    internal sealed class ArchitectureInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "Architecture";
        private const string STATE_SNAPSHOT = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE_SNAPSHOT });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "list_architectures", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly)
            });

        private readonly ArchitectureCommandHandler mHandler = new();

        /// <summary>获取稳定 Architecture Kit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取 Architecture 当前提供的状态 Snapshot。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取 Architecture 当前提供的只读 Command。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取 Architecture 注册表版本，供宿主在服务状态变化后即时刷新 Telemetry。</summary>
        public long StateVersion => ArchitectureRegistry.DiagnosticVersion;

        /// <summary>判断 Architecture handler 是否处理指定命令。</summary>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);

        /// <summary>执行 Architecture 只读命令并返回终态结果。</summary>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>
        /// 创建 Architecture 工作台状态 Snapshot。
        /// </summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>Architecture 自有 schema 的聚合 JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported Architecture snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.HandleAction("get_workbench_snapshot");
        }
    }
}
#endif
