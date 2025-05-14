using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public interface ISequence : IAction
    {
        ISequence Append(IAction action);
    }

    internal class Sequence : ISequence
    {
        /// <summary>
        /// 需要顺序执行的任务
        /// </summary>
        private readonly List<IAction> mActions = new();
        /// <summary>
        /// 当前正在执行的任务
        /// </summary>
        private IAction mCurAction = null;
        /// <summary>
        /// 当前任务节点
        /// </summary>
        private int mCurActionIndex = 0;
        /// <summary>
        /// 序列任务池
        /// </summary>
        private static readonly SimpleObjectPool<Sequence> sequencePool = new(() => new Sequence());

        public static Sequence Allocate()
        {
            var sequence = sequencePool.Allocate();
            sequence.ActionID = ActionKit.ID_GENERATOR++;
            sequence.OnInit();
            sequence.Deinited = false;
            return sequence;
        }

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }

        public void OnInit()
        {
            mCurActionIndex = 0;
            ActionState = ActionStatus.NotStart;
            Paused = false;

            foreach (var action in mActions)
            {
                action.OnInit();
            }
        }

        public void OnStart()
        {
            if (mActions.Count > 0)
            {
                mCurAction = mActions[mCurActionIndex];
                TryExecuteUntilNextNotFinished(0);
            }
            else
            {
                this.Finish();
            }
        }

        public void OnExecute(float dt) => TryExecuteUntilNextNotFinished(dt);

        private void TryExecuteUntilNextNotFinished(float dt)
        {
            while (mCurAction != null && mCurAction.Update(dt))
            {
                ++mCurActionIndex;
                if (mCurActionIndex < mActions.Count)
                {
                    mCurAction = mActions[mCurActionIndex];
                }
                else
                {
                    mCurAction = null;
                    this.Finish();
                }
            }
        }

        public ISequence Append(IAction action)
        {
            mActions.Add(action);
            return this;
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                foreach (var action in mActions)
                {
                    action.OnDeinit();
                }
                mActions.Clear();

                Deinited = true;

                MonoRecycler.AddRecycleCallback(new ActionRecycler<Sequence>(sequencePool, this));
            }
        }

        string IAction.LogError() => $"顺序队列出错";
    }

    public static class SequenceExtension
    {
        public static ISequence Sequence(this ISequence self, Action<ISequence> sequence = null)
        {
            var s = YokiFrame.Sequence.Allocate();
            sequence?.Invoke(s);
            return self.Append(s);
        }
    }
}