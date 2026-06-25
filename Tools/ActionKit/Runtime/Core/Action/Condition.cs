using System;

namespace YokiFrame
{
    internal class Condition : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<Condition> sPool = new(static () => new Condition());

        private Func<bool> mCondition;

        static Condition()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Condition>();
        }

        internal static Condition Allocate(Func<bool> callback)
        {
            var condition = sPool.Allocate();
            condition.ActionID = ActionKit.sIdGenerator++;
            condition.Deinited = false;
            condition.OnInit();
            condition.mCondition = callback;
            return condition;
        }

        public override void OnStart()
        {
            if (mCondition.Invoke()) this.Finish();
        }

        public override void OnExecute(float dt)
        {
            if (mCondition.Invoke()) this.Finish();
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            mCondition = null;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Condition>(sPool, this));
        }

        public override string GetDebugInfo() =>
            mCondition != null ? $"Condition -> {mCondition.Method.DeclaringType}.{mCondition.Method.Name}" : "Condition";
    }
}
