using System;

namespace YokiFrame
{
    /// <summary>
    /// 按秒延迟执行的 Action。
    /// </summary>
    public class Delay : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<Delay> sPool = new(static () => new Delay());

        /// <summary>
        /// 延迟秒数。
        /// </summary>
        public float DelayTime;

        /// <summary>
        /// 延迟完成时调用的回调。
        /// </summary>
        public Action OnDelayFinish { get; set; }

        /// <summary>
        /// 当前已累计的秒数。
        /// </summary>
        public float CurrentSeconds { get; set; }

        static Delay()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Delay>();
        }

        /// <summary>
        /// 从池中分配延迟 Action。
        /// </summary>
        /// <param name="delayTime">延迟秒数。</param>
        /// <param name="onDelayFinish">延迟完成时调用的回调。</param>
        public static Delay Allocate(float delayTime, Action onDelayFinish = null)
        {
            var delay = sPool.Allocate();
            delay.ActionID = ActionKit.sIdGenerator++;
            delay.Deinited = false;
            delay.OnInit();
            delay.DelayTime = delayTime;
            delay.OnDelayFinish = onDelayFinish;
            delay.CurrentSeconds = 0.0f;
            return delay;
        }

        /// <summary>
        /// 初始化延迟 Action 状态。
        /// </summary>
        public override void OnInit()
        {
            base.OnInit();
            CurrentSeconds = 0.0f;
        }

        /// <summary>
        /// 首次执行时立即推进一次。
        /// </summary>
        public override void OnStart() => OnExecute(0);

        /// <summary>
        /// 推进延迟计时。
        /// </summary>
        /// <param name="dt">本次更新的时间步长。</param>
        public override void OnExecute(float dt)
        {
            CurrentSeconds += dt;
            if (CurrentSeconds < DelayTime) return;

            this.Finish();
            OnDelayFinish?.Invoke();
        }

        /// <summary>
        /// 释放延迟 Action 状态并回收。
        /// </summary>
        public override void OnDeinit()
        {
            if (Deinited) return;

            OnDelayFinish = null;
            Deinited = true;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Delay>(sPool, this));
        }

        /// <summary>
        /// 返回用于调试面板展示的简短信息。
        /// </summary>
        public override string GetDebugInfo() => $"Delay({DelayTime:F1}s, {CurrentSeconds:F1}s elapsed)";
    }
}
