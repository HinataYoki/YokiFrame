using System;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 场景过渡效果接口，定义场景切换时的视觉过渡
    /// </summary>
    public interface ISceneTransition
    {
        /// <summary>
        /// 执行淡出效果（旧场景消失）
        /// </summary>
        /// <param name="onComplete">淡出完成回调</param>
        void FadeOutAsync(Action onComplete);

        /// <summary>
        /// 执行淡入效果（新场景出现）
        /// </summary>
        /// <param name="onComplete">淡入完成回调</param>
        void FadeInAsync(Action onComplete);

        /// <summary>
        /// 当前过渡进度（0-1）
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// 是否正在过渡中
        /// </summary>
        bool IsTransitioning { get; }
    }

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 支持 UniTask 的场景过渡效果接口扩展
    /// </summary>
    public interface ISceneTransitionUniTask : ISceneTransition
    {
        /// <summary>
        /// [UniTask] 执行淡出效果
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask FadeOutUniTaskAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// [UniTask] 执行淡入效果
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask FadeInUniTaskAsync(CancellationToken cancellationToken = default);
    }
#endif
}
