using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的嵌套 Parallel fluent 扩展。</summary>
    public static class ParallelExtension
    {
        /// <summary>
        /// 创建、配置并追加一个嵌套并行容器。
        /// </summary>
        /// <param name="self">目标父容器。</param>
        /// <param name="parallel">可选嵌套分支配置回调。</param>
        /// <param name="waitAll">true 等待全部分支；false 任一分支完成即结束。</param>
        /// <returns>原父容器。</returns>
        public static ISequence Parallel(this ISequence self, Action<ISequence> parallel, bool waitAll = true)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            IParallel nested = ActionKit.Parallel(waitAll);
            try
            {
                parallel?.Invoke(nested);
            }
            catch
            {
                ActionKitScheduler.DiscardUnscheduled(nested);
                throw;
            }
            return ActionRuntime.AppendCreated(self, nested);
        }
    }
}
