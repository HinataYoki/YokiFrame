using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的秒级延迟扩展方法。
    /// </summary>
    public static class DelayExtension
    {
        /// <summary>
        /// 向序列中追加一个按秒延迟的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="seconds">延迟秒数。</param>
        /// <param name="onDelayFinish">延迟完成时调用的回调。</param>
        public static ISequence Delay(this ISequence self, float seconds, Action onDelayFinish = null) =>
            self.Append(YokiFrame.Delay.Allocate(seconds, onDelayFinish));
    }
}
