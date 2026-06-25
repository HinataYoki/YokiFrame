using System;

namespace YokiFrame
{
    internal class DelayFrame : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<DelayFrame> sPool = new(static () => new DelayFrame());

        private int mStartFrameCount;
        private int mDelayedFrameCount;
        private Action mOnDelayFinish;

        static DelayFrame()
        {
            ActionKitScheduler.RegisterRecycleProcessor<DelayFrame>();
        }

        internal static DelayFrame Allocate(int frameCount, Action onDelayFinish = null)
        {
            var delayFrame = sPool.Allocate();
            delayFrame.ActionID = ActionKit.sIdGenerator++;
            delayFrame.OnInit();
            delayFrame.Deinited = false;
            delayFrame.mDelayedFrameCount = frameCount;
            delayFrame.mOnDelayFinish = onDelayFinish;
            return delayFrame;
        }

        public override void OnInit()
        {
            base.OnInit();
            mStartFrameCount = 0;
        }

        public override void OnStart()
        {
            mStartFrameCount = ActionKitScheduler.FrameCount;
            CheckDelay();
        }

        public override void OnExecute(float dt) => CheckDelay();

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            mOnDelayFinish = null;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<DelayFrame>(sPool, this));
        }

        public override string GetDebugInfo() =>
            mOnDelayFinish != null ? $"DelayFrame -> {mOnDelayFinish.Method.DeclaringType}.{mOnDelayFinish.Method.Name}" : "DelayFrame";

        private void CheckDelay()
        {
            if (ActionKitScheduler.FrameCount < mStartFrameCount + mDelayedFrameCount) return;

            mOnDelayFinish?.Invoke();
            this.Finish();
        }
    }
}
