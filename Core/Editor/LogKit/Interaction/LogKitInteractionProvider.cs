#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 将 LogKit 的真实 Core 状态、显式文件预览和用户操作接入统一 Kit Interaction。
    /// </summary>
    internal sealed class LogKitInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "LogKit";
        private const string STATE_SNAPSHOT = "state";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE_SNAPSHOT });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, "get_workbench_snapshot", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "read_log_file", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(KIT, "set_settings", YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(KIT, "reset_settings", YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor(KIT, "clear_history", YokiFrameCommandKind.UserAction)
            });

        private readonly LogKitCommandHandler mHandler = new LogKitCommandHandler();

        /// <summary>获取稳定 LogKit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取 LogKit 当前提供的 state Snapshot。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取 LogKit 当前提供的两个只读和三个 UserAction 命令。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>合并日志、设置和宿主能力的单调版本，任一事实变化都会触发实时发布。</summary>
        public long StateVersion => unchecked(
            LogKit.DiagnosticVersion
            + LogKitSettings.SettingsVersion
            + LogKitHostEnvironment.Version);

        /// <summary>判断 LogKit handler 是否处理指定命令。</summary>
        public bool CanHandle(YokiFrameCommandRequest request) => mHandler.CanHandle(request);

        /// <summary>执行 LogKit 命令并返回终态结果。</summary>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request) => mHandler.Handle(request);

        /// <summary>创建 LogKit 固定 state Snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>LogKit Workbench JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported LogKit snapshot: " + snapshotName, nameof(snapshotName));
            }

            return mHandler.CreateWorkbenchSnapshot();
        }
    }
}
#endif
