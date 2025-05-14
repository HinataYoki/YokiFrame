using System;

namespace YokiFrame
{
    internal class Callback : IAction
    {
        /// <summary>
        /// 回调任务
        /// </summary>
        private Action mCallback;
        /// <summary>
        /// 回调任务池
        /// </summary>
        private static readonly SimpleObjectPool<Callback> callbackPool = new(() => new Callback());

        public static Callback Allocate(Action callback)
        {
            var callbackAction = callbackPool.Allocate();
            callbackAction.ActionID = ActionKit.ID_GENERATOR++;
            callbackAction.OnInit();
            callbackAction.Deinited = false;
            callbackAction.mCallback = callback;
            return callbackAction;
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

        public void OnExecute(float dt)
        {
            mCallback?.Invoke();
            this.Finish();
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCallback = null;

                MonoRecycler.AddRecycleCallback(new ActionRecycler<Callback>(callbackPool, this));
            }
        }

        string IAction.LogError() => $"类 {mCallback.Method.DeclaringType} 方法 {mCallback.Method} 出错";
    }

    public static class CallbackExtension
    {
        public static ISequence Callback(this ISequence self, Action callback)
        {
            return self.Append(YokiFrame.Callback.Allocate(callback));
        }
    }
}