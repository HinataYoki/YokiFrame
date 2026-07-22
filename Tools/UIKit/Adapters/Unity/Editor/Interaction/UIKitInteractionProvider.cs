#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 Unity UIKit 的版本化只读 state 接入统一 Kit Interaction。</summary>
    internal sealed class UIKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string STATE = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.STATS,
                    YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.GET_WORKBENCH_SNAPSHOT,
                    YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.GET_EDITOR_CONTEXT,
                    YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.CREATE_PANEL_PREFAB,
                    YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.GENERATE_CODE_FOR_SELECTION,
                    YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.ADD_BIND_TO_SELECTION,
                    YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(
                    UIKitCommandHandler.KIT,
                    UIKitCommandHandler.REMOVE_BIND_FROM_SELECTION,
                    YokiFrameCommandKind.UserAction)
            });
        private readonly UIKitCommandHandler mHandler = new();

        /// <summary>获取稳定 UIKit 标识。</summary>
        public string Kit => UIKitCommandHandler.KIT;

        /// <summary>获取唯一的 state Snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取 Runtime 查询和显式 Unity Editor 工具命令目录。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取当前 Editor 会话内的 UIKit 单调可观察状态版本；读取不会创建 Root。</summary>
        public long StateVersion => UIKit.DiagnosticVersion;

        /// <summary>判断内部 handler 是否处理指定 UIKit 请求。</summary>
        /// <param name="request">待匹配请求。</param>
        /// <returns>Kit 与 action 均匹配时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);

        /// <summary>执行 UIKit 查询或显式 Editor UserAction 并返回终态结果。</summary>
        /// <param name="request">已通过宿主策略的命令。</param>
        /// <returns>只读状态或终态错误。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>创建唯一 UIKit state Snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>UIKit 完整有界只读状态。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Unsupported UIKit snapshot: " + snapshotName,
                    nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
