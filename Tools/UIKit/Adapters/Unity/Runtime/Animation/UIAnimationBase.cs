#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>使用 Unity 协程驱动的可取消动画基类。</summary>
    public abstract class UIAnimationBase : IUIAnimation
    {
        private sealed class AnimationRunner : MonoBehaviour { }
        private static AnimationRunner sRunner;
        private readonly AnimationCurve mCurve;
        private Coroutine mCoroutine;
        private Action mOnComplete;

        /// <summary>使用共享协程宿主创建动画基础参数，并保留可选插值曲线。</summary>
        protected UIAnimationBase(float duration, AnimationCurve curve = null)
        {
            Duration = Mathf.Max(0f, duration);
            mCurve = curve;
        }

        /// <inheritdoc />
        public float Duration { get; }

        /// <inheritdoc />
        public bool IsPlaying { get; private set; }

        /// <inheritdoc />
        public void Play(RectTransform target, Action onComplete = null)
        {
            Stop();
            if (target == default)
            {
                if (onComplete != null) onComplete();
                return;
            }
            Reset(target);
            mOnComplete = onComplete;
            if (Duration <= 0f)
            {
                SetToEndState(target);
                Complete();
                return;
            }
            EnsureRunner();
            IsPlaying = true;
            mCoroutine = sRunner.StartCoroutine(PlayCoroutine(target));
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (mCoroutine != null && sRunner != default) sRunner.StopCoroutine(mCoroutine);
            mCoroutine = null;
            mOnComplete = null;
            IsPlaying = false;
        }

        /// <inheritdoc />
        public abstract void Reset(RectTransform target);

        /// <inheritdoc />
        public abstract void SetToEndState(RectTransform target);

        /// <inheritdoc />
        public virtual void Recycle()
        {
            Stop();
        }

        /// <summary>按未缩放时间插值并在结束时调用回调。</summary>
        private IEnumerator PlayCoroutine(RectTransform target)
        {
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                if (target == default) yield break;
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / Duration);
                Apply(target, EvaluateCurve(normalizedTime));
                yield return null;
            }
            if (target != default) SetToEndState(target);
            Complete();
        }

        /// <summary>应用单帧动画值，由具体动画提供插值细节。</summary>
        protected abstract void Apply(RectTransform target, float normalizedTime);

        /// <summary>使用配置曲线转换归一化进度；未配置曲线时保持线性。</summary>
        protected float EvaluateCurve(float normalizedTime)
        {
            return mCurve == null ? normalizedTime : mCurve.Evaluate(normalizedTime);
        }

        /// <summary>确保共享协程宿主存在且不会随场景卸载。</summary>
        private static void EnsureRunner()
        {
            if (sRunner != default) return;
            GameObject host = new("[YokiFrame UIKit Animation]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            sRunner = host.AddComponent<AnimationRunner>();
        }

        /// <summary>提交动画终态并执行一次完成回调。</summary>
        private void Complete()
        {
            mCoroutine = null;
            IsPlaying = false;
            Action callback = mOnComplete;
            mOnComplete = null;
            if (callback != null) callback();
        }
    }
}
#endif
