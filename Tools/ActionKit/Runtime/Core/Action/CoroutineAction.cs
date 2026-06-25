using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame
{
    internal class CoroutineAction : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<CoroutineAction> sPool = new(static () => new CoroutineAction());

        private Func<IEnumerator> mCoroutineGetter;
        private IEnumerator mEnumerator;
        private readonly List<IEnumerator> mEnumeratorStack = new(4);

        static CoroutineAction()
        {
            ActionKitScheduler.RegisterRecycleProcessor<CoroutineAction>();
        }

        internal static CoroutineAction Allocate(Func<IEnumerator> coroutineGetter)
        {
            var coroutine = sPool.Allocate();
            coroutine.ActionID = ActionKit.sIdGenerator++;
            coroutine.Deinited = false;
            coroutine.OnInit();
            coroutine.mCoroutineGetter = coroutineGetter;
            return coroutine;
        }

        public override void OnInit()
        {
            base.OnInit();
            mEnumerator = null;
            mEnumeratorStack.Clear();
        }

        public override void OnStart()
        {
            if (mCoroutineGetter == null)
            {
                this.Finish();
                return;
            }

            mEnumerator = mCoroutineGetter.Invoke();
            if (mEnumerator == null || AdvanceEnumerator())
                this.Finish();
        }

        public override void OnExecute(float dt)
        {
            if (mEnumerator == null || AdvanceEnumerator())
                this.Finish();
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            mEnumerator = null;
            mEnumeratorStack.Clear();
            mCoroutineGetter = null;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<CoroutineAction>(sPool, this));
        }

        public override string GetDebugInfo() =>
            mCoroutineGetter != null ? $"Coroutine -> {mCoroutineGetter.Method.DeclaringType}.{mCoroutineGetter.Method.Name}" : "Coroutine";

        private bool AdvanceEnumerator()
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = mEnumerator.MoveNext();
                }
                catch (Exception e)
                {
                    ActionKitRuntimeLog.Error("[ActionKit] Coroutine 执行异常: " + e.Message);
                    return true;
                }

                if (!hasNext)
                {
                    if (mEnumeratorStack.Count == 0)
                        return true;

                    var lastIndex = mEnumeratorStack.Count - 1;
                    mEnumerator = mEnumeratorStack[lastIndex];
                    mEnumeratorStack.RemoveAt(lastIndex);
                    continue;
                }

                if (!(mEnumerator.Current is IEnumerator nestedEnumerator))
                    return false;

                mEnumeratorStack.Add(mEnumerator);
                mEnumerator = nestedEnumerator;
            }
        }
    }
}
