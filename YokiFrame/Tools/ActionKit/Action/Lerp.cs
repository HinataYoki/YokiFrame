using System;
using UnityEngine;

namespace YokiFrame
{
    public class Lerp : IAction
    {
        public float A;
        public float B;
        public float Duration;
        public Action<float> OnLerp;
        public Action OnLerpFinish;
        private float mCurrentTime = 0.0f;

        private static readonly SimplePoolKit<Lerp> lerpPool = new(() => new Lerp());

        public static Lerp Allocate(float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            var retNode = lerpPool.Allocate();
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

        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }
        public bool Deinited { get; set; }
        public bool Paused { get; set; }

        public void OnInit()
        {
            ActionState = ActionStatus.NotStart;
            Paused = false;
            mCurrentTime = 0.0f;
        }

        public void OnStart()
        {
            mCurrentTime = 0.0f;
            OnLerp?.Invoke(Mathf.Lerp(A, B, 0));
        }

        public void OnExecute(float dt)
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

        public void OnFinish()
        {
            OnLerp?.Invoke(Mathf.Lerp(A, B, 1.0f));
            OnLerpFinish?.Invoke();
        }

        public void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                OnLerp = null;
                OnLerpFinish = null;

                MonoRecycler.AddRecycleCallback(new ActionRecycler<Lerp>(lerpPool, this));
            }
        }

        string IAction.LogError() => $"类 {OnLerp.Method.DeclaringType} 方法 {OnLerp.Method} 出错 或者 类 {OnLerpFinish.Method.DeclaringType} 方法 {OnLerpFinish.Method} 出错";
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