using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public interface IParallel : ISequence { }

    internal class Parallel : IParallel
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
        private static readonly SimpleObjectPool<Parallel> parallelPool = new(() => new Parallel());
        /// <summary>
        /// 等待所有任务完成
        /// </summary>
        private bool WaitAll = true;

        public static Parallel Allocate(bool waitAll)
        {
            var parallel = parallelPool.Allocate();
            parallel.ActionID = ActionKit.ID_GENERATOR++;
            parallel.WaitAll = waitAll;
            parallel.Deinited = false;
            parallel.OnInit();
            return parallel;
        }

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }

        public void OnInit()
        {
            mFinishedCount = 0;
            ActionState = ActionStatus.NotStart;
            Paused = false;

            foreach (var action in mActions)
            {
                action.OnInit();
            }
        }

        public void OnStart() => Paralleling(0);

        public void OnExecute(float dt) => Paralleling(dt);

        private void Paralleling(float dt)
        {
            for (int i = mFinishedCount; i < mActions.Count; i++)
            {
                if (!mActions[i].Update(dt)) continue;
                ++mFinishedCount;

                if (WaitAll && mFinishedCount < mActions.Count)
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

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;

                foreach (var action in mActions)
                {
                    action.OnDeinit();
                }
                mActions.Clear();

                MonoRecycler.AddRecycleCallback(new ActionRecycler<Parallel>(parallelPool, this));
            }
        }

        string IAction.LogError() => $" 并行队列出错";
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