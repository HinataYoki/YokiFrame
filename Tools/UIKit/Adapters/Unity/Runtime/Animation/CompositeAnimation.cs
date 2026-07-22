#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>组合动画的子动画调度方式。</summary>
    public enum CompositeMode
    {
        Parallel,
        Sequential
    }

    /// <summary>把多个 IUIAnimation 按并行或顺序方式组合成单个转换。</summary>
    public sealed class CompositeAnimation : IUIAnimation
    {
        private readonly List<IUIAnimation> mAnimations = new(4);
        private PlaybackState mPlayback;
        private int mGeneration;

        /// <summary>创建指定模式的空组合动画。</summary>
        public CompositeAnimation(CompositeMode mode)
        {
            Mode = mode;
        }

        /// <summary>获取当前组合模式。</summary>
        public CompositeMode Mode { get; }

        /// <inheritdoc />
        public float Duration => CalculateDuration();

        /// <inheritdoc />
        public bool IsPlaying => mPlayback != null;

        /// <summary>向组合末尾添加一个非空动画。</summary>
        public CompositeAnimation Add(IUIAnimation animation)
        {
            if (animation != null) mAnimations.Add(animation);
            return this;
        }

        /// <summary>按枚举顺序添加多个非空动画。</summary>
        public CompositeAnimation AddRange(IEnumerable<IUIAnimation> animations)
        {
            if (animations == null) return this;
            foreach (IUIAnimation animation in animations) Add(animation);
            return this;
        }

        /// <inheritdoc />
        public void Play(RectTransform target, Action onComplete = null)
        {
            Stop();
            if (target == default || mAnimations.Count == 0)
            {
                if (onComplete != null) onComplete();
                return;
            }

            var state = new PlaybackState(++mGeneration, target, onComplete, mAnimations.Count);
            mPlayback = state;
            if (Mode == CompositeMode.Parallel) PlayParallel(state);
            else PlayNextSequential(state);
        }

        /// <inheritdoc />
        public void Stop()
        {
            ++mGeneration;
            mPlayback = null;
            for (var index = 0; index < mAnimations.Count; index++) mAnimations[index].Stop();
        }

        /// <inheritdoc />
        public void Reset(RectTransform target)
        {
            for (var index = 0; index < mAnimations.Count; index++) mAnimations[index].Reset(target);
        }

        /// <inheritdoc />
        public void SetToEndState(RectTransform target)
        {
            for (var index = 0; index < mAnimations.Count; index++)
                mAnimations[index].SetToEndState(target);
        }

        /// <inheritdoc />
        public void Recycle()
        {
            Stop();
            for (var index = 0; index < mAnimations.Count; index++) mAnimations[index].Recycle();
            mAnimations.Clear();
        }

        /// <summary>计算并行最大时长或顺序累计时长。</summary>
        private float CalculateDuration()
        {
            float duration = 0f;
            for (var index = 0; index < mAnimations.Count; index++)
            {
                float childDuration = mAnimations[index].Duration;
                if (Mode == CompositeMode.Sequential) duration += childDuration;
                else if (childDuration > duration) duration = childDuration;
            }
            return duration;
        }

        /// <summary>同时启动全部子动画，并在全部完成后提交组合回调。</summary>
        private void PlayParallel(PlaybackState state)
        {
            for (var index = 0; index < mAnimations.Count; index++)
            {
                IUIAnimation animation = mAnimations[index];
                animation.Play(state.Target, () => OnParallelChildCompleted(state));
            }
        }

        /// <summary>按列表顺序启动下一子动画，直到全部完成。</summary>
        private void PlayNextSequential(PlaybackState state)
        {
            if (!IsCurrent(state)) return;
            if (state.NextIndex >= mAnimations.Count)
            {
                Complete(state);
                return;
            }

            IUIAnimation animation = mAnimations[state.NextIndex++];
            animation.Play(state.Target, () => PlayNextSequential(state));
        }

        /// <summary>累计并行子动画完成数，并在达到总数时提交。</summary>
        private void OnParallelChildCompleted(PlaybackState state)
        {
            if (!IsCurrent(state)) return;
            state.CompletedCount++;
            if (state.CompletedCount >= state.TotalCount) Complete(state);
        }

        /// <summary>判断回调仍属于当前播放代次。</summary>
        private bool IsCurrent(PlaybackState state)
        {
            return ReferenceEquals(mPlayback, state) && state.Generation == mGeneration;
        }

        /// <summary>清理当前播放状态并执行一次完成回调。</summary>
        private void Complete(PlaybackState state)
        {
            if (!IsCurrent(state)) return;
            mPlayback = null;
            Action callback = state.OnComplete;
            state.OnComplete = null;
            if (callback != null) callback();
        }

        /// <summary>保存单次组合播放所需的可失效状态。</summary>
        private sealed class PlaybackState
        {
            /// <summary>创建一轮组合播放状态。</summary>
            internal PlaybackState(
                int generation,
                RectTransform target,
                Action onComplete,
                int totalCount)
            {
                Generation = generation;
                Target = target;
                OnComplete = onComplete;
                TotalCount = totalCount;
            }

            internal int Generation { get; }
            internal RectTransform Target { get; }
            internal int TotalCount { get; }
            internal int CompletedCount { get; set; }
            internal int NextIndex { get; set; }
            internal Action OnComplete { get; set; }
        }
    }
}
#endif
