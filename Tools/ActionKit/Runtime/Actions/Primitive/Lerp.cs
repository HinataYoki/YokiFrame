using System;

namespace YokiFrame
{
    /// <summary>
    /// 在指定时长内线性插值 float，并保证正常完成时写入目标值。
    /// </summary>
    public class Lerp : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<Lerp> sPool = PoolKit.Create(
            static () => new Lerp(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private float mCurrentTime;
        private bool mPoolOwned;

        /// <summary>当前租约起始值；保留 2.0-pre 可直接配置的公开字段契约。</summary>
        public float A;

        /// <summary>当前租约目标值；保留 2.0-pre 可直接配置的公开字段契约。</summary>
        public float B;

        /// <summary>当前租约持续秒数；保留 2.0-pre 可直接配置的公开字段契约。</summary>
        public float Duration;

        /// <summary>每次插值输出回调。</summary>
        public Action<float> OnLerp;

        /// <summary>正常完成回调。</summary>
        public Action OnLerpFinish;

        /// <summary>创建可由调用方配置或继承的空插值 Action。</summary>
        public Lerp() { }

        /// <summary>
        /// 分配一个 float 插值；持续时间必须是有限数，零或负数同步输出起点和终点。
        /// </summary>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">有限持续秒数。</param>
        /// <param name="onLerp">每次推进时接收当前值的回调。</param>
        /// <param name="onLerpFinish">仅在正常完成时调用的回调。</param>
        /// <returns>新的插值执行租约。</returns>
        public static Lerp Allocate(float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            ValidateDuration(duration);

            Lerp action = sPool.Allocate();
            action.mPoolOwned = true;
            action.PreparePooled(ActionKitScheduler.NextActionId());
            action.A = a;
            action.B = b;
            action.Duration = duration;
            action.OnLerp = onLerp;
            action.OnLerpFinish = onLerpFinish;
            action.mCurrentTime = 0f;
            return action;
        }

        /// <summary>重置当前轮次累计时间。</summary>
        public override void OnInit()
        {
            base.OnInit();
            mCurrentTime = 0f;
        }

        /// <summary>输出起点；零或负时长随后立即进入正常完成。</summary>
        public override void OnStart()
        {
            ValidateDuration(Duration);
            OnLerp?.Invoke(A);
            if (Duration <= 0f) this.Finish();
        }

        /// <summary>累计时间并输出限制在 0..1 的线性插值。</summary>
        /// <param name="dt">当前 controller 选择的非负时间步长。</param>
        public override void OnExecute(float dt)
        {
            ValidateDuration(Duration);
            mCurrentTime += dt;
            if (mCurrentTime >= Duration)
            {
                this.Finish();
                return;
            }

            OnLerp?.Invoke(A + (B - A) * (mCurrentTime / Duration));
        }

        /// <summary>正常完成时写入精确目标值并调用完成回调。</summary>
        public override void OnFinish()
        {
            OnLerp?.Invoke(B);
            OnLerpFinish?.Invoke();
        }

        /// <summary>释放两个业务回调。</summary>
        public override void OnDeinit()
        {
            OnLerp = null;
            OnLerpFinish = null;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建起止值和进度按需诊断文本。</summary>
        /// <returns>不参与 Tick 热路径的插值摘要。</returns>
        public override string GetDebugInfo() => "Lerp(" + A + " -> " + B + ", " + mCurrentTime + "/" + Duration + "s)";
#endif

        /// <summary>仅把内部 Allocate 创建的租约归还 Lerp 局部池。</summary>
        void IPooledAction.ReturnToPool()
        {
            if (mPoolOwned) sPool.Recycle(this);
        }

        /// <summary>拒绝无法终结插值的 NaN 或无穷时长，公开字段被修改后也保持调度安全。</summary>
        /// <param name="duration">待校验的插值持续秒数。</param>
        private static void ValidateDuration(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));
        }

        /// <summary>回池前清除回调和值状态。</summary>
        private void ResetForPool()
        {
            A = 0f;
            B = 0f;
            Duration = 0f;
            mCurrentTime = 0f;
            OnLerp = null;
            OnLerpFinish = null;
            mPoolOwned = false;
        }
    }
}
