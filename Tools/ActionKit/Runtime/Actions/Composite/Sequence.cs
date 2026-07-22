using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 按追加顺序推进子 Action，并在同 Tick 只向首个耗时节点传递 delta。
    /// </summary>
    internal sealed class Sequence : ActionBase, ISequence, IActionContainerInternal, IPooledAction
    {
        private const int MAX_RETAINED_CHILD_CAPACITY = 256;
        private static readonly ObjectPool<Sequence> sPool = PoolKit.Create(
            static () => new Sequence(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private readonly List<IAction> mActions = new(8);
        private int mCurrentIndex;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private Sequence() { }

        /// <summary>分配一个空顺序容器。</summary>
        /// <returns>新的 Sequence 执行租约。</returns>
        internal static Sequence Allocate()
        {
            Sequence sequence = sPool.Allocate();
            sequence.PreparePooled(ActionKitScheduler.NextActionId());
            sequence.mCurrentIndex = 0;
            return sequence;
        }

        /// <summary>获取当前直接子 Action 数量。</summary>
        int IActionContainerInternal.ChildCount => mActions.Count;

        /// <summary>重置自身和全部子 Action，供首次 Start 与 Repeat 新一轮使用。</summary>
        public override void OnInit()
        {
            base.OnInit();
            mCurrentIndex = 0;
            for (var index = 0; index < mActions.Count; index++)
            {
                ActionRuntime.Restart(mActions[index]);
                if (ActionState == ActionStatus.Finished
                    || ActionKitScheduler.CurrentAdvanceTerminationRequested) return;
            }
        }

        /// <summary>使用 dt=0 排空开头的即时 Action。</summary>
        public override void OnStart() => Advance(0f);

        /// <summary>推进当前耗时节点，并以 dt=0 排空随后即时节点。</summary>
        public override void OnExecute(float dt) => Advance(dt);

        /// <summary>
        /// 追加一个唯一所有权的子 Action。
        /// </summary>
        /// <param name="action">待追加 Action。</param>
        /// <returns>当前 Sequence。</returns>
        public ISequence Append(IAction action)
        {
            ActionOwnership.ClaimChild(this, action);
            mActions.Add(action);
            return this;
        }

        /// <summary>释放子列表引用；子节点已由 ActionRuntime 先完成逐个清理。</summary>
        public override void OnDeinit()
        {
            mActions.Clear();
            mCurrentIndex = 0;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建子数量和当前索引按需诊断文本。</summary>
        public override string GetDebugInfo() => "Sequence(" + mActions.Count + " actions, index=" + mCurrentIndex + ")";
#endif

        /// <summary>按索引获取直接子 Action，供生命周期与诊断遍历。</summary>
        IAction IActionContainerInternal.GetChild(int index) => mActions[index];

        /// <summary>把已释放实例归还 Sequence 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>推进顺序节点；完成一个节点后不重复消费本 Tick 的 delta。</summary>
        private void Advance(float deltaTime)
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

            this.Finish();
        }

        /// <summary>回池前清空列表，并裁剪异常峰值容量。</summary>
        private void ResetForPool()
        {
            mActions.Clear();
            if (mActions.Capacity > MAX_RETAINED_CHILD_CAPACITY) mActions.Capacity = MAX_RETAINED_CHILD_CAPACITY;
            mCurrentIndex = 0;
        }
    }
}
