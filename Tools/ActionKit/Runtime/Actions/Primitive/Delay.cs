using System;

namespace YokiFrame
{
    /// <summary>
    /// 使用 controller 当前时间源等待指定秒数。
    /// </summary>
    public class Delay : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<Delay> sPool = PoolKit.Create(
            static () => new Delay(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private bool mPoolOwned;

        /// <summary>当前租约的目标等待秒数；保留 2.0-pre 可直接配置的公开字段契约。</summary>
        public float DelayTime;

        /// <summary>获取或设置仅在正常完成时调用的回调。</summary>
        public Action OnDelayFinish { get; set; }

        /// <summary>获取或设置当前轮次已经累计的秒数。</summary>
        public float CurrentSeconds { get; set; }

        /// <summary>创建可由调用方配置或继承的空延迟 Action。</summary>
        public Delay() { }

        /// <summary>
        /// 分配一个秒级延迟；零或负数会在 Start 的 dt=0 推进中完成。
        /// </summary>
        /// <param name="delayTime">目标等待秒数。</param>
        /// <param name="onDelayFinish">正常完成时调用的回调。</param>
        /// <returns>新的延迟执行租约。</returns>
        public static Delay Allocate(float delayTime, Action onDelayFinish = null)
        {
            ValidateFiniteTime(delayTime, nameof(delayTime));

            Delay action = sPool.Allocate();
            action.mPoolOwned = true;
            action.PreparePooled(ActionKitScheduler.NextActionId());
            action.DelayTime = delayTime;
            action.OnDelayFinish = onDelayFinish;
            action.CurrentSeconds = 0f;
            return action;
        }

        /// <summary>重置累计秒数，支持 Repeat 在不重新分配的情况下开启下一轮。</summary>
        public override void OnInit()
        {
            base.OnInit();
            CurrentSeconds = 0f;
        }

        /// <summary>使用零时间检查即时延迟。</summary>
        public override void OnStart() => Advance(0f);

        /// <summary>累计当前 controller 选择的时间源。</summary>
        /// <param name="dt">当前 controller 选择的非负时间步长。</param>
        public override void OnExecute(float dt) => Advance(dt);

        /// <summary>仅在正常完成时调用延迟回调。</summary>
        public override void OnFinish() => OnDelayFinish?.Invoke();

        /// <summary>释放完成回调，避免对象池保留业务目标。</summary>
        public override void OnDeinit() => OnDelayFinish = null;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建包含目标和当前进度的按需诊断文本。</summary>
        /// <returns>不参与 Tick 热路径的延迟摘要。</returns>
        public override string GetDebugInfo() => "Delay(" + DelayTime + "s, " + CurrentSeconds + "s elapsed)";
#endif

        /// <summary>仅把内部 Allocate 创建的租约归还 Delay 局部池。</summary>
        void IPooledAction.ReturnToPool()
        {
            if (mPoolOwned) sPool.Recycle(this);
        }

        /// <summary>累计非负时间并在达到阈值时完成。</summary>
        private void Advance(float deltaTime)
        {
            ValidateFiniteTime(DelayTime, nameof(DelayTime));
            ValidateFiniteTime(CurrentSeconds, nameof(CurrentSeconds));
            CurrentSeconds += deltaTime;
            if (CurrentSeconds >= DelayTime) this.Finish();
        }

        /// <summary>拒绝会让公开可变计时永久停滞的 NaN 或无穷值。</summary>
        /// <param name="value">待校验的等待时长或累计秒数。</param>
        /// <param name="parameterName">发生错误时报告的公开成员或参数名。</param>
        private static void ValidateFiniteTime(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        /// <summary>回池前清除业务引用和数值状态。</summary>
        private void ResetForPool()
        {
            DelayTime = 0f;
            CurrentSeconds = 0f;
            OnDelayFinish = null;
            mPoolOwned = false;
        }
    }
}
