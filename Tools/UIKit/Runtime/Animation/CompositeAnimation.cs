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
    public class CompositeAnimation : IUIAnimation, IPoolable
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private readonly List<IUIAnimation> mAnimations = new(4);
        private CompositeMode mMode;
        private bool mIsPlaying;
        
        /// <summary>缓存的动画数量</summary>
        private int mAnimationCount;
        
        /// <summary>缓存的总时长</summary>
        private float mCachedDuration;
        private bool mDurationDirty = true;
        
        /// <summary>池化的并行上下文</summary>
        private ParallelContext mParallelContext;
        
        /// <summary>池化的顺序上下文</summary>
        private SequentialContext mSequentialContext;

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>复用的 UniTask 数组</summary>
        private UniTask[] mTasksBuffer;
#endif

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled()
        {
            Stop();
            mAnimations.Clear();
            mAnimationCount = 0;
            mDurationDirty = true;
        }
        
        #endregion

        /// <summary>
        /// 组合模式
        /// </summary>
        public CompositeMode Mode => mMode;

        public float Duration
        {
            get
            {
                if (!mDurationDirty) return mCachedDuration;
                
                mCachedDuration = CalculateDuration();
                mDurationDirty = false;
                return mCachedDuration;
            }
        }
        
        private float CalculateDuration()
        {
            if (mAnimationCount == 0) return 0f;
            
            if (mMode == CompositeMode.Parallel)
            {
                float maxDuration = 0f;
                for (int i = 0; i < mAnimationCount; i++)
                {
                    float d = mAnimations[i].Duration;
                    if (d > maxDuration) maxDuration = d;
                }
                return maxDuration;
            }
            else
            {
                float totalDuration = 0f;
                for (int i = 0; i < mAnimationCount; i++)
                {
                    totalDuration += mAnimations[i].Duration;
                }
                return totalDuration;
            }
        }

        public bool IsPlaying => mIsPlaying;

        /// <summary>
        /// 设置组合模式（池化复用时调用）
        /// </summary>
        internal CompositeAnimation Setup(CompositeMode mode)
        {
            mMode = mode;
            return this;
        }

        /// <summary>
        /// 添加动画
        /// </summary>
        public CompositeAnimation Add(IUIAnimation animation)
        {
            if (animation != default)
            {
                mAnimations.Add(animation);
                mAnimationCount = mAnimations.Count;
                mDurationDirty = true;
            }
            return this;
        }

        /// <summary>
        /// 添加多个动画（List 版本）
        /// </summary>
        public CompositeAnimation AddRange(List<IUIAnimation> animations)
        {
            for (int i = 0; i < animations.Count; i++)
            {
                var anim = animations[i];
                if (anim != default)
                {
                    mAnimations.Add(anim);
                }
            }
            mAnimationCount = mAnimations.Count;
            mDurationDirty = true;
            return this;
        }
        
        /// <summary>
        /// 添加多个动画（数组版本）
        /// </summary>
        public CompositeAnimation AddRange(IUIAnimation[] animations)
        {
            for (int i = 0; i < animations.Length; i++)
            {
                var anim = animations[i];
                if (anim != default)
                {
                    mAnimations.Add(anim);
                }
            }
            mAnimationCount = mAnimations.Count;
            mDurationDirty = true;
            return this;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            Play(target, onComplete == default ? null : static state => ((Action)state)?.Invoke(), onComplete);
        }
        
        public void Play(RectTransform target, Action<object> onComplete, object state)
        {
            if (target == default || mAnimationCount == 0)
            {
                onComplete?.Invoke(state);
                return;
            }

            Stop();
            mIsPlaying = true;

            if (mMode == CompositeMode.Parallel)
            {
                PlayParallel(target, onComplete, state);
            }
            else
            {
                PlaySequential(target, 0, onComplete, state);
            }
        }

        private void PlayParallel(RectTransform target, Action<object> onComplete, object state)
        {
            int totalCount = mAnimationCount;
            
            // 复用上下文对象
            mParallelContext ??= new ParallelContext();
            mParallelContext.TotalCount = totalCount;
            mParallelContext.CompletedCount = 0;
            mParallelContext.OnComplete = onComplete;
            mParallelContext.State = state;
            mParallelContext.Animation = this;
            
            for (int i = 0; i < totalCount; i++)
            {
                mAnimations[i].Play(target, static ctx =>
                {
                    var c = (ParallelContext)ctx;
                    c.CompletedCount++;
                    if (c.CompletedCount >= c.TotalCount)
                    {
                        c.Animation.mIsPlaying = false;
                        var callback = c.OnComplete;
                        var callbackState = c.State;
                        // 清理引用避免泄漏
                        c.OnComplete = null;
                        c.State = null;
                        callback?.Invoke(callbackState);
                    }
                }, mParallelContext);
            }
        }
        
        /// <summary>
        /// 并行播放上下文（复用）
        /// </summary>
        private class ParallelContext
        {
            public int TotalCount;
            public int CompletedCount;
            public Action<object> OnComplete;
            public object State;
            public CompositeAnimation Animation;
        }

        private void PlaySequential(RectTransform target, int index, Action<object> onComplete, object state)
        {
            if (index >= mAnimationCount)
            {
                mIsPlaying = false;
                onComplete?.Invoke(state);
                return;
            }

            // 复用上下文对象
            mSequentialContext ??= new SequentialContext();
            mSequentialContext.Target = target;
            mSequentialContext.Index = index;
            mSequentialContext.OnComplete = onComplete;
            mSequentialContext.State = state;
            mSequentialContext.Animation = this;

            mAnimations[index].Play(target, static ctx =>
            {
                var c = (SequentialContext)ctx;
                c.Animation.PlaySequential(c.Target, c.Index + 1, c.OnComplete, c.State);
            }, mSequentialContext);
        }
        
        /// <summary>
        /// 顺序播放上下文（复用）
        /// </summary>
        private class SequentialContext
        {
            public RectTransform Target;
            public int Index;
            public Action<object> OnComplete;
            public object State;
            public CompositeAnimation Animation;
        }

        public void Stop()
        {
            for (int i = 0; i < mAnimationCount; i++)
            {
                mAnimations[i].Stop();
            }
            mIsPlaying = false;
        }

        public void Reset(RectTransform target)
        {
            for (int i = 0; i < mAnimationCount; i++)
            {
                mAnimations[i].Reset(target);
            }
        }

        public void SetToEndState(RectTransform target)
        {
            for (int i = 0; i < mAnimationCount; i++)
            {
                mAnimations[i].SetToEndState(target);
            }
        }
        
        /// <summary>
        /// 归还到池（同时归还子动画）
        /// </summary>
        public void Recycle()
        {
            // 先归还所有子动画
            for (int i = 0; i < mAnimationCount; i++)
            {
                mAnimations[i].Recycle();
            }
            // 再归还自身
            SafePoolKit<CompositeAnimation>.Instance.Recycle(this);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == default || mAnimationCount == 0) return;

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
            // 确保缓冲区足够大
            if (mTasksBuffer == default || mTasksBuffer.Length < mAnimationCount)
            {
                mTasksBuffer = new UniTask[Mathf.Max(mAnimationCount, 4)];
            }
            
            for (int i = 0; i < mAnimationCount; i++)
            {
                var anim = mAnimations[i];
                if (anim is IUIAnimationUniTask uniTaskAnim)
                {
                    mTasksBuffer[i] = uniTaskAnim.PlayUniTaskAsync(target, ct);
                }
                else
                {
                    // 使用 AutoResetUniTaskCompletionSource 避免 GC
                    var tcs = AutoResetUniTaskCompletionSource.Create();
                    anim.Play(target, static state => ((AutoResetUniTaskCompletionSource)state).TrySetResult(), tcs);
                    mTasksBuffer[i] = tcs.Task;
                }
            }

            // 使用 Span 切片避免额外数组分配
            await UniTask.WhenAll(new ArraySegment<UniTask>(mTasksBuffer, 0, mAnimationCount));
        }

        private async UniTask PlaySequentialUniTaskAsync(RectTransform target, CancellationToken ct)
        {
            for (int i = 0; i < mAnimationCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                
                var anim = mAnimations[i];
                if (anim is IUIAnimationUniTask uniTaskAnim)
                {
                    await uniTaskAnim.PlayUniTaskAsync(target, ct);
                }
                else
                {
                    // 使用 AutoResetUniTaskCompletionSource 避免 GC
                    var tcs = AutoResetUniTaskCompletionSource.Create();
                    anim.Play(target, static state => ((AutoResetUniTaskCompletionSource)state).TrySetResult(), tcs);
                    await tcs.Task;
                }
            }
        }
#endif
    }
}
