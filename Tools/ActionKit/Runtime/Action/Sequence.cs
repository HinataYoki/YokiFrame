using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public interface ISequence : IAction
    {
        ISequence Append(IAction action);
    }

    internal class Sequence : ActionBase, ISequence
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
        private static readonly SimplePoolKit<Sequence> mPool = new(() => new Sequence());

        static Sequence()
        {
            ActionKitPlayerLoopSystem.RegisterRecycleProcessor<Sequence>();
        }

        public static Sequence Allocate()
        {
            var sequence = mPool.Allocate();
            sequence.ActionID = ActionKit.ID_GENERATOR++;
            sequence.OnInit();
            sequence.Deinited = false;
            return sequence;
        }

        public override void OnInit()
        {
            base.OnInit();
            mCurActionIndex = 0;

            foreach (var action in mActions)
            {
                action.OnInit();
            }
        }

        public override void OnStart()
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

        public override void OnExecute(float dt) => TryExecuteUntilNextNotFinished(dt);

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

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                foreach (var action in mActions)
                {
                    action.OnDeinit();
                }
                mActions.Clear();

                Deinited = true;

                ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Sequence>(mPool, this));
            }
        }

        public override string GetDebugInfo() => $"Sequence({mActions.Count} actions)";
        
        // 编辑器监控接口（通过反射访问，运行时零开销）
        internal IReadOnlyList<IAction> EditorGetActions() => mActions;
        internal int EditorGetCurrentIndex() => mCurActionIndex;
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