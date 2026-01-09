using System;
using System.Collections.Generic;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 组合动画模式
    /// </summary>
    public enum CompositeMode
    {
        /// <summary>
        /// 并行播放所有动画
        /// </summary>
        Parallel,
        /// <summary>
        /// 顺序播放所有动画
        /// </summary>
        Sequential
    }

    /// <summary>
    /// 组合动画
    /// </summary>
    public class CompositeAnimation : IUIAnimation
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private readonly List<IUIAnimation> mAnimations = new();
        private readonly CompositeMode mMode;
        private int mCompletedCount;
        private bool mIsPlaying;

        public float Duration
        {
            get
            {
                if (mAnimations.Count == 0) return 0f;
                
                if (mMode == CompositeMode.Parallel)
                {
                    float maxDuration = 0f;
                    foreach (var anim in mAnimations)
                    {
                        if (anim.Duration > maxDuration)
                            maxDuration = anim.Duration;
                    }
                    return maxDuration;
                }
                else
                {
                    float totalDuration = 0f;
                    foreach (var anim in mAnimations)
                    {
                        totalDuration += anim.Duration;
                    }
                    return totalDuration;
                }
            }
        }

        public bool IsPlaying => mIsPlaying;

        public CompositeAnimation(CompositeMode mode = CompositeMode.Parallel)
        {
            mMode = mode;
        }

        /// <summary>
        /// 添加动画
        /// </summary>
        public CompositeAnimation Add(IUIAnimation animation)
        {
            if (animation != null)
            {
                mAnimations.Add(animation);
            }
            return this;
        }

        /// <summary>
        /// 添加多个动画
        /// </summary>
        public CompositeAnimation AddRange(IEnumerable<IUIAnimation> animations)
        {
            foreach (var anim in animations)
            {
                Add(anim);
            }
            return this;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null || mAnimations.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            Stop();
            mIsPlaying = true;
            mCompletedCount = 0;

            if (mMode == CompositeMode.Parallel)
            {
                PlayParallel(target, onComplete);
            }
            else
            {
                PlaySequential(target, 0, onComplete);
            }
        }

        private void PlayParallel(RectTransform target, Action onComplete)
        {
            int totalCount = mAnimations.Count;
            
            foreach (var anim in mAnimations)
            {
                anim.Play(target, () =>
                {
                    mCompletedCount++;
                    if (mCompletedCount >= totalCount)
                    {
                        mIsPlaying = false;
                        onComplete?.Invoke();
                    }
                });
            }
        }

        private void PlaySequential(RectTransform target, int index, Action onComplete)
        {
            if (index >= mAnimations.Count)
            {
                mIsPlaying = false;
                onComplete?.Invoke();
                return;
            }

            mAnimations[index].Play(target, () =>
            {
                PlaySequential(target, index + 1, onComplete);
            });
        }

        public void Stop()
        {
            foreach (var anim in mAnimations)
            {
                anim.Stop();
            }
            mIsPlaying = false;
        }

        public void Reset(RectTransform target)
        {
            foreach (var anim in mAnimations)
            {
                anim.Reset(target);
            }
        }

        public void SetToEndState(RectTransform target)
        {
            foreach (var anim in mAnimations)
            {
                anim.SetToEndState(target);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == null || mAnimations.Count == 0) return;

            Stop();
            mIsPlaying = true;

            try
            {
                if (mMode == CompositeMode.Parallel)
                {
                    await PlayParallelUniTaskAsync(target, ct);
                }
                else
                {
                    await PlaySequentialUniTaskAsync(target, ct);
                }
            }
            finally
            {
                mIsPlaying = false;
            }
        }

        private async UniTask PlayParallelUniTaskAsync(RectTransform target, CancellationToken ct)
        {
            var tasks = new List<UniTask>();
            
            foreach (var anim in mAnimations)
            {
                if (anim is IUIAnimationUniTask uniTaskAnim)
                {
                    tasks.Add(uniTaskAnim.PlayUniTaskAsync(target, ct));
                }
                else
                {
                    // 回退到回调方式，包装为 UniTask
                    var tcs = new UniTaskCompletionSource();
                    anim.Play(target, () => tcs.TrySetResult());
                    tasks.Add(tcs.Task);
                }
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlaySequentialUniTaskAsync(RectTransform target, CancellationToken ct)
        {
            foreach (var anim in mAnimations)
            {
                ct.ThrowIfCancellationRequested();
                
                if (anim is IUIAnimationUniTask uniTaskAnim)
                {
                    await uniTaskAnim.PlayUniTaskAsync(target, ct);
                }
                else
                {
                    var tcs = new UniTaskCompletionSource();
                    anim.Play(target, () => tcs.TrySetResult());
                    await tcs.Task;
                }
            }
        }
#endif
    }
}
