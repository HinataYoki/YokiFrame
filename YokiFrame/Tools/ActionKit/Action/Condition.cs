using System;

namespace YokiFrame
{
    internal class Condition : IAction
    {
        /// <summary>
        /// 条件
        /// </summary>
        private Func<bool> mCondition;
        /// <summary>
        /// 条件任务池
        /// </summary>
        private static readonly SimpleObjectPool<Condition> conditionPool = new(() => new Condition());

        public static Condition Allocate(Func<bool> callback)
        {
            var condition = conditionPool.Allocate();
            condition.ActionID = ActionKit.ID_GENERATOR++;
            condition.Deinited = false;
            condition.OnInit();
            condition.mCondition = callback;
            return condition;
        }

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }


        public void OnInit()
        {
            Paused = false;
            ActionState = ActionStatus.NotStart;
        }

        public void OnStart()
        {
            if (mCondition.Invoke())
            {
                this.Finish();
            }
        }

        public void OnExecute(float dt)
        {
            if (mCondition.Invoke())
            {
                this.Finish();
            }
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCondition = null;

                MonoRecycler.AddRecycleCallback(new ActionRecycler<Condition>(conditionPool, this));
            }
        }

        string IAction.LogError() => $"类 {mCondition.Method.DeclaringType} 方法 {mCondition.Method} 出错";
    }

    public static class ConditionExtension
    {
        public static ISequence Condition(this ISequence self, Func<bool> condition)
        {
            return self.Append(YokiFrame.Condition.Allocate(condition));
        }
    }
}