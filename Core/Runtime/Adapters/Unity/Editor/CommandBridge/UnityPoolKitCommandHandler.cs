#if !GODOT
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity Editor 侧 PoolKit 命令包装器，负责保存编辑器监控偏好。
    /// </summary>
    internal sealed class UnityPoolKitCommandHandler : IKitCommandHandler
    {
        private const string SET_TRACKING_ACTION = "set_tracking";

        private readonly PoolKitCommandHandler mInner = new();

        public string KitName => mInner.KitName;

        public string[] SupportedActions => mInner.SupportedActions;

        public string HandleAction(string action, string payloadJson)
        {
            if (!string.Equals(action, SET_TRACKING_ACTION, StringComparison.Ordinal))
                return mInner.HandleAction(action, payloadJson);

            return KitStateSnapshotPublisher.ApplyPoolTrackingCommand(payloadJson);
        }
    }
}
#endif
