using System;

namespace YokiFrame
{
    public interface IRepeat : ISequence
    {
    }

    internal class Repeat : IRepeat
    {
        private Sequence mSequence = null;
        private int mMaxRepeatCount = 0;
        private int mCurrentRepeatCount = 0;
        private Func<bool> mCondition = () => true;

        private static readonly SimpleObjectPool<Repeat> repeatPool = new(() => new Repeat());

        public static Repeat Allocate(int repeatCount = 0, Func<bool> condition = null)
        {
            var repeat = repeatPool.Allocate();
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

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }

        public void OnInit()
        {
            mCurrentRepeatCount = 0;
            ActionState = ActionStatus.NotStart;
            Paused = false;

            mSequence.OnInit();
        }

        public void OnStart() => Repeating(0);

        public void OnExecute(float dt) => Repeating(dt);

        private void Repeating(float dt)
        {
            if (mSequence.Update(dt))
            {
                ++mCurrentRepeatCount;
                if (Condition())
                {
                    mSequence.OnInit();
                    Repeating(dt);
                }
                else
                {
                    this.Finish();
                }
            }
        }

        private bool Condition() => mCondition.Invoke() && (mMaxRepeatCount == 0 || mCurrentRepeatCount < mMaxRepeatCount);

        public ISequence Append(IAction action)
        {
            mSequence.Append(action);
            return this;
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mMaxRepeatCount = 0;
                mCondition = () => true;
                mSequence.OnDeinit();
                MonoRecycler.AddRecycleCallback(new ActionRecycler<Repeat>(repeatPool, this));
            }
        }

        string IAction.LogError() => $"循环队列出错";
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