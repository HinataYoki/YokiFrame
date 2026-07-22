#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>提供 AudioKit 的只读状态命令。</summary>
    internal sealed class AudioKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT = "AudioKit";
        private static readonly string[] sSupportedActions =
        {
            "stats", "get_workbench_snapshot"
        };

        /// <summary>创建固定 AudioKit 只读 action 目录的 handler。</summary>
        internal AudioKitCommandHandler() : base(KIT, sSupportedActions) { }

        /// <summary>创建当前 AudioKit 完整有界状态。</summary>
        internal string CreateWorkbenchSnapshot() => AudioKitSnapshotWriter.WriteWorkbench();

        /// <summary>执行已声明的只读请求，绝不改变 Runtime 音频状态。</summary>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                string payload = string.Equals(request.Action, "stats", StringComparison.Ordinal)
                    ? AudioKitSnapshotWriter.WriteStats()
                    : CreateWorkbenchSnapshot();
                return YokiFrameCommandResult.Success(payload);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("AudioKitCommandFailed", exception.Message);
            }
        }
    }
}
#endif
