using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public interface IParallel : ISequence { }

    internal class Parallel : ActionBase, IParallel
    {
        /// <summary>
        /// 需要完成的任务
        /// </summary>
        private readonly List<IAction> mActions = new();
        /// <summary>
        /// 已经完成的任务数量
        /// </summary>
        private int mFinishedCount = 0;
        /// <summary>
        /// 并行任务池
        /// </summary>
        private static readonly SimplePoolKit<Parallel> mPool = new(() => new Parallel());
        /// <summary>
        /// 等待所有任务完成
        /// </summary>
        private bool mWaitAll = true;

        static Parallel()
        {
            ActionKitPlayerLoopSystem.RegisterRecycleProcessor<Parallel>();
        }

        public static Parallel Allocate(bool waitAll)
        {
            var parallel = mPool.Allocate();
            parallel.ActionID = ActionKit.ID_GENERATOR++;
            parallel.mWaitAll = waitAll;
            parallel.Deinited = false;
            parallel.OnInit();
            return parallel;
        }

        public override void OnInit()
        {
            base.OnInit();
            mFinishedCount = 0;

            foreach (var action in mActions)
            {
                action.OnInit();
            }
        }

        public override void OnStart() => Paralleling(0);

        public override void OnExecute(float dt) => Paralleling(dt);

        private void Paralleling(float dt)
        {
            for (int i = mFinishedCount; i < mActions.Count; i++)
            {
                if (!mActions[i].Update(dt)) continue;
                ++mFinishedCount;

                if (mWaitAll && mFinishedCount < mActions.Count)
                {
                    //把每次完成的任务挪到前面
                    (mActions[i], mActions[mFinishedCount - 1]) = (mActions[mFinishedCount - 1], mActions[i]);
                }
                else
                {
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
                Deinited = true;

                foreach (var action in mActions)
                {
                    action.OnDeinit();
                }
                mActions.Clear();

                ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Parallel>(mPool, this));
            }
        }

        public override string GetDebugInfo() => $"Parallel({mActions.Count} actions, waitAll={mWaitAll})";
        
        // 编辑器监控接口（通过反射访问，运行时零开销）
        internal IReadOnlyList<IAction> EditorGetActions() => mActions;
    }

    public static class ParallelExtension
    {
        public static ISequence Parallel(this ISequence self, Action<ISequence> parallel, bool waitAll = true)
        {
            var p = YokiFrame.Parallel.Allocate(waitAll);
            parallel?.Invoke(p);
            return self.Append(p);
        }
    }
}