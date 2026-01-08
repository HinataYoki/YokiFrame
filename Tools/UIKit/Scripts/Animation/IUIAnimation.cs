using System;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UI 动画接口
    /// </summary>
    public interface IUIAnimation
    {
        /// <summary>
        /// 动画时长（秒）
        /// </summary>
        float Duration { get; }
        
        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }
        
        /// <summary>
        /// 播放动画（回调方式）
        /// </summary>
        /// <param name="target">动画目标 RectTransform</param>
        /// <param name="onComplete">动画完成回调</param>
        void Play(RectTransform target, Action onComplete = null);
        
        /// <summary>
        /// 停止动画
        /// </summary>
        void Stop();
        
        /// <summary>
        /// 重置到初始状态
        /// </summary>
        /// <param name="target">动画目标 RectTransform</param>
        void Reset(RectTransform target);
        
        /// <summary>
        /// 设置到结束状态
        /// </summary>
        /// <param name="target">动画目标 RectTransform</param>
        void SetToEndState(RectTransform target);
    }

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 支持 UniTask 的动画接口扩展
    /// </summary>
    public interface IUIAnimationUniTask : IUIAnimation
    {
        /// <summary>
        /// 异步播放动画
        /// </summary>
        /// <param name="target">动画目标 RectTransform</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>UniTask</returns>
        UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default);
    }
#endif
}
