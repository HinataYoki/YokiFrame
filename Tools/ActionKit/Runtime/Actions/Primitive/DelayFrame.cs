using System;

namespace YokiFrame
{
    /// <summary>
    /// 按 ActionKit 实际推进次数等待指定帧数，不依赖全局 int 截止值。
    /// </summary>
    internal sealed class DelayFrame : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<DelayFrame> sPool = PoolKit.Create(
            static () => new DelayFrame(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private int mTargetFrameCount;
        private int mElapsedFrameCount;
        private Action mOnDelayFinish;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private DelayFrame() { }

        /// <summary>
        /// 分配一个帧延迟；零或负数在首次 dt=0 推进中立即完成。
        /// </summary>
        /// <param name="frameCount">需要跨过的调度帧数。</param>
        /// <param name="onDelayFinish">正常完成时调用的回调。</param>
        /// <returns>新的帧延迟租约。</returns>
        internal static DelayFrame Allocate(int frameCount, Action onDelayFinish = null)
        {
            DelayFrame action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            action.mTargetFrameCount = frameCount;
            action.mElapsedFrameCount = 0;
            action.mOnDelayFinish = onDelayFinish;
            return action;
        }

        /// <summary>重置当前轮次已经跨过的帧数。</summary>
        public override void OnInit()
        {
            base.OnInit();
            mElapsedFrameCount = 0;
        }

        /// <summary>在零或负目标下立即完成，正目标等待后续真实 Tick。</summary>
        public override void OnStart()
        {
            if (mTargetFrameCount <= 0) this.Finish();
        }

        /// <summary>每次被实际推进时累计一帧并检查目标。</summary>
        public override void OnExecute(float dt)
        {
            mElapsedFrameCount++;
            if (mElapsedFrameCount >= mTargetFrameCount) this.Finish();
        }

        /// <summary>仅在正常完成时调用帧延迟回调。</summary>
        public override void OnFinish() => mOnDelayFinish?.Invoke();

        /// <summary>释放完成回调。</summary>
        public override void OnDeinit() => mOnDelayFinish = null;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建帧进度按需诊断文本。</summary>
        public override string GetDebugInfo() => "DelayFrame(" + mElapsedFrameCount + "/" + mTargetFrameCount + ")";
#endif

        /// <summary>把已释放实例归还 DelayFrame 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>回池前清除回调和帧数状态。</summary>
        private void ResetForPool()
        {
            mTargetFrameCount = 0;
            mElapsedFrameCount = 0;
            mOnDelayFinish = null;
        }
    }
}
