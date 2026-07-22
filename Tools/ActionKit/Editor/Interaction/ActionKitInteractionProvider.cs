#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 ActionKit 运行状态和显式诊断操作接入统一 Kit Interaction。</summary>
    internal sealed class ActionKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "ActionKit";
        private const string STATE_SNAPSHOT = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE_SNAPSHOT });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "stats", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "set_stack_trace", YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(KIT, "clear_stack_trace", YokiFrameCommandKind.UserAction)
            });
        private readonly ActionKitCommandHandler mHandler = new();

        /// <summary>获取稳定 ActionKit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取固定 state Snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取两个只读与两个显式诊断命令。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取动作结构、终态和有界进度采样的单调版本。</summary>
        public long StateVersion => ActionKitScheduler.DiagnosticVersion;

        /// <summary>判断 ActionKit handler 是否处理指定命令。</summary>
        /// <param name="request">待匹配命令。</param>
        /// <returns>命令属于 ActionKit 时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);

        /// <summary>执行 ActionKit 命令并返回终态结果。</summary>
        /// <param name="request">已通过宿主策略的命令。</param>
        /// <returns>Provider 终态结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>创建 ActionKit 固定 state Snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>ActionKit 有界状态 JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Unsupported ActionKit snapshot: " + snapshotName,
                    nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
