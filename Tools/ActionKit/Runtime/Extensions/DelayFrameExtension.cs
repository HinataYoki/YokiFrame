using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的帧级延迟 fluent 扩展。</summary>
    public static class DelayFrameExtension
    {
        /// <summary>
        /// 向容器追加帧级延迟。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="frameCount">需要跨过的实际调度帧数；小于等于零时立即完成。</param>
        /// <param name="onDelayFinish">仅在正常完成时调用的回调。</param>
        /// <returns>原容器。</returns>
        public static ISequence DelayFrame(this ISequence self, int frameCount, Action onDelayFinish = null)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, YokiFrame.DelayFrame.Allocate(frameCount, onDelayFinish));
        }

        /// <summary>
        /// 向容器追加下一调度帧完成的节点。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="onDelayFinish">仅在正常完成时调用的回调。</param>
        /// <returns>原容器。</returns>
        public static ISequence NextFrame(this ISequence self, Action onDelayFinish = null) =>
            DelayFrame(self, 1, onDelayFinish);
    }
}
