using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 同 Tick 推进全部未完成子 Action，并按 waitAll 或 waitAny 结束。
    /// </summary>
    internal sealed class Parallel : ActionBase, IParallel, IActionContainerInternal, IPooledAction
    {
        private const int MAX_RETAINED_CHILD_CAPACITY = 256;
        private static readonly ObjectPool<Parallel> sPool = PoolKit.Create(
            static () => new Parallel(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private readonly List<IAction> mActions = new(8);
        private int mFinishedCount;
        private bool mWaitAll;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private Parallel() { }

        /// <summary>
        /// 分配一个空并行容器。
        /// </summary>
        /// <param name="waitAll">是否等待全部分支完成。</param>
        /// <returns>新的 Parallel 执行租约。</returns>
        internal static Parallel Allocate(bool waitAll)
        {
            Parallel parallel = sPool.Allocate();
            parallel.PreparePooled(ActionKitScheduler.NextActionId());
            parallel.mWaitAll = waitAll;
            parallel.mFinishedCount = 0;
            return parallel;
        }

        /// <summary>获取当前直接子 Action 数量。</summary>
        int IActionContainerInternal.ChildCount => mActions.Count;

        /// <summary>重置自身和全部子分支。</summary>
        public override void OnInit()
        {
            base.OnInit();
            mFinishedCount = 0;
            for (var index = 0; index < mActions.Count; index++)
            {
                ActionRuntime.Restart(mActions[index]);
                if (ActionState == ActionStatus.Finished
                    || ActionKitScheduler.CurrentAdvanceTerminationRequested) return;
            }
        }

        /// <summary>使用 dt=0 推进所有即时分支，空容器会立即完成。</summary>
        public override void OnStart() => Advance(0f);

        /// <summary>使用同一宿主 delta 推进每条未完成并行分支。</summary>
        public override void OnExecute(float dt) => Advance(dt);

        /// <summary>
        /// 追加一个唯一所有权的并行分支。
        /// </summary>
        /// <param name="action">待追加 Action。</param>
        /// <returns>当前 Parallel。</returns>
        public IParallel Append(IAction action)
        {
            ActionOwnership.ClaimChild(this, action);
            mActions.Add(action);
            return this;
        }

        /// <summary>通过 ISequence 调用时仍追加到当前并行容器。</summary>
        ISequence ISequence.Append(IAction action) => Append(action);

        /// <summary>释放子列表引用；未赢分支不会收到 OnFinish。</summary>
        public override void OnDeinit()
        {
            mActions.Clear();
            mFinishedCount = 0;
            mWaitAll = true;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建分支数量、完成数量和等待模式按需诊断文本。</summary>
        public override string GetDebugInfo() =>
            "Parallel(" + mFinishedCount + "/" + mActions.Count + ", waitAll=" + mWaitAll + ")";
#endif

        /// <summary>按索引获取直接子 Action。</summary>
        IAction IActionContainerInternal.GetChild(int index) => mActions[index];

        /// <summary>把已释放实例归还 Parallel 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>推进全部未完成分支；waitAny 找到赢家后立即停止后续副作用。</summary>
        private void Advance(float deltaTime)
        {
            if (mActions.Count == 0)
            {
                this.Finish();
                return;
            }

            var index = mFinishedCount;
            while (index < mActions.Count)
            {
                bool childCompleted = ActionRuntime.Update(mActions[index], deltaTime);
                if (ActionState == ActionStatus.Finished
                    || ActionKitScheduler.CurrentAdvanceTerminationRequested) return;
                if (!childCompleted) { index++; continue; }
                if (!mWaitAll) { this.Finish(); return; }
                MoveFinishedToPrefix(index);
                index++;
            }

            if (mFinishedCount == mActions.Count) this.Finish();
        }

        /// <summary>把完成分支移动到前缀；换入分支已经在本 Tick 推进过，调用方必须跳过它。</summary>
        private void MoveFinishedToPrefix(int completedIndex)
        {
            int prefixIndex = mFinishedCount;
            if (completedIndex != prefixIndex)
            {
                IAction pending = mActions[prefixIndex];
                mActions[prefixIndex] = mActions[completedIndex];
                mActions[completedIndex] = pending;
            }

            mFinishedCount++;
        }

        /// <summary>回池前清空列表并裁剪异常峰值容量。</summary>
        private void ResetForPool()
        {
            mActions.Clear();
            if (mActions.Capacity > MAX_RETAINED_CHILD_CAPACITY) mActions.Capacity = MAX_RETAINED_CHILD_CAPACITY;
            mFinishedCount = 0;
            mWaitAll = true;
        }
    }
}
