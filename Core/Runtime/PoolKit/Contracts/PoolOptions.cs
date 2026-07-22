using System;

namespace YokiFrame
{
    /// <summary>
    /// 对象池的预热和缓存容量配置。
    /// </summary>
    public readonly struct PoolOptions
    {
        /// <summary>
        /// C# 9 的默认 struct 值没有机会执行参数化构造，使用该标记把它稳定映射为默认容量策略。
        /// </summary>
        private readonly bool mHasExplicitValues;

        /// <summary>
        /// 保存经过构造函数校验的预热数量。
        /// </summary>
        private readonly int mInitialCount;

        /// <summary>
        /// 保存经过构造函数校验的最大缓存数量。
        /// </summary>
        private readonly int mMaxRetained;

        /// <summary>
        /// 表示缓存数量不设上限。
        /// </summary>
        public const int UNBOUNDED = -1;

        /// <summary>
        /// 获取不预热且不限制缓存数量的默认配置。
        /// </summary>
        public static PoolOptions Default { get; } = new(0, UNBOUNDED);

        /// <summary>
        /// 创建对象池配置，并验证预热数量不超过缓存容量。
        /// </summary>
        /// <param name="initialCount">预创建并缓存的对象数量。</param>
        /// <param name="maxRetained">最大缓存数量；-1 表示不限制。</param>
        public PoolOptions(int initialCount = 0, int maxRetained = UNBOUNDED)
        {
            if (initialCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount), "Initial count cannot be negative.");
            }

            if (maxRetained < UNBOUNDED)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetained), "Max retained must be -1 or greater.");
            }

            if (maxRetained >= 0 && initialCount > maxRetained)
            {
                throw new ArgumentException("Initial count cannot exceed max retained.", nameof(initialCount));
            }

            mInitialCount = initialCount;
            mMaxRetained = maxRetained;
            mHasExplicitValues = true;
        }

        /// <summary>
        /// 获取预创建并缓存的对象数量。
        /// </summary>
        public int InitialCount
        {
            get { return mHasExplicitValues ? mInitialCount : 0; }
        }

        /// <summary>
        /// 获取最大缓存数量；-1 表示不限制。
        /// </summary>
        public int MaxRetained
        {
            get { return mHasExplicitValues ? mMaxRetained : UNBOUNDED; }
        }
    }
}
