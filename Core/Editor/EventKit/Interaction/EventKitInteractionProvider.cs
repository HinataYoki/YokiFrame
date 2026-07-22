#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 把 EventKit Runtime 诊断快照接入统一 Kit Interaction，不引入宿主或代码扫描依赖。
    /// </summary>
    internal sealed class EventKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "EventKit";
        private const string STATE_SNAPSHOT = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE_SNAPSHOT });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(
                    KIT,
                    "get_workbench_snapshot",
                    YokiFrameCommandKind.ReadOnly)
            });

        private readonly EventKitCommandHandler mHandler = new();

        /// <summary>创建 EventKit Provider，并确保 Runtime hook 已安装。</summary>
        internal EventKitInteractionProvider()
        {
            EventKitDiagnosticRegistry.EnsureInitialized();
        }

        /// <summary>获取稳定 EventKit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取 EventKit 当前提供的 state Snapshot。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取 EventKit 唯一的只读 Workbench 命令。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取 EventKit Runtime 活动的单调版本。</summary>
        public long StateVersion => EventKitDiagnosticRegistry.StateVersion;

        /// <summary>判断 EventKit handler 是否处理指定命令。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>Kit/action 匹配时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return mHandler.CanHandle(request);
        }

        /// <summary>执行 EventKit 只读命令并返回终态结果。</summary>
        /// <param name="request">已匹配命令请求。</param>
        /// <returns>EventKit handler 结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return mHandler.Handle(request);
        }

        /// <summary>创建 EventKit Runtime state Snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>EventKit Workbench JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported EventKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
