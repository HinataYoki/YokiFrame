using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的并行容器扩展方法。
    /// </summary>
    public static class ParallelExtension
    {
        /// <summary>
        /// 向序列中追加一个并行容器。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="parallel">并行容器配置回调。</param>
        /// <param name="waitAll">为 true 时等待所有子 Action 完成；为 false 时任一子 Action 完成即结束。</param>
        public static ISequence Parallel(this ISequence self, Action<ISequence> parallel, bool waitAll = true)
        {
            var parallelAction = YokiFrame.Parallel.Allocate(waitAll);
            parallel?.Invoke(parallelAction);
            return self.Append(parallelAction);
        }
    }
}
