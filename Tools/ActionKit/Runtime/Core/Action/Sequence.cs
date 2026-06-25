using System.Collections.Generic;

namespace YokiFrame
{
    internal class Sequence : ActionBase, ISequence
    {
        private static readonly YokiFrame.SimplePoolKit<Sequence> sPool = new(static () => new Sequence());

        private readonly List<IAction> mActions = new();
        private IAction mCurAction;
        private int mCurActionIndex;

        static Sequence()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Sequence>();
        }

        internal static Sequence Allocate()
        {
            var sequence = sPool.Allocate();
            sequence.ActionID = ActionKit.sIdGenerator++;
            sequence.OnInit();
            sequence.Deinited = false;
            return sequence;
        }

        public override void OnInit()
        {
            base.OnInit();
            mCurActionIndex = 0;
            foreach (var action in mActions) action.OnInit();
        }

        public override void OnStart()
        {
            if (mActions.Count > 0)
            {
                mCurAction = mActions[mCurActionIndex];
                TryExecuteUntilNextNotFinished(0);
                return;
            }

            this.Finish();
        }

        public override void OnExecute(float dt) => TryExecuteUntilNextNotFinished(dt);

        /// <summary>
        /// 添加一个子 Action。
        /// </summary>
        /// <param name="action">要追加的子 Action。</param>
        public ISequence Append(IAction action)
        {
            mActions.Add(action);
            return this;
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            foreach (var action in mActions) action.OnDeinit();
            mActions.Clear();
            Deinited = true;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Sequence>(sPool, this));
        }

        public override string GetDebugInfo() => $"Sequence({mActions.Count} actions)";

        internal IReadOnlyList<IAction> EditorGetActions() => mActions;

        internal int EditorGetCurrentIndex() => mCurActionIndex;

        private void TryExecuteUntilNextNotFinished(float dt)
        {
            while (mCurAction != null && mCurAction.Update(dt))
            {
                ++mCurActionIndex;
                if (mCurActionIndex < mActions.Count)
                {
                    mCurAction = mActions[mCurActionIndex];
                    continue;
                }

                mCurAction = null;
                this.Finish();
            }
        }
    }
}
