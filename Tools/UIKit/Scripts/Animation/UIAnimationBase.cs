using System;
using System.Collections;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UI 动画基类（协程实现）
    /// </summary>
    public abstract class UIAnimationBase : IUIAnimation
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        protected float mDuration;
        protected AnimationCurve mCurve;
        protected Coroutine mCoroutine;
        protected MonoBehaviour mCoroutineRunner;
        
        public float Duration => mDuration;
        public bool IsPlaying { get; protected set; }

        protected UIAnimationBase(float duration, AnimationCurve curve)
        {
            mDuration = duration;
            mCurve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        }

        public virtual void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Stop();
            
            mCoroutineRunner = target.GetComponent<MonoBehaviour>();
            if (mCoroutineRunner == null || !mCoroutineRunner.gameObject.activeInHierarchy)
            {
                // 无法运行协程，直接设置到结束状态
                SetToEndState(target);
                onComplete?.Invoke();
                return;
            }

            IsPlaying = true;
            mCoroutine = mCoroutineRunner.StartCoroutine(PlayCoroutine(target, onComplete));
        }

        public virtual void Stop()
        {
            if (mCoroutine != null && mCoroutineRunner != null)
            {
                mCoroutineRunner.StopCoroutine(mCoroutine);
                mCoroutine = null;
            }
            IsPlaying = false;
        }

        public abstract void Reset(RectTransform target);
        public abstract void SetToEndState(RectTransform target);
        
        protected abstract void ApplyAnimation(RectTransform target, float normalizedTime);

        protected virtual IEnumerator PlayCoroutine(RectTransform target, Action onComplete)
        {
            float elapsed = 0f;
            
            while (elapsed < mDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mDuration);
                float curveValue = mCurve.Evaluate(t);
                
                ApplyAnimation(target, curveValue);
                
                yield return null;
            }
            
            SetToEndState(target);
            IsPlaying = false;
            mCoroutine = null;
            onComplete?.Invoke();
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public virtual async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == null) return;

            Stop();
            IsPlaying = true;

            try
            {
                float elapsed = 0f;
                
                while (elapsed < mDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / mDuration);
                    float curveValue = mCurve.Evaluate(t);
                    
                    ApplyAnimation(target, curveValue);
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                
                SetToEndState(target);
            }
            catch (OperationCanceledException)
            {
                // 动画被取消，设置到结束状态
                SetToEndState(target);
                throw;
            }
            finally
            {
                IsPlaying = false;
            }
        }
#endif
    }
}
