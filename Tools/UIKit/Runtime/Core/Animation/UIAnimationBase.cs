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
        /// <summary>曲线采样点数量</summary>
        private const int CURVE_SAMPLE_COUNT = 32;
        
        /// <summary>默认 EaseInOut 曲线静态采样缓存</summary>
        private static readonly float[] sDefaultCurveSamples = PreSampleDefaultCurve();
        
        protected float mDuration;
        protected AnimationCurve mCurve;
        protected Coroutine mCoroutine;
        protected MonoBehaviour mCoroutineRunner;
        
        /// <summary>自定义曲线采样值（仅非默认曲线时分配）</summary>
        private float[] mCustomCurveSamples;
        
        /// <summary>是否使用默认曲线</summary>
        private bool mUseDefaultCurve = true;
        
        /// <summary>带状态的回调</summary>
        private Action<object> mOnCompleteWithState;
        private object mCallbackState;
        
        public float Duration => mDuration;
        public bool IsPlaying { get; protected set; }

        /// <summary>
        /// 无参构造（池化支持）
        /// </summary>
        protected UIAnimationBase() { }

        protected UIAnimationBase(float duration, AnimationCurve curve)
        {
            SetupBase(duration, curve);
        }
        
        /// <summary>
        /// 预采样默认 EaseInOut 曲线（静态初始化）
        /// </summary>
        private static float[] PreSampleDefaultCurve()
        {
            var defaultCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            var samples = new float[CURVE_SAMPLE_COUNT + 1];
            for (int i = 0; i <= CURVE_SAMPLE_COUNT; i++)
            {
                float t = (float)i / CURVE_SAMPLE_COUNT;
                samples[i] = defaultCurve.Evaluate(t);
            }
            return samples;
        }
        
        /// <summary>
        /// 设置基础参数（池化复用）
        /// </summary>
        protected void SetupBase(float duration, AnimationCurve curve = null)
        {
            mDuration = duration;
            mCurve = curve;
            
            // 判断是否使用默认曲线
            if (curve == default)
            {
                mUseDefaultCurve = true;
            }
            else
            {
                mUseDefaultCurve = false;
                PreSampleCustomCurve(curve);
            }
        }
        
        /// <summary>
        /// 预采样自定义动画曲线
        /// </summary>
        private void PreSampleCustomCurve(AnimationCurve curve)
        {
            mCustomCurveSamples ??= new float[CURVE_SAMPLE_COUNT + 1];
            for (int i = 0; i <= CURVE_SAMPLE_COUNT; i++)
            {
                float t = (float)i / CURVE_SAMPLE_COUNT;
                mCustomCurveSamples[i] = curve.Evaluate(t);
            }
        }
        
        /// <summary>
        /// 从预采样数据获取曲线值
        /// </summary>
        protected float GetCurveValue(float normalizedTime)
        {
            var samples = mUseDefaultCurve ? sDefaultCurveSamples : mCustomCurveSamples;
            if (samples == default) return normalizedTime;
            
            float scaledTime = normalizedTime * CURVE_SAMPLE_COUNT;
            int index = Mathf.FloorToInt(scaledTime);
            
            if (index >= CURVE_SAMPLE_COUNT)
                return samples[CURVE_SAMPLE_COUNT];
            
            float fraction = scaledTime - index;
            return Mathf.Lerp(samples[index], samples[index + 1], fraction);
        }

        public virtual void Play(RectTransform target, Action onComplete = null)
        {
            Play(target, onComplete == default ? null : static state => ((Action)state)?.Invoke(), onComplete);
        }
        
        public virtual void Play(RectTransform target, Action<object> onComplete, object state)
        {
            if (target == default)
            {
                onComplete?.Invoke(state);
                return;
            }

            Stop();
            
            if (mCoroutineRunner == default || mCoroutineRunner.gameObject != target.gameObject)
            {
                if (!target.TryGetComponent(out mCoroutineRunner))
                {
                    SetToEndState(target);
                    onComplete?.Invoke(state);
                    return;
                }
            }
            
            if (!mCoroutineRunner.gameObject.activeInHierarchy)
            {
                SetToEndState(target);
                onComplete?.Invoke(state);
                return;
            }

            mOnCompleteWithState = onComplete;
            mCallbackState = state;
            IsPlaying = true;
            mCoroutine = mCoroutineRunner.StartCoroutine(PlayCoroutine(target));
        }

        public virtual void Stop()
        {
            if (mCoroutine != default && mCoroutineRunner != default)
            {
                mCoroutineRunner.StopCoroutine(mCoroutine);
                mCoroutine = null;
            }
            IsPlaying = false;
            mOnCompleteWithState = null;
            mCallbackState = null;
        }

        public abstract void Reset(RectTransform target);
        public abstract void SetToEndState(RectTransform target);
        public abstract void Recycle();
        
        protected abstract void ApplyAnimation(RectTransform target, float normalizedTime);

        protected virtual IEnumerator PlayCoroutine(RectTransform target)
        {
            float elapsed = 0f;
            float inverseDuration = 1f / mDuration;
            
            while (elapsed < mDuration)
            {
                // 检测 target 是否被销毁
                if (target == default)
                {
                    IsPlaying = false;
                    mCoroutine = null;
                    mOnCompleteWithState = null;
                    mCallbackState = null;
                    yield break;
                }
                
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed * inverseDuration);
                float curveValue = GetCurveValue(t);
                
                ApplyAnimation(target, curveValue);
                
                yield return null;
            }
            
            // 最终状态设置前再次检测
            if (target != default)
            {
                SetToEndState(target);
            }
            
            IsPlaying = false;
            mCoroutine = null;
            
            var callback = mOnCompleteWithState;
            var callbackState = mCallbackState;
            mOnCompleteWithState = null;
            mCallbackState = null;
            callback?.Invoke(callbackState);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public virtual async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == default) return;

            Stop();
            IsPlaying = true;

            try
            {
                float elapsed = 0f;
                float inverseDuration = 1f / mDuration;
                
                while (elapsed < mDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    // 检测 target 是否被销毁
                    if (target == default)
                    {
                        return;
                    }
                    
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed * inverseDuration);
                    float curveValue = GetCurveValue(t);
                    
                    ApplyAnimation(target, curveValue);
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                
                if (target != default)
                {
                    SetToEndState(target);
                }
            }
            catch (OperationCanceledException)
            {
                if (target != default)
                {
                    SetToEndState(target);
                }
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
