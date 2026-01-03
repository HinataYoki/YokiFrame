using System;
using System.Collections;

namespace YokiFrame
{
    internal class Coroutine : ActionBase
    {
        /// <summary>
        /// 协程
        /// </summary>
        private Func<IEnumerator> mCoroutineGetter = null;
        /// <summary>
        /// 协程任务池
        /// </summary>
        private static readonly SimplePoolKit<Coroutine> mPool = new(() => new Coroutine());

        public static Coroutine Allocate(Func<IEnumerator> coroutineGetter)
        {
            var coroutine = mPool.Allocate();
            coroutine.ActionID = ActionKit.ID_GENERATOR++;
            coroutine.Deinited = false;
            coroutine.OnInit();
            coroutine.mCoroutineGetter = coroutineGetter;
            return coroutine;
        }
        
        public override void OnStart()
        {
            MonoGlobalExecutor.ExecuteCoroutine(mCoroutineGetter(), () =>
            {
                this.Finish();
            });
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCoroutineGetter = null;
                MonoRecycler.AddRecycleCallback(new ActionRecycler<Coroutine>(mPool, this));
            }
        }

        public override string GetDebugInfo() => 
            mCoroutineGetter != null ? $"Coroutine -> {mCoroutineGetter.Method.DeclaringType}.{mCoroutineGetter.Method.Name}" : "Coroutine";
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