using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Collections;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 淡入淡出场景过渡效果
    /// 优先使用 UniTask，无 UniTask 时回退到协程实现
    /// </summary>
    public class FadeTransition : ISceneTransition
    {
        private readonly float mFadeDuration;
        private readonly Color mFadeColor;
        private GameObject mFadeObject;
        private CanvasGroup mCanvasGroup;
        private float mProgress;
        private bool mIsTransitioning;
        private CancellationTokenSource mCts;

#if !YOKIFRAME_UNITASK_SUPPORT
        private FadeDriver mDriver;
#endif

        /// <summary>
        /// 当前过渡进度（0-1）
        /// </summary>
        public float Progress => mProgress;

        /// <summary>
        /// 是否正在过渡中
        /// </summary>
        public bool IsTransitioning => mIsTransitioning;

        /// <summary>
        /// 创建淡入淡出过渡效果
        /// </summary>
        /// <param name="fadeDuration">淡入淡出持续时间（秒）</param>
        /// <param name="fadeColor">淡入淡出颜色</param>
        public FadeTransition(float fadeDuration = 0.5f, Color? fadeColor = null)
        {
            mFadeDuration = Mathf.Max(0.1f, fadeDuration);
            mFadeColor = fadeColor ?? Color.black;
        }

        /// <summary>
        /// 淡出效果（旧场景消失，屏幕变黑）
        /// </summary>
        public void FadeOutAsync(Action onComplete)
        {
            mIsTransitioning = true;
            mProgress = 0f;
            
            EnsureFadeObject();
            mCanvasGroup.alpha = 0f;
            mFadeObject.SetActive(true);

            CancelCurrentFade();
            mCts = new CancellationTokenSource();

#if YOKIFRAME_UNITASK_SUPPORT
            FadeOutUniTask(onComplete, mCts.Token).Forget();
#else
            mDriver.StartFade(0f, 1f, mFadeDuration, 
                alpha =>
                {
                    mCanvasGroup.alpha = alpha;
                    mProgress = alpha * 0.5f;
                },
                () =>
                {
                    mProgress = 0.5f;
                    onComplete?.Invoke();
                });
#endif
        }

        /// <summary>
        /// 淡入效果（新场景出现，屏幕恢复）
        /// </summary>
        public void FadeInAsync(Action onComplete)
        {
            EnsureFadeObject();
            mCanvasGroup.alpha = 1f;

            CancelCurrentFade();
            mCts = new CancellationTokenSource();

#if YOKIFRAME_UNITASK_SUPPORT
            FadeInUniTask(onComplete, mCts.Token).Forget();
#else
            mDriver.StartFade(1f, 0f, mFadeDuration,
                alpha =>
                {
                    mCanvasGroup.alpha = alpha;
                    mProgress = 0.5f + (1f - alpha) * 0.5f;
                },
                () =>
                {
                    mProgress = 1f;
                    mIsTransitioning = false;
                    mFadeObject.SetActive(false);
                    onComplete?.Invoke();
                });
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private async UniTaskVoid FadeOutUniTask(Action onComplete, CancellationToken ct)
        {
            float elapsed = 0f;
            
            while (elapsed < mFadeDuration)
            {
                if (ct.IsCancellationRequested) return;
                
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mFadeDuration);
                mCanvasGroup.alpha = t;
                mProgress = t * 0.5f;
                
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            mCanvasGroup.alpha = 1f;
            mProgress = 0.5f;
            onComplete?.Invoke();
        }

        private async UniTaskVoid FadeInUniTask(Action onComplete, CancellationToken ct)
        {
            float elapsed = 0f;
            
            while (elapsed < mFadeDuration)
            {
                if (ct.IsCancellationRequested) return;
                
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mFadeDuration);
                float alpha = 1f - t;
                mCanvasGroup.alpha = alpha;
                mProgress = 0.5f + t * 0.5f;
                
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            mCanvasGroup.alpha = 0f;
            mProgress = 1f;
            mIsTransitioning = false;
            mFadeObject.SetActive(false);
            onComplete?.Invoke();
        }
#endif

        private void CancelCurrentFade()
        {
            if (mCts != null)
            {
                mCts.Cancel();
                mCts.Dispose();
                mCts = null;
            }
        }

        /// <summary>
        /// 确保淡入淡出对象存在
        /// </summary>
        private void EnsureFadeObject()
        {
            if (mFadeObject != null) return;

            mFadeObject = new GameObject("[SceneKit_FadeTransition]");
            mFadeObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(mFadeObject);

            var canvas = mFadeObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            mCanvasGroup = mFadeObject.AddComponent<CanvasGroup>();
            mCanvasGroup.blocksRaycasts = true;

#if !YOKIFRAME_UNITASK_SUPPORT
            mDriver = mFadeObject.AddComponent<FadeDriver>();
#endif

            var imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(mFadeObject.transform, false);
            
            var rectTransform = imageObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var image = imageObj.AddComponent<Image>();
            image.color = mFadeColor;
            image.raycastTarget = false;

            mFadeObject.SetActive(false);
        }

        /// <summary>
        /// 销毁过渡效果对象
        /// </summary>
        public void Dispose()
        {
            CancelCurrentFade();
            
            if (mFadeObject != null)
            {
                UnityEngine.Object.Destroy(mFadeObject);
                mFadeObject = null;
                mCanvasGroup = null;
#if !YOKIFRAME_UNITASK_SUPPORT
                mDriver = null;
#endif
            }
        }

#if !YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 内部动画驱动器 - 无 UniTask 时使用协程实现
        /// </summary>
        private class FadeDriver : MonoBehaviour
        {
            private Coroutine mCurrentCoroutine;

            public void StartFade(float from, float to, float duration, Action<float> onUpdate, Action onComplete)
            {
                if (mCurrentCoroutine != null)
                {
                    StopCoroutine(mCurrentCoroutine);
                }
                mCurrentCoroutine = StartCoroutine(FadeCoroutine(from, to, duration, onUpdate, onComplete));
            }

            private IEnumerator FadeCoroutine(float from, float to, float duration, Action<float> onUpdate, Action onComplete)
            {
                float elapsed = 0f;
                
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float value = Mathf.Lerp(from, to, t);
                    onUpdate?.Invoke(value);
                    yield return null;
                }

                onUpdate?.Invoke(to);
                onComplete?.Invoke();
                mCurrentCoroutine = null;
            }
        }
#endif
    }
}
