using System;

namespace YokiFrame
{
    internal class Callback : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<Callback> sPool = new(static () => new Callback());

        private Action mCallback;

        static Callback()
        {
            ActionKitScheduler.RegisterRecycleProcessor<Callback>();
        }

        internal static Callback Allocate(Action callback)
        {
            var callbackAction = sPool.Allocate();
            callbackAction.ActionID = ActionKit.sIdGenerator++;
            callbackAction.OnInit();
            callbackAction.Deinited = false;
            callbackAction.mCallback = callback;
            return callbackAction;
        }

        public override void OnStart()
        {
            mCallback?.Invoke();
            this.Finish();
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            mCallback = null;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Callback>(sPool, this));
        }

        public override string GetDebugInfo() =>
            mCallback != null ? $"Callback -> {mCallback.Method.DeclaringType}.{mCallback.Method.Name}" : "Callback";
    }
}
