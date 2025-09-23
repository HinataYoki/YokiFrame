using System;

namespace YokiFrame
{
    public class Delay : IAction
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
        private static readonly SimplePoolKit<Delay> delayPool = new(() => new Delay());

        public static Delay Allocate(float delayTime, Action onDelayFinish = null)
        {
            var delay = delayPool.Allocate();
            delay.ActionID = ActionKit.ID_GENERATOR++;
            delay.Deinited = false;
            delay.OnInit();
            delay.DelayTime = delayTime;
            delay.OnDelayFinish = onDelayFinish;
            delay.CurrentSeconds = 0.0f;
            return delay;
        }

        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }
        public bool Paused { get; set; }
        public bool Deinited { get; set; }

        public void OnInit()
        {
            ActionState = ActionStatus.NotStart;
            Paused = false;
            CurrentSeconds = 0.0f;
        }

        public void OnStart() => OnExecute(0);

        public void OnExecute(float dt)
        {
            CurrentSeconds += dt;
            if (CurrentSeconds >= DelayTime)
            {
                this.Finish();
                OnDelayFinish?.Invoke();
            }
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                OnDelayFinish = null;
                Deinited = true;
                MonoRecycler.AddRecycleCallback(new ActionRecycler<Delay>(delayPool, this));
            }
        }

        string IAction.LogError() => $"类 {OnDelayFinish.Method.DeclaringType} 方法 {OnDelayFinish.Method} 出错";
    }

    public static class DelayExtension
    {
        public static ISequence Delay(this ISequence self, float seconds, Action onDelayFinish = null)
        {
            return self.Append(YokiFrame.Delay.Allocate(seconds, onDelayFinish));
        }
    }
}