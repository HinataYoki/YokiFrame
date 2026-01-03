using System;
using UnityEngine;

namespace YokiFrame
{
    internal class DelayFrame : ActionBase
    {
        /// <summary>
        /// 延迟开始帧
        /// </summary>
        private int mStartFrameCount;
        /// <summary>
        /// 延迟帧
        /// </summary>
        private int mDelayedFrameCount;
        /// <summary>
        /// 延迟结束回调
        /// </summary>
        private Action mOnDelayFinish;
        /// <summary>
        /// 延迟帧任务池
        /// </summary>
        private static readonly SimplePoolKit<DelayFrame> mPool = new(() => new DelayFrame());

        public static DelayFrame Allocate(int frameCount, Action onDelayFinish = null)
        {
            var delayFrame = mPool.Allocate();
            delayFrame.ActionID = ActionKit.ID_GENERATOR++;
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
            mStartFrameCount = Time.frameCount;
            Delay();
        }

        public override void OnExecute(float dt) => Delay();

        private void Delay()
        {
            if (Time.frameCount >= mStartFrameCount + mDelayedFrameCount)
            {
                mOnDelayFinish?.Invoke();
                this.Finish();
            }
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mOnDelayFinish = null;
                MonoRecycler.AddRecycleCallback(new ActionRecycler<DelayFrame>(mPool, this));
            }
        }

        public override string GetDebugInfo() => 
            mOnDelayFinish != null ? $"DelayFrame -> {mOnDelayFinish.Method.DeclaringType}.{mOnDelayFinish.Method.Name}" : "DelayFrame";
    }

    public static class DelayFrameExtension
    {
        public static ISequence DelayFrame(this ISequence self, int frameCount, Action onDelayFinish = null)
        {
            return self.Append(YokiFrame.DelayFrame.Allocate(frameCount, onDelayFinish));
        }

        public static ISequence NextFrame(this ISequence self, Action onDelayFinish = null)
        {
            return self.Append(YokiFrame.DelayFrame.Allocate(1, onDelayFinish));
        }
    }
}