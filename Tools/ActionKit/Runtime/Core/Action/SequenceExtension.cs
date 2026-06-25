using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的顺序容器扩展方法。
    /// </summary>
    public static class SequenceExtension
    {
        /// <summary>
        /// 向序列中追加一个嵌套序列。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="sequence">嵌套序列配置回调。</param>
        public static ISequence Sequence(this ISequence self, Action<ISequence> sequence = null)
        {
            var nestedSequence = YokiFrame.Sequence.Allocate();
            sequence?.Invoke(nestedSequence);
            return self.Append(nestedSequence);
        }
    }
}
