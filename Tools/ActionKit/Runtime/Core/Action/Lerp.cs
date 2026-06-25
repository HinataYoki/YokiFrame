using System;

namespace YokiFrame
{
    /// <summary>
    /// 浮点插值 Action。
    /// </summary>
    public class Lerp : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<Lerp> sPool = new(static () => new Lerp());

        private float mCurrentTime;

        /// <summary>
        /// 起始值。
        /// </summary>
        public float A;

        /// <summary>
        /// 目标值。
        /// </summary>
        public float B;

        /// <summary>
        /// 插值持续时间。
        /// </summary>
        public float Duration;

        /// <summary>
        /// 每次更新时接收当前插值的回调。
        /// </summary>
        public Action<float> OnLerp;

        /// <summary>
        /// 插值完成时调用的回调。
        /// </summary>
        public Action OnLerpFinish;

        static Lerp()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Lerp>();
        }

        /// <summary>
        /// 从池中分配插值 Action。
        /// </summary>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">插值持续时间。</param>
        /// <param name="onLerp">每次更新时接收当前插值的回调。</param>
        /// <param name="onLerpFinish">插值完成时调用的回调。</param>
        public static Lerp Allocate(float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            var lerp = sPool.Allocate();
            lerp.ActionID = ActionKit.sIdGenerator++;
            lerp.Deinited = false;
            lerp.OnInit();
            lerp.A = a;
            lerp.B = b;
            lerp.Duration = duration;
            lerp.OnLerp = onLerp;
            lerp.OnLerpFinish = onLerpFinish;
            return lerp;
        }

        /// <summary>
        /// 初始化插值状态。
        /// </summary>
        public override void OnInit()
        {
            base.OnInit();
            mCurrentTime = 0.0f;
        }

        /// <summary>
        /// 开始插值并回调起始值。
        /// </summary>
        public override void OnStart()
        {
            mCurrentTime = 0.0f;
            OnLerp?.Invoke(LerpValue(A, B, 0f));
        }

        /// <summary>
        /// 推进插值。
        /// </summary>
        /// <param name="dt">本次更新的时间步长。</param>
        public override void OnExecute(float dt)
        {
            mCurrentTime += dt;
            if (mCurrentTime < Duration)
            {
                OnLerp?.Invoke(LerpValue(A, B, mCurrentTime / Duration));
                return;
            }

            this.Finish();
        }

        /// <summary>
        /// 完成插值并回调目标值。
        /// </summary>
        public override void OnFinish()
        {
            OnLerp?.Invoke(LerpValue(A, B, 1.0f));
            OnLerpFinish?.Invoke();
        }

        /// <summary>
        /// 释放插值状态并回收。
        /// </summary>
        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            OnLerp = null;
            OnLerpFinish = null;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Lerp>(sPool, this));
        }

        /// <summary>
        /// 返回用于调试面板展示的简短信息。
        /// </summary>
        public override string GetDebugInfo()
        {
            var lerpInfo = OnLerp != null ? $"{OnLerp.Method.DeclaringType}.{OnLerp.Method.Name}" : "null";
            return $"Lerp({A}->{B}) -> {lerpInfo}";
        }

        private static float LerpValue(float a, float b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return a + (b - a) * t;
        }
    }
}
