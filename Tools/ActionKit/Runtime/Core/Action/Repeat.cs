using System;

namespace YokiFrame
{
    internal class Repeat : ActionBase, IRepeat
    {
        private static readonly Func<bool> sDefaultCondition = static () => true;
        private static readonly YokiFrame.SimplePoolKit<Repeat> sPool = new(static () => new Repeat());

        private Sequence mSequence;
        private int mMaxRepeatCount;
        private int mCurrentRepeatCount;
        private Func<bool> mCondition = sDefaultCondition;

        static Repeat()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Repeat>();
        }

        internal static Repeat Allocate(int repeatCount = -1, Func<bool> condition = null)
        {
            var repeat = sPool.Allocate();
            repeat.ActionID = ActionKit.sIdGenerator++;
            repeat.mSequence = Sequence.Allocate();
            if (condition != null) repeat.mCondition = condition;
            repeat.Deinited = false;
            repeat.OnInit();
            repeat.mMaxRepeatCount = repeatCount;
            return repeat;
        }

        public override void OnInit()
        {
            base.OnInit();
            mCurrentRepeatCount = 0;
            mSequence.OnInit();
        }

        public override void OnStart() => Repeating(0);

        public override void OnExecute(float dt) => Repeating(dt);

        /// <summary>
        /// 添加一个子 Action。
        /// </summary>
        /// <param name="action">要追加的子 Action。</param>
        public ISequence Append(IAction action)
        {
            mSequence.Append(action);
            return this;
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            mMaxRepeatCount = 0;
            mCondition = sDefaultCondition;
            mSequence.OnDeinit();
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Repeat>(sPool, this));
        }

        public override string GetDebugInfo() => $"Repeat(max={mMaxRepeatCount}, current={mCurrentRepeatCount})";

        internal Sequence EditorGetSequence() => mSequence;

        private bool Condition() => mCondition.Invoke() && (mMaxRepeatCount <= 0 || mCurrentRepeatCount < mMaxRepeatCount);

        private void Repeating(float dt)
        {
            if (!mSequence.Update(dt)) return;

            ++mCurrentRepeatCount;
            if (Condition())
            {
                mSequence.OnInit();
                return;
            }

            this.Finish();
        }
    }
}
