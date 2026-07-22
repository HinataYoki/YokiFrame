using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的嵌套 Sequence fluent 扩展。</summary>
    public static class SequenceExtension
    {
        /// <summary>
        /// 创建、配置并追加一个嵌套顺序容器。
        /// </summary>
        /// <param name="self">目标父容器。</param>
        /// <param name="sequence">可选嵌套配置回调。</param>
        /// <returns>原父容器。</returns>
        public static ISequence Sequence(this ISequence self, Action<ISequence> sequence = null)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            ISequence nested = ActionKit.Sequence();
            try
            {
                sequence?.Invoke(nested);
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
