using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的秒级延迟 fluent 扩展。</summary>
    public static class DelayExtension
    {
        /// <summary>
        /// 向容器追加秒级延迟。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="seconds">目标等待秒数。</param>
        /// <param name="onDelayFinish">正常完成回调。</param>
        /// <returns>原容器。</returns>
        public static ISequence Delay(this ISequence self, float seconds, Action onDelayFinish = null)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, YokiFrame.Delay.Allocate(seconds, onDelayFinish));
        }
    }
}
