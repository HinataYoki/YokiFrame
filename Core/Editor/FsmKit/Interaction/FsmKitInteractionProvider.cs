#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 把 FsmKit 已有诊断入口接入通用 Kit Interaction，不复制状态机注册表或 JSON 逻辑。
    /// </summary>
    internal sealed class FsmKitInteractionProvider :
        IYokiFrameVersionedKitInteractionProvider,
        IYokiFrameVersionedNamedTelemetryProvider
    {
        private const string KIT = "FsmKit";
        private const string STATE_SNAPSHOT = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE_SNAPSHOT });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "list_all", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_state", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_history", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_state_events", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly)
            });

        private readonly FsmKitCommandHandler mHandler = new();

        /// <summary>获取稳定 FsmKit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取 FsmKit 当前提供的状态 Snapshot。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取 FsmKit 当前提供的只读 Command。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取 FsmKit 诊断事实的单调版本。</summary>
        public long StateVersion => FsmKitRegistry.StateVersion;

        /// <summary>获取当前活动 instanceId，作为命名 Telemetry latest frame 名称。</summary>
        public IReadOnlyList<string> TelemetryNames => FsmKitRegistry.GetInstanceIds();

        /// <summary>判断 FsmKit handler 是否处理指定命令。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>Kit/action 匹配时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return mHandler.CanHandle(request);
        }

        /// <summary>执行 FsmKit 只读命令并返回终态结果。</summary>
        /// <param name="request">已匹配命令请求。</param>
        /// <returns>FsmKit handler 结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return mHandler.Handle(request);
        }

        /// <summary>创建 FsmKit Workbench 聚合状态 Snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>FsmKit 自有 schema 的聚合 JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported FsmKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        /// <summary>创建一个活动 FSM 实例的完整 Shared Memory latest-frame payload。</summary>
        /// <param name="name">FsmKit 注册表生成的稳定 instanceId。</param>
        /// <returns>与标准 FsmKit/state 相同 schema、但精确选择该实例的 JSON。</returns>
        public string CreateTelemetry(string name)
        {
            return mHandler.CreateWorkbenchSnapshot(name);
        }

        /// <summary>获取指定 FSM 实例命名 Telemetry 的单调版本，不创建完整快照。</summary>
        /// <param name="name">FsmKit 注册表生成的稳定 instanceId。</param>
        /// <returns>活动实例的当前版本；实例已经失效或不存在时返回零。</returns>
        public long GetTelemetryVersion(string name)
        {
            return FsmKitRegistry.GetInstanceVersion(name);
        }
    }
}
#endif
