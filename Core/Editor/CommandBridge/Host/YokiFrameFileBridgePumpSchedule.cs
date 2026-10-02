#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING

namespace YokiFrame
{
    /// <summary>
    /// FileBridge 宿主共用的轮询时钟。Unity Editor、Godot Editor 和 Godot Runtime 只提供帧间隔，
    /// 心跳、命令和存储清理是否到期由这里统一判断，避免三处各自维护间隔。
    /// </summary>
    internal struct YokiFrameFileBridgePumpSchedule
    {
        /// <summary>心跳间隔。只证明宿主仍在线，不承担实时 Kit 状态。</summary>
        public const double HEARTBEAT_INTERVAL_SECONDS = 5.0d;

        /// <summary>命令目录轮询间隔。</summary>
        public const double COMMAND_POLL_INTERVAL_SECONDS = 0.2d;

        /// <summary>终态协议文件清理间隔。</summary>
        public const double STORAGE_CLEANUP_INTERVAL_SECONDS = 300.0d;

        private double mHeartbeatElapsed;
        private double mCommandPollElapsed;
        private double mStorageCleanupElapsed;

        /// <summary>推进一帧并返回本帧到期的阶段。调用方按心跳、清理、命令的顺序执行。</summary>
        /// <param name="deltaSeconds">本帧秒数。非法值按 0 处理，避免一次异常帧清空全部周期。</param>
        /// <returns>本帧需要执行的阶段。</returns>
        public Due Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                deltaSeconds = 0d;
            }

            mHeartbeatElapsed += deltaSeconds;
            mCommandPollElapsed += deltaSeconds;
            mStorageCleanupElapsed += deltaSeconds;
            Due due = new Due();
            if (mHeartbeatElapsed >= HEARTBEAT_INTERVAL_SECONDS)
            {
                mHeartbeatElapsed = 0d;
                due.RefreshHeartbeat = true;
            }

            if (mStorageCleanupElapsed >= STORAGE_CLEANUP_INTERVAL_SECONDS)
            {
                mStorageCleanupElapsed = 0d;
                due.PruneStorage = true;
            }

            if (mCommandPollElapsed >= COMMAND_POLL_INTERVAL_SECONDS)
            {
                mCommandPollElapsed = 0d;
                due.PollCommands = true;
            }

            return due;
        }

        /// <summary>一帧里到期的 FileBridge 维护阶段。</summary>
        internal struct Due
        {
            /// <summary>是否刷新心跳。</summary>
            public bool RefreshHeartbeat;

            /// <summary>是否回收终态协议文件。</summary>
            public bool PruneStorage;

            /// <summary>是否消费命令队列。</summary>
            public bool PollCommands;
        }
    }
}
#endif
