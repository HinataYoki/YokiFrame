using System;

namespace YokiFrame
{
    public class Delay : ActionBase
    {
        /// <summary>
        /// 延迟时间
        /// </summary>
        public float DelayTime;
        /// <summary>
        /// 延迟回调
        /// </summary>
        public Action OnDelayFinish { get; set; }
        /// <summary>
        /// 当前延迟的时间
        /// </summary>
        public float CurrentSeconds { get; set; }
        /// <summary>
        /// 延迟任务池
        /// </summary>
        private static readonly SimplePoolKit<Delay> mPool = new(() => new Delay());
        
        /// <summary>
        /// 静态构造函数 - 注册回收处理器
        /// </summary>
        static Delay()
        {
            ActionKitPlayerLoopSystem.RegisterRecycleProcessor<Delay>();
        }

        public static Delay Allocate(float delayTime, Action onDelayFinish = null)
        {
            var delay = mPool.Allocate();
            delay.ActionID = ActionKit.ID_GENERATOR++;
            delay.Deinited = false;
            delay.OnInit();
            delay.DelayTime = delayTime;
            delay.OnDelayFinish = onDelayFinish;
            delay.CurrentSeconds = 0.0f;
            return delay;
        }

        public override void OnInit()
        {
            base.OnInit();
            CurrentSeconds = 0.0f;
        }

        public override void OnStart() => OnExecute(0);

        public override void OnExecute(float dt)
        {
            CurrentSeconds += dt;
            if (CurrentSeconds >= DelayTime)
            {
                this.Finish();
                OnDelayFinish?.Invoke();
            }
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                OnDelayFinish = null;
                Deinited = true;
                ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Delay>(mPool, this));
            }
        }

        public override string GetDebugInfo() => $"Delay({DelayTime:F1}s, {CurrentSeconds:F1}s elapsed)";
    }

    public static class DelayExtension
    {
        public static ISequence Delay(this ISequence self, float seconds, Action onDelayFinish = null)
        {
            return self.Append(YokiFrame.Delay.Allocate(seconds, onDelayFinish));
        }
    }
}