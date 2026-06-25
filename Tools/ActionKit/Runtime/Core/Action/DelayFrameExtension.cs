using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的帧级延迟扩展方法。
    /// </summary>
    public static class DelayFrameExtension
    {
        /// <summary>
        /// 向序列中追加一个按帧延迟的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="frameCount">需要等待的帧数。</param>
        /// <param name="onDelayFinish">延迟完成时调用的回调。</param>
        public static ISequence DelayFrame(this ISequence self, int frameCount, Action onDelayFinish = null) =>
            self.Append(YokiFrame.DelayFrame.Allocate(frameCount, onDelayFinish));

        /// <summary>
        /// 向序列中追加一个下一帧执行的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="onDelayFinish">下一帧调用的回调。</param>
        public static ISequence NextFrame(this ISequence self, Action onDelayFinish = null) =>
            self.Append(YokiFrame.DelayFrame.Allocate(1, onDelayFinish));
    }
}
