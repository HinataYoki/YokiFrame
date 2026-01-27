using System;
using UnityEngine;

namespace YokiFrame
{
    public class Lerp : ActionBase
    {
        public float A;
        public float B;
        public float Duration;
        public Action<float> OnLerp;
        public Action OnLerpFinish;
        private float mCurrentTime = 0.0f;

        private static readonly SimplePoolKit<Lerp> mPool = new(() => new Lerp());

        static Lerp()
        {
            ActionKitPlayerLoopSystem.RegisterRecycleProcessor<Lerp>();
        }

        public static Lerp Allocate(float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            var retNode = mPool.Allocate();
            retNode.ActionID = ActionKit.ID_GENERATOR++;
            retNode.Deinited = false;
            retNode.OnInit();
            retNode.A = a;
            retNode.B = b;
            retNode.Duration = duration;
            retNode.OnLerp = onLerp;
            retNode.OnLerpFinish = onLerpFinish;
            return retNode;
        }

        public override void OnInit()
        {
            base.OnInit();
            mCurrentTime = 0.0f;
        }

        public override void OnStart()
        {
            mCurrentTime = 0.0f;
            OnLerp?.Invoke(Mathf.Lerp(A, B, 0));
        }

        public override void OnExecute(float dt)
        {
            mCurrentTime += dt;
            if (mCurrentTime < Duration)
            {
                OnLerp?.Invoke(Mathf.Lerp(A, B, mCurrentTime / Duration));
            }
            else
            {
                this.Finish();
            }
        }

        public override void OnFinish()
        {
            OnLerp?.Invoke(Mathf.Lerp(A, B, 1.0f));
            OnLerpFinish?.Invoke();
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                OnLerp = null;
                OnLerpFinish = null;

                ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<Lerp>(mPool, this));
            }
        }

        public override string GetDebugInfo()
        {
            var lerpInfo = OnLerp != null ? $"{OnLerp.Method.DeclaringType}.{OnLerp.Method.Name}" : "null";
            return $"Lerp({A}->{B}) -> {lerpInfo}";
        }
    }

    public static class LerpExtension
    {
        public static ISequence Lerp(this ISequence self, float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            return self.Append(YokiFrame.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish));
        }

        public static ISequence Lerp01(this ISequence self, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            return self.Append(YokiFrame.Lerp.Allocate(0, 1, duration, onLerp, onLerpFinish));
        }
    }
}