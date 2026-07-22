using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的条件等待 fluent 扩展。</summary>
    public static class ConditionExtension
    {
        /// <summary>
        /// 向容器追加条件满足后完成的节点。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="condition">每次调度检查的条件。</param>
        /// <returns>原容器。</returns>
        public static ISequence Condition(this ISequence self, Func<bool> condition)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, YokiFrame.Condition.Allocate(condition));
        }
    }
}
