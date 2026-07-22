using System;

namespace YokiFrame
{
    /// <summary>
    /// 在条件返回 true 前保持运行，相当于跨宿主 WaitUntil。
    /// </summary>
    internal sealed class Condition : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<Condition> sPool = PoolKit.Create(
            static () => new Condition(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private Func<bool> mCondition;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private Condition() { }

        /// <summary>
        /// 分配一个条件 Action，并在构造阶段拒绝永久故障的 null 条件。
        /// </summary>
        /// <param name="condition">每次调度检查的完成条件。</param>
        /// <returns>新的条件执行租约。</returns>
        internal static Condition Allocate(Func<bool> condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            Condition action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            action.mCondition = condition;
            return action;
        }

        /// <summary>首次执行时立即检查一次条件。</summary>
        public override void OnStart() => CheckCondition();

        /// <summary>每个后续 Tick 检查条件，时间步长不参与判断。</summary>
        public override void OnExecute(float dt) => CheckCondition();

        /// <summary>释放条件委托，防止池长期持有业务闭包。</summary>
        public override void OnDeinit() => mCondition = null;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>仅在诊断请求时描述条件目标。</summary>
        public override string GetDebugInfo() => mCondition == null
            ? "Condition"
            : "Condition -> " + mCondition.Method.DeclaringType + "." + mCondition.Method.Name;
#endif

        /// <summary>把已释放实例归还 Condition 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>调用条件并在满足时完成当前 Action。</summary>
        private void CheckCondition()
        {
            if (mCondition()) this.Finish();
        }

        /// <summary>回池前再次清理条件委托。</summary>
        private void ResetForPool() => mCondition = null;
    }
}
