using System;
using UnityEngine;

namespace YokiFrame
{
    internal class DelayFrame : IAction
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
        private static readonly SimpleObjectPool<DelayFrame> delayFramePool = new(() => new DelayFrame());

        public static DelayFrame Allocate(int frameCount, Action onDelayFinish = null)
        {
            var delayFrame = delayFramePool.Allocate();
            delayFrame.ActionID = ActionKit.ID_GENERATOR++;
            delayFrame.OnInit();
            delayFrame.Deinited = false;
            delayFrame.mDelayedFrameCount = frameCount;
            delayFrame.mOnDelayFinish = onDelayFinish;

            return delayFrame;
        }

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }

        public void OnInit()
        {
            ActionState = ActionStatus.NotStart;
            Paused = false;
            mStartFrameCount = 0;
        }

        public void OnStart()
        {
            mStartFrameCount = Time.frameCount;
            Delay();
        }

        public void OnExecute(float dt) => Delay();

        private void Delay()
        {
            if (Time.frameCount >= mStartFrameCount + mDelayedFrameCount)
            {
                mOnDelayFinish?.Invoke();
                this.Finish();
            }
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mOnDelayFinish = null;
                MonoRecycler.AddRecycleCallback(new ActionRecycler<DelayFrame>(delayFramePool, this));
            }
        }

        string IAction.LogError() => $"类 {mOnDelayFinish.Method.DeclaringType} 方法 {mOnDelayFinish.Method} 出错";
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