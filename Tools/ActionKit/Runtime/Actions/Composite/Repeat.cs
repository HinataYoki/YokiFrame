using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 按轮顺序推进同一组子 Action；每次调度至多完成一轮即时子树。
    /// </summary>
    internal sealed class Repeat : ActionBase, IRepeat, IActionContainerInternal, IPooledAction
    {
        private const int MAX_RETAINED_CHILD_CAPACITY = 256;
        private static readonly Func<bool> sDefaultCondition = static () => true;
        private static readonly ObjectPool<Repeat> sPool = PoolKit.Create(
            static () => new Repeat(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private readonly List<IAction> mActions = new(8);
        private Func<bool> mCondition = sDefaultCondition;
        private int mMaxRepeatCount;
        private int mCurrentRepeatCount;
        private int mCurrentIndex;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private Repeat() { }

        /// <summary>
        /// 分配一个重复容器。
        /// </summary>
        /// <param name="repeatCount">目标轮数；小于等于零表示无限。</param>
        /// <param name="condition">每轮结束后的继续条件。</param>
        /// <returns>新的 Repeat 执行租约。</returns>
        internal static Repeat Allocate(int repeatCount, Func<bool> condition)
        {
            Repeat repeat = sPool.Allocate();
            repeat.PreparePooled(ActionKitScheduler.NextActionId());
            repeat.mMaxRepeatCount = repeatCount;
            repeat.mCondition = condition ?? sDefaultCondition;
            repeat.mCurrentRepeatCount = 0;
            repeat.mCurrentIndex = 0;
            return repeat;
        }

        /// <summary>获取当前直接子 Action 数量。</summary>
        int IActionContainerInternal.ChildCount => mActions.Count;

        /// <summary>重置轮次计数并初始化第一轮子树。</summary>
        public override void OnInit()
        {
            base.OnInit();
            mCurrentRepeatCount = 0;
            ResetRound();
        }

        /// <summary>使用 dt=0 推进第一轮即时节点。</summary>
        public override void OnStart() => AdvanceRound(0f);

        /// <summary>推进当前轮，并在本次调用内只完成一轮。</summary>
        public override void OnExecute(float dt) => AdvanceRound(dt);

        /// <summary>
        /// 追加一个唯一所有权的重复子 Action。
        /// </summary>
        /// <param name="action">待追加 Action。</param>
        /// <returns>当前 Repeat。</returns>
        public ISequence Append(IAction action)
        {
            ActionOwnership.ClaimChild(this, action);
            mActions.Add(action);
            return this;
        }

        /// <summary>释放条件和子列表引用。</summary>
        public override void OnDeinit()
        {
            mActions.Clear();
            mCondition = sDefaultCondition;
            mMaxRepeatCount = 0;
            mCurrentRepeatCount = 0;
            mCurrentIndex = 0;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建轮数和当前子索引按需诊断文本。</summary>
        public override string GetDebugInfo() =>
            "Repeat(" + mCurrentRepeatCount + "/" + mMaxRepeatCount + ", index=" + mCurrentIndex + ")";
#endif

        /// <summary>按索引获取直接子 Action。</summary>
        IAction IActionContainerInternal.GetChild(int index) => mActions[index];

        /// <summary>把已释放实例归还 Repeat 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>按 Sequence 规则推进当前轮，同一个 delta 只交给首个耗时节点。</summary>
        private void AdvanceRound(float deltaTime)
        {
            float currentDelta = deltaTime;
            while (mCurrentIndex < mActions.Count)
            {
                bool childCompleted = ActionRuntime.Update(mActions[mCurrentIndex], currentDelta);
                if (ActionState == ActionStatus.Finished
                    || ActionKitScheduler.CurrentAdvanceTerminationRequested
                    || !childCompleted) return;
                mCurrentIndex++;
                currentDelta = 0f;
            }

            CompleteRound();
        }

        /// <summary>记录一轮完成，并按次数和 condition 决定结束或重置下一轮。</summary>
        private void CompleteRound()
        {
            mCurrentRepeatCount++;
            bool conditionPassed = mCondition();
            bool belowCount = mMaxRepeatCount <= 0 || mCurrentRepeatCount < mMaxRepeatCount;
            if (ActionState == ActionStatus.Finished
                || ActionKitScheduler.CurrentAdvanceTerminationRequested) return;
            if (conditionPassed && belowCount)
            {
                ResetRound();
                return;
            }

            this.Finish();
        }

        /// <summary>不重新分配子 Action，原地开始下一轮。</summary>
        private void ResetRound()
        {
            mCurrentIndex = 0;
            for (var index = 0; index < mActions.Count; index++)
            {
                ActionRuntime.Restart(mActions[index]);
                if (ActionState == ActionStatus.Finished
                    || ActionKitScheduler.CurrentAdvanceTerminationRequested) return;
            }
        }

        /// <summary>回池前清空引用并裁剪异常峰值容量。</summary>
        private void ResetForPool()
        {
            mActions.Clear();
            if (mActions.Capacity > MAX_RETAINED_CHILD_CAPACITY) mActions.Capacity = MAX_RETAINED_CHILD_CAPACITY;
            mCondition = sDefaultCondition;
            mMaxRepeatCount = 0;
            mCurrentRepeatCount = 0;
            mCurrentIndex = 0;
        }
    }
}
