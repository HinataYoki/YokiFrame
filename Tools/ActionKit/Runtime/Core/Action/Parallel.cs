using System.Collections.Generic;

namespace YokiFrame
{
    internal class Parallel : ActionBase, IParallel
    {
        private static readonly YokiFrame.SimplePoolKit<Parallel> sPool = new(static () => new Parallel());

        private readonly List<IAction> mActions = new();
        private int mFinishedCount;
        private bool mWaitAll = true;

        static Parallel()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Parallel>();
        }

        internal static Parallel Allocate(bool waitAll)
        {
            var parallel = sPool.Allocate();
            parallel.ActionID = ActionKit.sIdGenerator++;
            parallel.mWaitAll = waitAll;
            parallel.Deinited = false;
            parallel.OnInit();
            return parallel;
        }

        public override void OnInit()
        {
            base.OnInit();
            mFinishedCount = 0;
            foreach (var action in mActions) action.OnInit();
        }

        public override void OnStart() => Paralleling(0);

        public override void OnExecute(float dt) => Paralleling(dt);

        /// <summary>
        /// 添加一个子 Action。
        /// </summary>
        /// <param name="action">要追加的子 Action。</param>
        public IParallel Append(IAction action)
        {
            mActions.Add(action);
            return this;
        }

        ISequence ISequence.Append(IAction action) => Append(action);

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            foreach (var action in mActions) action.OnDeinit();
            mActions.Clear();
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Parallel>(sPool, this));
        }

        public override string GetDebugInfo() => $"Parallel({mActions.Count} actions, waitAll={mWaitAll})";

        internal IReadOnlyList<IAction> EditorGetActions() => mActions;

        private void Paralleling(float dt)
        {
            for (int i = mFinishedCount; i < mActions.Count; i++)
            {
                if (!mActions[i].Update(dt)) continue;

                ++mFinishedCount;
                if (mWaitAll && mFinishedCount < mActions.Count)
                {
                    (mActions[i], mActions[mFinishedCount - 1]) = (mActions[mFinishedCount - 1], mActions[i]);
                    continue;
                }

                this.Finish();
            }
        }
    }
}
