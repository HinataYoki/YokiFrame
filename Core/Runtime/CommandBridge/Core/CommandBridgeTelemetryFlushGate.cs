using System;

namespace YokiFrame
{
    /// <summary>
    /// 面向 Adapter 遥测发布器的引擎无关 dirty/节流门控。
    /// </summary>
    public sealed class CommandBridgeTelemetryFlushGate
    {
        private const double TIME_COMPARISON_EPSILON = 0.000001d;

        private readonly double mIntervalSeconds;
        private bool mDirty;
        private double mNextFlushAtSeconds;

        /// <summary>
        /// 创建 dirty/节流门控。
        /// </summary>
        /// <param name="intervalSeconds">请求后最短刷新间隔，单位秒。</param>
        public CommandBridgeTelemetryFlushGate(double intervalSeconds)
        {
            if (intervalSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));

            mIntervalSeconds = intervalSeconds;
        }

        /// <summary>
        /// 获取当前是否存在待刷新变更。
        /// </summary>
        public bool IsDirty => mDirty;

        /// <summary>
        /// 请求在指定时间之后刷新。
        /// </summary>
        /// <param name="nowSeconds">当前时间，单位秒。</param>
        public void Request(double nowSeconds)
        {
            if (!mDirty)
                mNextFlushAtSeconds = nowSeconds + mIntervalSeconds;

            mDirty = true;
        }

        /// <summary>
        /// 在刷新到期时消费 dirty 标记。
        /// </summary>
        /// <param name="nowSeconds">当前时间，单位秒。</param>
        /// <returns>成功消费并应执行刷新时返回 true，否则返回 false。</returns>
        public bool ConsumeIfDue(double nowSeconds)
        {
            if (!mDirty)
                return false;
            if (nowSeconds + TIME_COMPARISON_EPSILON < mNextFlushAtSeconds)
                return false;

            Clear();
            return true;
        }

        /// <summary>
        /// 清空 dirty 标记和下一次刷新时间。
        /// </summary>
        public void Clear()
        {
            mDirty = false;
            mNextFlushAtSeconds = 0d;
        }
    }
}
