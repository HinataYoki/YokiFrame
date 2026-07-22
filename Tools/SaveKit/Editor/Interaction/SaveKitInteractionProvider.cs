#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 SaveKit 无 payload 的只读状态接入统一 Tool Interaction catalog。</summary>
    internal sealed class SaveKitInteractionProvider : IYokiFrameSnapshotVersionedKitInteractionProvider
    {
        private const string KIT = "SaveKit";
        private const string STATE = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "stats", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly)
            });
        private readonly SaveKitCommandHandler mHandler = new();

        /// <summary>获取稳定 SaveKit 标识。</summary>
        public string Kit { get { return KIT; } }

        /// <summary>获取固定 state Snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames { get { return sSnapshotNames; } }

        /// <summary>获取仅用于 FileBridge Snapshot 增量写入的 SaveKit 状态版本。</summary>
        public long StateVersion { get { return SaveKit.InteractionStateVersion; } }

        /// <summary>获取仅包含两个查询入口的命令描述。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands { get { return sCommands; } }

        /// <summary>判断请求是否属于当前 SaveKit 只读命令表。</summary>
        /// <param name="request">待匹配的命令请求。</param>
        /// <returns>匹配当前 Kit/action 时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return mHandler.CanHandle(request);
        }

        /// <summary>执行 SaveKit 只读命令并返回终态响应。</summary>
        /// <param name="request">已通过宿主策略的请求。</param>
        /// <returns>固定 schema 的成功或错误结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return mHandler.Handle(request);
        }

        /// <summary>创建 SaveKit state Snapshot，拒绝未知名称避免伪造在线状态。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>不包含存档 payload 的有界 JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported SaveKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
