#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 SpatialKit 只读状态接入统一 Tool Interaction catalog。</summary>
    internal sealed class SpatialKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "SpatialKit";
        private const string STATE = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "stats", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "list_indexes", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "density", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "analyze", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly)
            });
        private readonly SpatialKitCommandHandler mHandler = new SpatialKitCommandHandler();

        /// <summary>获取稳定 Kit 标识。</summary>
        public string Kit { get { return KIT; } }

        /// <summary>获取固定 state snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames { get { return sSnapshotNames; } }

        /// <summary>获取 SpatialKit 只读命令描述。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands { get { return sCommands; } }

        /// <summary>获取空间索引状态单调版本。</summary>
        public long StateVersion { get { return SpatialKit.GetDiagnosticsVersion(); } }

        /// <summary>判断请求是否属于 SpatialKit 当前命令表。</summary>
        /// <param name="request">待匹配请求。</param>
        /// <returns>匹配时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return mHandler.CanHandle(request);
        }

        /// <summary>执行 SpatialKit 只读命令。</summary>
        /// <param name="request">已通过宿主策略的请求。</param>
        /// <returns>命令终态结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return mHandler.Handle(request);
        }

        /// <summary>创建 SpatialKit state snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>SpatialKit state JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported SpatialKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
