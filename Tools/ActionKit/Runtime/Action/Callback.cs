using System;

namespace YokiFrame
{
    internal class Callback : ActionBase
    {
        /// <summary>
        /// 回调任务
        /// </summary>
        private Action mCallback;
        /// <summary>
        /// 回调任务池
        /// </summary>
        private static readonly SimplePoolKit<Callback> mPool = new(() => new Callback());

        static Callback()
        {
            ActionKitPlayerLoopSystem.RegisterRecycleProcessor<Callback>();
        }

        public static Callback Allocate(Action callback)
        {
            var callbackAction = mPool.Allocate();
            callbackAction.ActionID = ActionKit.ID_GENERATOR++;
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
            if (!Deinited)
            {
                Deinited = true;
                mCallback = null;

                ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Callback>(mPool, this));
            }
        }

        public override string GetDebugInfo() => 
            mCallback != null ? $"Callback -> {mCallback.Method.DeclaringType}.{mCallback.Method.Name}" : "Callback";
    }

    public static class CallbackExtension
    {
        public static ISequence Callback(this ISequence self, Action callback)
        {
            return self.Append(YokiFrame.Callback.Allocate(callback));
        }
    }
}