using System;

namespace YokiFrame
{
    internal class Condition : ActionBase
    {
        /// <summary>
        /// 条件
        /// </summary>
        private Func<bool> mCondition;
        /// <summary>
        /// 条件任务池
        /// </summary>
        private static readonly SimplePoolKit<Condition> mPool = new(() => new Condition());

        public static Condition Allocate(Func<bool> callback)
        {
            var condition = mPool.Allocate();
            condition.ActionID = ActionKit.ID_GENERATOR++;
            condition.Deinited = false;
            condition.OnInit();
            condition.mCondition = callback;
            return condition;
        }

        public override void OnStart()
        {
            if (mCondition.Invoke())
            {
                this.Finish();
            }
        }

        public override void OnExecute(float dt)
        {
            if (mCondition.Invoke())
            {
                this.Finish();
            }
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCondition = null;

                MonoRecycler.AddRecycleCallback(new ActionRecycler<Condition>(mPool, this));
            }
        }

        public override string GetDebugInfo() => 
            mCondition != null ? $"Condition -> {mCondition.Method.DeclaringType}.{mCondition.Method.Name}" : "Condition";
    }

    public static class ConditionExtension
    {
        public static ISequence Condition(this ISequence self, Func<bool> condition)
        {
            return self.Append(YokiFrame.Condition.Allocate(condition));
        }
    }
}