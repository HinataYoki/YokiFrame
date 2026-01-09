using System;

namespace YokiFrame
{
    public interface IRepeat : ISequence
    {
    }

    internal class Repeat : ActionBase, IRepeat
    {
        private Sequence mSequence = null;
        private int mMaxRepeatCount = 0;
        private int mCurrentRepeatCount = 0;
        private Func<bool> mCondition = () => true;

        private static readonly SimplePoolKit<Repeat> mPool = new(() => new Repeat());

        public static Repeat Allocate(int repeatCount = 0, Func<bool> condition = null)
        {
            var repeat = mPool.Allocate();
            repeat.ActionID = ActionKit.ID_GENERATOR++;
            repeat.mSequence = Sequence.Allocate();
            if (condition != null)
            {
                repeat.mCondition = condition;
            }
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

        private void Repeating(float dt)
        {
            if (mSequence.Update(dt))
            {
                ++mCurrentRepeatCount;
                if (Condition())
                {
                    mSequence.OnInit();
                }
                else
                {
                    this.Finish();
                }
            }
        }

        private bool Condition() => mCondition.Invoke() && (mMaxRepeatCount <= 0 || mCurrentRepeatCount < mMaxRepeatCount);

        public ISequence Append(IAction action)
        {
            mSequence.Append(action);
            return this;
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mMaxRepeatCount = 0;
                mCondition = () => true;
                mSequence.OnDeinit();
                MonoRecycler.AddRecycleCallback(new ActionRecycler<Repeat>(mPool, this));
            }
        }

        public override string GetDebugInfo() => $"Repeat(max={mMaxRepeatCount}, current={mCurrentRepeatCount})";
        
        // 编辑器监控接口（通过反射访问，运行时零开销）
        internal Sequence EditorGetSequence() => mSequence;
    }

    public static class RepeatExtension
    {
        public static ISequence Repeat(this ISequence self, Action<IRepeat> repeat, int count = -1, Func<bool> condition = null)
        {
            var r = YokiFrame.Repeat.Allocate(count, condition);
            repeat?.Invoke(r);
            return self.Append(r);
        }
    }
}