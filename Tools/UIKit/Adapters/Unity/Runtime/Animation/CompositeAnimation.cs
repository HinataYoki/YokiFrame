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

        /// <summary>
        /// 向组合末尾添加一个非空动画，并拒绝会形成环的添加。
        /// </summary>
        /// <remarks>
        /// 本类的播放、停止、复位、回收和时长计算都按子项递归，且递归经 <see cref="IUIAnimation"/>
        /// 接口下钻，无法用深度参数穿透；因此改为在构造期保证不出现环，使全部递归方法天然有界。
        /// 遍历规模等于被添加子树的节点数（动画构建期一次），不进入按帧执行的播放路径。
        /// </remarks>
        /// <param name="animation">待添加的子动画；为 null 时忽略。</param>
        /// <returns>当前组合实例，便于链式添加。</returns>
        public CompositeAnimation Add(IUIAnimation animation)
        {
            if (animation == null) return this;
            if (animation is CompositeAnimation composite && Reaches(composite, this))
            {
                throw new InvalidOperationException(
                    "Cannot add a composite animation that already contains this instance; it would create a cycle.");
            }

            mAnimations.Add(animation);
            return this;
        }

        /// <summary>
        /// 迭代判断 <paramref name="from"/> 的子树中是否可达 <paramref name="target"/>。
        /// </summary>
        /// <remarks>
        /// 使用 visited 集合而非递归，保证在已存在环的异常结构上自身也可终止。
        /// </remarks>
        /// <param name="from">搜索起点。</param>
        /// <param name="target">搜索目标。</param>
        /// <returns>可达时返回 true。</returns>
        private static bool Reaches(CompositeAnimation from, CompositeAnimation target)
        {
            var pending = new Stack<CompositeAnimation>();
            var visited = new HashSet<CompositeAnimation>();
            pending.Push(from);
            while (pending.Count > 0)
            {
                CompositeAnimation node = pending.Pop();
                if (ReferenceEquals(node, target)) return true;
                if (!visited.Add(node)) continue;

                for (var index = 0; index < node.mAnimations.Count; index++)
                {
                    if (node.mAnimations[index] is CompositeAnimation child) pending.Push(child);
                }
            }

            return false;
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
