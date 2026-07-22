using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的嵌套 Repeat fluent 扩展。</summary>
    public static class RepeatExtension
    {
        /// <summary>
        /// 创建、配置并追加一个嵌套重复容器。
        /// </summary>
        /// <param name="self">目标父容器。</param>
        /// <param name="repeat">可选重复体配置回调。</param>
        /// <param name="count">目标轮数；小于等于零表示无限重复。</param>
        /// <param name="condition">每轮完成后决定是否继续的条件。</param>
        /// <returns>原父容器。</returns>
        public static ISequence Repeat(
            this ISequence self,
            Action<IRepeat> repeat,
            int count = -1,
            Func<bool> condition = null)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            IRepeat nested = ActionKit.Repeat(count, condition);
            try
            {
                repeat?.Invoke(nested);
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
