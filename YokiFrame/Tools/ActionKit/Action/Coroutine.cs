using System;
using System.Collections;

namespace YokiFrame
{
    internal class Coroutine : IAction
    {
        /// <summary>
        /// 协程
        /// </summary>
        private Func<IEnumerator> mCoroutineGetter = null;
        /// <summary>
        /// 协程任务池
        /// </summary>
        private static readonly SimplePoolKit<Coroutine> coroutinePool = new(() => new Coroutine());

        public static Coroutine Allocate(Func<IEnumerator> coroutineGetter)
        {
            var coroutine = coroutinePool.Allocate();
            coroutine.ActionID = ActionKit.ID_GENERATOR++;
            coroutine.Deinited = false;
            coroutine.OnInit();
            coroutine.mCoroutineGetter = coroutineGetter;
            return coroutine;
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
            MonoGlobalExecutor.ExecuteCoroutine(mCoroutineGetter(), () =>
            {
                this.Finish();
            });
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCoroutineGetter = null;
                MonoRecycler.AddRecycleCallback(new ActionRecycler<Coroutine>(coroutinePool, this));
            }
        }

        string IAction.LogError() => $"类 {mCoroutineGetter.Method.DeclaringType} 方法 {mCoroutineGetter.Method} 出错";
    }

    public static class CoroutineExtension
    {
        public static ISequence Coroutine(this ISequence self, Func<IEnumerator> coroutineGetter)
        {
            return self.Append(YokiFrame.Coroutine.Allocate(coroutineGetter));
        }

        public static IAction ToAction(this IEnumerator self)
        {
            return YokiFrame.Coroutine.Allocate(() => self);
        }
    }
}